using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Connected + transactional ADO.NET repository for Books and their BookAuthors / initial copies.
public sealed class BookRepository : IBookRepository
{
    private static readonly Dictionary<string, string> SortAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "b.Title",
        ["isbn"] = "b.ISBN",
        ["publicationYear"] = "b.PublicationYear",
        ["categoryName"] = "c.CategoryName",
        ["createdAt"] = "b.CreatedAt"
    };

    private readonly IDbConnectionFactory _factory;

    public BookRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<PagedResponse<Book>> GetPagedAsync(BookQueryRequest query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;
        var offset = (page - 1) * pageSize;

        var sortCol = SortAllowlist.TryGetValue(query.SortBy ?? string.Empty, out var mapped) ? mapped : "b.Title";
        var sortDir = string.Equals(query.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

        const string whereClause = """
            WHERE (@IncludeInactive = 1 OR b.IsActive = 1)
              AND (@CategoryId IS NULL OR b.CategoryId = @CategoryId)
              AND (@PublisherId IS NULL OR b.PublisherId = @PublisherId)
              AND (@AuthorId IS NULL OR EXISTS (
                    SELECT 1 FROM dbo.BookAuthors ba WHERE ba.BookId = b.BookId AND ba.AuthorId = @AuthorId
                  ))
              AND (@Search IS NULL
                    OR b.Title LIKE @Pattern
                    OR b.ISBN LIKE @Pattern
                    OR EXISTS (
                        SELECT 1
                        FROM dbo.BookAuthors ba
                        INNER JOIN dbo.Authors a ON a.AuthorId = ba.AuthorId
                        WHERE ba.BookId = b.BookId AND a.AuthorName LIKE @Pattern
                    ))
            """;

        var countSql = $"SELECT COUNT(1) FROM dbo.Books b {whereClause};";
        var dataSql = $"""
            SELECT
                b.BookId,
                b.Title,
                b.ISBN,
                b.CategoryId,
                c.CategoryName,
                b.PublisherId,
                p.PublisherName,
                b.PublicationYear,
                b.Edition,
                b.Language,
                b.Description,
                b.ShelfLocation,
                b.CreatedAt,
                b.UpdatedAt,
                b.IsActive,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId) AS TotalCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Available') AS AvailableCopies
            FROM dbo.Books b
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            LEFT JOIN dbo.Publishers p ON p.PublisherId = b.PublisherId
            {whereClause}
            ORDER BY {sortCol} {sortDir}, b.BookId ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        int totalCount;
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            BindQueryParams(countCmd, query);
            totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
        }

        var books = new List<Book>();
        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            BindQueryParams(dataCmd, query);
            dataCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
            dataCmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                books.Add(MapBook(reader));
            }
        }

        if (books.Count > 0)
        {
            await LoadAuthorsForBooksAsync(conn, books, ct);
        }

        return new PagedResponse<Book>
        {
            Items = books,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<BookDetailResponse?> GetDetailByIdAsync(int bookId, CancellationToken ct = default)
    {
        const string bookSql = """
            SELECT
                b.BookId,
                b.Title,
                b.ISBN,
                b.CategoryId,
                c.CategoryName,
                b.PublisherId,
                p.PublisherName,
                b.PublicationYear,
                b.Edition,
                b.Language,
                b.Description,
                b.ShelfLocation,
                b.CreatedAt,
                b.UpdatedAt,
                b.IsActive,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId) AS TotalCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Available') AS AvailableCopies
            FROM dbo.Books b
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            LEFT JOIN dbo.Publishers p ON p.PublisherId = b.PublisherId
            WHERE b.BookId = @BookId;
            """;

        const string copiesSql = """
            SELECT CopyId, BookId, AccessionNumber, ShelfLocation, AcquisitionDate, CopyStatus, Notes, CreatedAt
            FROM dbo.BookCopies
            WHERE BookId = @BookId
            ORDER BY AccessionNumber ASC;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        Book? book = null;
        await using (var cmd = new SqlCommand(bookSql, conn))
        {
            cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                book = MapBook(reader);
            }
        }

        if (book is null) return null;

        await LoadAuthorsForBooksAsync(conn, new List<Book> { book }, ct);

        var copies = new List<BookCopy>();
        await using (var copyCmd = new SqlCommand(copiesSql, conn))
        {
            copyCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            await using var reader = await copyCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                copies.Add(new BookCopy
                {
                    CopyId = reader.GetInt32(0),
                    BookId = reader.GetInt32(1),
                    BookTitle = book.Title,
                    ISBN = book.ISBN,
                    AccessionNumber = reader.GetString(2),
                    ShelfLocation = AdoHelper.GetNullableString(reader, 3),
                    AcquisitionDate = reader.GetDateTime(4),
                    CopyStatus = reader.GetString(5),
                    Notes = AdoHelper.GetNullableString(reader, 6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
        }

        return new BookDetailResponse { Book = book, Copies = copies };
    }

    public async Task<Book> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var authorIds = request.AuthorIds.Distinct().ToList();
        if (authorIds.Count == 0)
        {
            throw new BusinessRuleException("At least one author must be selected.");
        }

        var normalizedIsbn = request.ISBN.Trim().Replace("-", string.Empty).ToUpperInvariant();

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await ValidateForeignKeysAsync(conn, request.CategoryId, request.PublisherId, authorIds, ct);

        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            const string insertBookSql = """
                INSERT INTO dbo.Books
                    (Title, ISBN, CategoryId, PublisherId, PublicationYear, Edition, Language, Description, ShelfLocation)
                OUTPUT INSERTED.BookId
                VALUES
                    (@Title, @ISBN, @CategoryId, @PublisherId, @PublicationYear, @Edition, @Language, @Description, @ShelfLocation);
                """;

            int bookId;
            await using (var cmd = new SqlCommand(insertBookSql, conn, tx))
            {
                cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 300).Value = request.Title.Trim();
                cmd.Parameters.Add("@ISBN", SqlDbType.NVarChar, 20).Value = normalizedIsbn;
                cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = request.CategoryId;
                cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(request.PublisherId);
                cmd.Parameters.Add("@PublicationYear", SqlDbType.Int).Value = request.PublicationYear;
                cmd.Parameters.Add("@Edition", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Edition) ? null : request.Edition.Trim());
                cmd.Parameters.Add("@Language", SqlDbType.NVarChar, 50).Value = request.Language.Trim();
                cmd.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());
                cmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ShelfLocation) ? null : request.ShelfLocation.Trim());

                bookId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            await SyncBookAuthorsAsync(conn, tx, bookId, authorIds, replaceExisting: false, ct);

            // Optionally create initial physical copies atomically with the new title
            if (request.InitialCopies > 0)
            {
                var prefix = string.IsNullOrWhiteSpace(request.AccessionPrefix)
                    ? $"ACC-{bookId:D3}"
                    : request.AccessionPrefix.Trim().ToUpperInvariant();

                const string copySql = """
                    INSERT INTO dbo.BookCopies (BookId, AccessionNumber, ShelfLocation, CopyStatus, Notes)
                    VALUES (@BookId, @AccessionNumber, @ShelfLocation, N'Available', N'Initial copy');
                    """;

                for (var i = 1; i <= request.InitialCopies; i++)
                {
                    await using var copyCmd = new SqlCommand(copySql, conn, tx);
                    copyCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
                    copyCmd.Parameters.Add("@AccessionNumber", SqlDbType.NVarChar, 50).Value = $"{prefix}-{i:D2}";
                    copyCmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
                        AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ShelfLocation) ? null : request.ShelfLocation.Trim());
                    await copyCmd.ExecuteNonQueryAsync(ct);
                }
            }

            await tx.CommitAsync(ct);
            return (await GetDetailByIdAsync(bookId, ct))!.Book;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct);
            throw new DuplicateResourceException($"A book with ISBN '{normalizedIsbn}' or duplicate accession number already exists.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Book> UpdateAsync(int bookId, UpdateBookRequest request, CancellationToken ct = default)
    {
        var authorIds = request.AuthorIds.Distinct().ToList();
        if (authorIds.Count == 0)
        {
            throw new BusinessRuleException("At least one author must be selected.");
        }

        var normalizedIsbn = request.ISBN.Trim().Replace("-", string.Empty).ToUpperInvariant();

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await ValidateForeignKeysAsync(conn, request.CategoryId, request.PublisherId, authorIds, ct);

        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            const string updateSql = """
                UPDATE dbo.Books
                SET Title = @Title,
                    ISBN = @ISBN,
                    CategoryId = @CategoryId,
                    PublisherId = @PublisherId,
                    PublicationYear = @PublicationYear,
                    Edition = @Edition,
                    Language = @Language,
                    Description = @Description,
                    ShelfLocation = @ShelfLocation,
                    IsActive = @IsActive,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE BookId = @BookId;
                """;

            await using (var cmd = new SqlCommand(updateSql, conn, tx))
            {
                cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
                cmd.Parameters.Add("@Title", SqlDbType.NVarChar, 300).Value = request.Title.Trim();
                cmd.Parameters.Add("@ISBN", SqlDbType.NVarChar, 20).Value = normalizedIsbn;
                cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = request.CategoryId;
                cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(request.PublisherId);
                cmd.Parameters.Add("@PublicationYear", SqlDbType.Int).Value = request.PublicationYear;
                cmd.Parameters.Add("@Edition", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Edition) ? null : request.Edition.Trim());
                cmd.Parameters.Add("@Language", SqlDbType.NVarChar, 50).Value = request.Language.Trim();
                cmd.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());
                cmd.Parameters.Add("@ShelfLocation", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ShelfLocation) ? null : request.ShelfLocation.Trim());
                cmd.Parameters.Add("@IsActive", SqlDbType.Bit).Value = request.IsActive;

                if (await cmd.ExecuteNonQueryAsync(ct) == 0)
                {
                    throw new ResourceNotFoundException($"Book #{bookId} was not found.");
                }
            }

            await SyncBookAuthorsAsync(conn, tx, bookId, authorIds, replaceExisting: true, ct);
            await tx.CommitAsync(ct);

            return (await GetDetailByIdAsync(bookId, ct))!.Book;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct);
            throw new DuplicateResourceException($"Another book with ISBN '{normalizedIsbn}' already exists.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task DeleteOrDeactivateAsync(int bookId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = @BookId) THEN 1 ELSE 0 END,
                (SELECT COUNT(1) FROM dbo.BookCopies bc INNER JOIN dbo.Loans l ON l.CopyId = bc.CopyId WHERE bc.BookId = @BookId),
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = @BookId AND bc.CopyStatus = N'Borrowed'),
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = @BookId);
            """;

        bool hasHistory = false;
        int copyCount = 0;

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt32(0) == 0)
                {
                    throw new ResourceNotFoundException($"Book #{bookId} was not found.");
                }

                hasHistory = reader.GetInt32(1) > 0;
                var activeBorrowed = reader.GetInt32(2);
                copyCount = reader.GetInt32(3);

                if (activeBorrowed > 0)
                {
                    throw new ReferentialIntegrityException(
                        $"Cannot delete or deactivate book #{bookId} while {activeBorrowed} physical copy/copies are currently borrowed.");
                }
            }
        }

        // Preserve titles referenced by loans or physical copies by marking inactive instead of hard-deleting
        if (hasHistory || copyCount > 0)
        {
            const string deactivateSql = """
                UPDATE dbo.Books
                SET IsActive = 0,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE BookId = @BookId;
                """;
            await using var cmd = new SqlCommand(deactivateSql, conn);
            cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        const string deleteSql = "DELETE FROM dbo.Books WHERE BookId = @BookId;";
        await using var delCmd = new SqlCommand(deleteSql, conn);
        delCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
        await delCmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ValidateForeignKeysAsync(
        SqlConnection conn,
        int categoryId,
        int? publisherId,
        List<int> authorIds,
        CancellationToken ct)
    {
        await using (var catCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Categories WHERE CategoryId = @Id;", conn))
        {
            catCmd.Parameters.Add("@Id", SqlDbType.Int).Value = categoryId;
            if (Convert.ToInt32(await catCmd.ExecuteScalarAsync(ct)) == 0)
            {
                throw new BusinessRuleException($"Category #{categoryId} does not exist.");
            }
        }

        if (publisherId.HasValue)
        {
            await using var pubCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Publishers WHERE PublisherId = @Id;", conn);
            pubCmd.Parameters.Add("@Id", SqlDbType.Int).Value = publisherId.Value;
            if (Convert.ToInt32(await pubCmd.ExecuteScalarAsync(ct)) == 0)
            {
                throw new BusinessRuleException($"Publisher #{publisherId.Value} does not exist.");
            }
        }

        foreach (var authorId in authorIds)
        {
            await using var authCmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Authors WHERE AuthorId = @Id;", conn);
            authCmd.Parameters.Add("@Id", SqlDbType.Int).Value = authorId;
            if (Convert.ToInt32(await authCmd.ExecuteScalarAsync(ct)) == 0)
            {
                throw new BusinessRuleException($"Author #{authorId} does not exist.");
            }
        }
    }

    private static async Task SyncBookAuthorsAsync(
        SqlConnection conn,
        SqlTransaction tx,
        int bookId,
        List<int> authorIds,
        bool replaceExisting,
        CancellationToken ct)
    {
        if (replaceExisting)
        {
            await using var delCmd = new SqlCommand("DELETE FROM dbo.BookAuthors WHERE BookId = @BookId;", conn, tx);
            delCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            await delCmd.ExecuteNonQueryAsync(ct);
        }

        const string insertLink = "INSERT INTO dbo.BookAuthors (BookId, AuthorId) VALUES (@BookId, @AuthorId);";
        foreach (var authorId in authorIds)
        {
            await using var cmd = new SqlCommand(insertLink, conn, tx);
            cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
            cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task LoadAuthorsForBooksAsync(SqlConnection conn, List<Book> books, CancellationToken ct)
    {
        var paramNames = books.Select((_, idx) => $"@b{idx}").ToList();
        var sql = $"""
            SELECT ba.BookId, a.AuthorId, a.AuthorName
            FROM dbo.BookAuthors ba
            INNER JOIN dbo.Authors a ON a.AuthorId = ba.AuthorId
            WHERE ba.BookId IN ({string.Join(",", paramNames)})
            ORDER BY a.AuthorName ASC;
            """;

        var map = books.ToDictionary(b => b.BookId);
        await using var cmd = new SqlCommand(sql, conn);
        for (var i = 0; i < books.Count; i++)
        {
            cmd.Parameters.Add(paramNames[i], SqlDbType.Int).Value = books[i].BookId;
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var bookId = reader.GetInt32(0);
            if (map.TryGetValue(bookId, out var book))
            {
                book.Authors.Add(new AuthorSummary
                {
                    AuthorId = reader.GetInt32(1),
                    AuthorName = reader.GetString(2)
                });
            }
        }
    }

    private static void BindQueryParams(SqlCommand cmd, BookQueryRequest query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        cmd.Parameters.Add("@IncludeInactive", SqlDbType.Bit).Value = query.IncludeInactive == true ? 1 : 0;
        cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.CategoryId);
        cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.PublisherId);
        cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.AuthorId);
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 300).Value = AdoHelper.DbNullIfNull(search);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 302).Value = search is null ? DBNull.Value : $"%{search}%";
    }

    private static Book MapBook(SqlDataReader reader) => new()
    {
        BookId = reader.GetInt32(0),
        Title = reader.GetString(1),
        ISBN = reader.GetString(2),
        CategoryId = reader.GetInt32(3),
        CategoryName = reader.GetString(4),
        PublisherId = AdoHelper.GetNullableInt32(reader, 5),
        PublisherName = AdoHelper.GetNullableString(reader, 6),
        PublicationYear = reader.GetInt32(7),
        Edition = AdoHelper.GetNullableString(reader, 8),
        Language = reader.GetString(9),
        Description = AdoHelper.GetNullableString(reader, 10),
        ShelfLocation = AdoHelper.GetNullableString(reader, 11),
        CreatedAt = reader.GetDateTime(12),
        UpdatedAt = reader.GetDateTime(13),
        IsActive = reader.GetBoolean(14),
        TotalCopies = reader.GetInt32(15),
        AvailableCopies = reader.GetInt32(16)
    };
}
