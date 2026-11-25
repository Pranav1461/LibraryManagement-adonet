using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Connected + transactional repository for physical book copies and copy-level history.
public sealed class BookCopyRepository : IBookCopyRepository
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Borrowed", "Reserved", "Lost", "Damaged", "Inactive"
    };

    private readonly IDbConnectionFactory _factory;

    public BookCopyRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<PagedResponse<BookCopy>> GetPagedAsync(BookCopyQueryRequest query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 15 : query.PageSize;
        var offset = (page - 1) * pageSize;

        const string whereClause = """
            WHERE (@BookId IS NULL OR bc.BookId = @BookId)
              AND (@CopyStatus IS NULL OR bc.CopyStatus = @CopyStatus)
              AND (@Search IS NULL OR bc.AccessionNumber LIKE @Pattern OR b.Title LIKE @Pattern OR b.ISBN LIKE @Pattern)
            """;

        var countSql = $"""
            SELECT COUNT(1)
            FROM dbo.BookCopies bc
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                bc.CopyId,
                bc.BookId,
                b.Title,
                b.ISBN,
                bc.AccessionNumber,
                bc.ShelfLocation,
                bc.AcquisitionDate,
                bc.CopyStatus,
                bc.Notes,
                bc.CreatedAt
            FROM dbo.BookCopies bc
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            {whereClause}
            ORDER BY bc.AccessionNumber ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        int total;
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            BindQueryParams(countCmd, query);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
        }

        var items = new List<BookCopy>();
        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            BindQueryParams(dataCmd, query);
            dataCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
            dataCmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(MapCopy(reader));
            }
        }

        return new PagedResponse<BookCopy>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<BookCopyDetailResponse?> GetDetailByIdAsync(int copyId, CancellationToken ct = default)
    {
        const string copySql = """
            SELECT
                bc.CopyId,
                bc.BookId,
                b.Title,
                b.ISBN,
                bc.AccessionNumber,
                bc.ShelfLocation,
                bc.AcquisitionDate,
                bc.CopyStatus,
                bc.Notes,
                bc.CreatedAt
            FROM dbo.BookCopies bc
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE bc.CopyId = @CopyId;
            """;

        const string historySql = """
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.CopyId = @CopyId
            ORDER BY l.IssueDate DESC, l.LoanId DESC;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        BookCopy? copy = null;
        await using (var cmd = new SqlCommand(copySql, conn))
        {
            cmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = copyId;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                copy = MapCopy(reader);
            }
        }

        if (copy is null) return null;

        var history = new List<Loan>();
        await using (var histCmd = new SqlCommand(historySql, conn))
        {
            histCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = copyId;
            await using var reader = await histCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                history.Add(LoanRepository.MapLoan(reader));
            }
        }

        return new BookCopyDetailResponse { Copy = copy, LoanHistory = history };
    }

    public async Task<BookCopy> CreateAsync(CreateBookCopyRequest request, CancellationToken ct = default)
    {
        var status = NormalizeStatus(request.CopyStatus);
        if (status == "Borrowed")
        {
            throw new BusinessRuleException("A newly added physical copy cannot start with status 'Borrowed'. Issue it via Loans.");
        }

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await EnsureBookExistsAsync(conn, null, request.BookId, ct);

        const string sql = """
            INSERT INTO dbo.BookCopies (BookId, AccessionNumber, ShelfLocation, AcquisitionDate, CopyStatus, Notes)
            OUTPUT INSERTED.CopyId
            VALUES (@BookId, @AccessionNumber, @ShelfLocation, @AcquisitionDate, @CopyStatus, @Notes);
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = request.BookId;
        cmd.Parameters.Add("@AccessionNumber", SqlDbType.NVarChar, 50).Value = request.AccessionNumber.Trim().ToUpperInvariant();
        cmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ShelfLocation) ? null : request.ShelfLocation.Trim());
        cmd.Parameters.Add("@AcquisitionDate", SqlDbType.DateTime2).Value = request.AcquisitionDate ?? DateTime.UtcNow;
        cmd.Parameters.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = status;
        cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        try
        {
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return (await GetDetailByIdAsync(id, ct))!.Copy;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Accession number '{request.AccessionNumber.Trim().ToUpperInvariant()}' already exists.");
        }
    }

    public async Task<List<BookCopy>> BulkCreateAsync(BulkCreateBookCopiesRequest request, CancellationToken ct = default)
    {
        if (request.Copies is null || request.Copies.Count == 0)
        {
            throw new BusinessRuleException("Provide at least one physical copy to add.");
        }

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            await EnsureBookExistsAsync(conn, tx, request.BookId, ct);

            const string sql = """
                INSERT INTO dbo.BookCopies (BookId, AccessionNumber, ShelfLocation, AcquisitionDate, CopyStatus, Notes)
                OUTPUT INSERTED.CopyId
                VALUES (@BookId, @AccessionNumber, @ShelfLocation, @AcquisitionDate, @CopyStatus, @Notes);
                """;

            var insertedIds = new List<int>();
            foreach (var item in request.Copies)
            {
                var status = NormalizeStatus(item.CopyStatus);
                if (status == "Borrowed")
                {
                    throw new BusinessRuleException("New physical copies cannot be created with status 'Borrowed'.");
                }

                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = request.BookId;
                cmd.Parameters.Add("@AccessionNumber", SqlDbType.NVarChar, 50).Value = item.AccessionNumber.Trim().ToUpperInvariant();
                cmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(item.ShelfLocation) ? null : item.ShelfLocation.Trim());
                cmd.Parameters.Add("@AcquisitionDate", SqlDbType.DateTime2).Value = item.AcquisitionDate ?? DateTime.UtcNow;
                cmd.Parameters.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = status;
                cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim());

                insertedIds.Add(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)));
            }

            await tx.CommitAsync(ct);

            var result = new List<BookCopy>();
            foreach (var id in insertedIds)
            {
                var detail = await GetDetailByIdAsync(id, ct);
                if (detail is not null) result.Add(detail.Copy);
            }

            return result;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct);
            throw new DuplicateResourceException("One or more accession numbers already exist.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<BookCopy> UpdateAsync(int copyId, UpdateBookCopyRequest request, CancellationToken ct = default)
    {
        var newStatus = NormalizeStatus(request.CopyStatus);

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                bc.CopyStatus,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.CopyId = bc.CopyId AND l.ReturnDate IS NULL)
            FROM dbo.BookCopies bc
            WHERE bc.CopyId = @CopyId;
            """;

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = copyId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new ResourceNotFoundException($"Physical copy #{copyId} was not found.");
            }

            var hasActiveLoan = reader.GetInt32(1) > 0;
            if (hasActiveLoan && newStatus != "Borrowed")
            {
                throw new BusinessRuleException("Cannot change copy status while it has an active unreturned loan.");
            }

            if (!hasActiveLoan && newStatus == "Borrowed")
            {
                throw new BusinessRuleException("Cannot set status to 'Borrowed' directly without an active loan.");
            }
        }

        const string sql = """
            UPDATE dbo.BookCopies
            SET AccessionNumber = @AccessionNumber,
                ShelfLocation = @ShelfLocation,
                AcquisitionDate = COALESCE(@AcquisitionDate, AcquisitionDate),
                CopyStatus = @CopyStatus,
                Notes = @Notes
            WHERE CopyId = @CopyId;
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = copyId;
        cmd.Parameters.Add("@AccessionNumber", SqlDbType.NVarChar, 50).Value = request.AccessionNumber.Trim().ToUpperInvariant();
        cmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ShelfLocation) ? null : request.ShelfLocation.Trim());
        cmd.Parameters.Add("@AcquisitionDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(request.AcquisitionDate);
        cmd.Parameters.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = newStatus;
        cmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return (await GetDetailByIdAsync(copyId, ct))!.Copy;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Accession number '{request.AccessionNumber.Trim().ToUpperInvariant()}' already exists.");
        }
    }

    private static string NormalizeStatus(string? status)
    {
        var trimmed = string.IsNullOrWhiteSpace(status) ? "Available" : status.Trim();
        var match = ValidStatuses.FirstOrDefault(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new BusinessRuleException($"Invalid copy status '{status}'. Allowed: Available, Borrowed, Reserved, Lost, Damaged, Inactive.");
        }
        return match;
    }

    private static async Task EnsureBookExistsAsync(SqlConnection conn, SqlTransaction? tx, int bookId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Books WHERE BookId = @BookId;", conn, tx);
        cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 0)
        {
            throw new ResourceNotFoundException($"Book #{bookId} was not found.");
        }
    }

    private static void BindQueryParams(SqlCommand cmd, BookCopyQueryRequest query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var status = string.IsNullOrWhiteSpace(query.CopyStatus) ? null : query.CopyStatus.Trim();
        cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.BookId);
        cmd.Parameters.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(status);
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 300).Value = AdoHelper.DbNullIfNull(search);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 302).Value = search is null ? DBNull.Value : $"%{search}%";
    }

    private static BookCopy MapCopy(SqlDataReader reader) => new()
    {
        CopyId = reader.GetInt32(0),
        BookId = reader.GetInt32(1),
        BookTitle = reader.GetString(2),
        ISBN = reader.GetString(3),
        AccessionNumber = reader.GetString(4),
        ShelfLocation = AdoHelper.GetNullableString(reader, 5),
        AcquisitionDate = reader.GetDateTime(6),
        CopyStatus = reader.GetString(7),
        Notes = AdoHelper.GetNullableString(reader, 8),
        CreatedAt = reader.GetDateTime(9)
    };
}
