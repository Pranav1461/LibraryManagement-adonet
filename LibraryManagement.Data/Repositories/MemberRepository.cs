using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Connected ADO.NET repository for library members, membership status, and loan history.
public sealed class MemberRepository : IMemberRepository
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Inactive", "Expired"
    };

    private readonly IDbConnectionFactory _factory;

    public MemberRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<PagedResponse<Member>> GetPagedAsync(MemberQueryRequest query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;
        var offset = (page - 1) * pageSize;

        const string whereClause = """
            WHERE (@Status IS NULL OR m.MembershipStatus = @Status)
              AND (@Search IS NULL
                    OR m.FullName LIKE @Pattern
                    OR m.MembershipNumber LIKE @Pattern
                    OR m.Email LIKE @Pattern
                    OR m.Phone LIKE @Pattern)
            """;

        var countSql = $"SELECT COUNT(1) FROM dbo.Members m {whereClause};";
        var dataSql = $"""
            SELECT
                m.MemberId,
                m.MembershipNumber,
                m.FullName,
                m.Email,
                m.Phone,
                m.Address,
                m.RegistrationDate,
                m.MembershipExpiryDate,
                m.MembershipStatus,
                m.CreatedAt,
                m.UpdatedAt,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.MemberId = m.MemberId AND l.ReturnDate IS NULL) AS ActiveLoanCount,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.MemberId = m.MemberId AND l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE)) AS OverdueLoanCount
            FROM dbo.Members m
            {whereClause}
            ORDER BY m.FullName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        int total;
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            BindQueryParams(countCmd, query);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
        }

        var items = new List<Member>();
        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            BindQueryParams(dataCmd, query);
            dataCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
            dataCmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(MapMember(reader));
            }
        }

        return new PagedResponse<Member>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<MemberDetailResponse?> GetDetailByIdAsync(int memberId, CancellationToken ct = default)
    {
        const string memberSql = """
            SELECT
                m.MemberId,
                m.MembershipNumber,
                m.FullName,
                m.Email,
                m.Phone,
                m.Address,
                m.RegistrationDate,
                m.MembershipExpiryDate,
                m.MembershipStatus,
                m.CreatedAt,
                m.UpdatedAt,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.MemberId = m.MemberId AND l.ReturnDate IS NULL) AS ActiveLoanCount,
                (SELECT COUNT(1) FROM dbo.Loans l WHERE l.MemberId = m.MemberId AND l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE)) AS OverdueLoanCount
            FROM dbo.Members m
            WHERE m.MemberId = @MemberId;
            """;

        const string loansSql = """
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.MemberId = @MemberId
            ORDER BY l.IssueDate DESC, l.LoanId DESC;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        Member? member = null;
        await using (var cmd = new SqlCommand(memberSql, conn))
        {
            cmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                member = MapMember(reader);
            }
        }

        if (member is null) return null;

        var history = new List<Loan>();
        await using (var loanCmd = new SqlCommand(loansSql, conn))
        {
            loanCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            await using var reader = await loanCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                history.Add(LoanRepository.MapLoan(reader));
            }
        }

        return new MemberDetailResponse
        {
            Member = member,
            CurrentLoans = history.Where(l => l.ReturnDate is null).ToList(),
            OverdueLoans = history.Where(l => l.IsOverdue).ToList(),
            BorrowingHistory = history
        };
    }

    public async Task<Member> CreateAsync(CreateMemberRequest request, CancellationToken ct = default)
    {
        var status = NormalizeStatus(request.MembershipStatus);
        var expiry = request.MembershipExpiryDate ?? DateTime.UtcNow.AddYears(1);

        const string sql = """
            INSERT INTO dbo.Members
                (MembershipNumber, FullName, Email, Phone, Address, MembershipExpiryDate, MembershipStatus)
            OUTPUT INSERTED.MemberId
            VALUES
                (@MembershipNumber, @FullName, @Email, @Phone, @Address, @MembershipExpiryDate, @MembershipStatus);
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindWriteParams(cmd, request, expiry, status);

        try
        {
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return (await GetDetailByIdAsync(id, ct))!.Member;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException("A member with that membership number or email already exists.");
        }
    }

    public async Task<Member> UpdateAsync(int memberId, UpdateMemberRequest request, CancellationToken ct = default)
    {
        var status = NormalizeStatus(request.MembershipStatus);
        var expiry = request.MembershipExpiryDate ?? DateTime.UtcNow.AddYears(1);

        const string sql = """
            UPDATE dbo.Members
            SET MembershipNumber = @MembershipNumber,
                FullName = @FullName,
                Email = @Email,
                Phone = @Phone,
                Address = @Address,
                MembershipExpiryDate = @MembershipExpiryDate,
                MembershipStatus = @MembershipStatus,
                UpdatedAt = SYSUTCDATETIME()
            WHERE MemberId = @MemberId;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        BindWriteParams(cmd, request, expiry, status);

        try
        {
            if (await cmd.ExecuteNonQueryAsync(ct) == 0)
            {
                throw new ResourceNotFoundException($"Member #{memberId} was not found.");
            }
            return (await GetDetailByIdAsync(memberId, ct))!.Member;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException("Another member with that membership number or email already exists.");
        }
    }

    public async Task DeleteOrDeactivateAsync(int memberId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Members WHERE MemberId = @MemberId) THEN 1 ELSE 0 END,
                (SELECT COUNT(1) FROM dbo.Loans WHERE MemberId = @MemberId),
                (SELECT COUNT(1) FROM dbo.Loans WHERE MemberId = @MemberId AND ReturnDate IS NULL);
            """;

        bool hasHistory = false;
        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt32(0) == 0)
                {
                    throw new ResourceNotFoundException($"Member #{memberId} was not found.");
                }

                hasHistory = reader.GetInt32(1) > 0;
                var activeLoans = reader.GetInt32(2);
                if (activeLoans > 0)
                {
                    throw new ReferentialIntegrityException(
                        $"Cannot delete or deactivate member #{memberId} while they have {activeLoans} unreturned book(s).");
                }
            }
        }

        // Keep members with loan history in the DB by marking them Inactive
        if (hasHistory)
        {
            const string deactivateSql = """
                UPDATE dbo.Members
                SET MembershipStatus = N'Inactive',
                    UpdatedAt = SYSUTCDATETIME()
                WHERE MemberId = @MemberId;
                """;
            await using var cmd = new SqlCommand(deactivateSql, conn);
            cmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            await cmd.ExecuteNonQueryAsync(ct);
            return;
        }

        const string delSql = "DELETE FROM dbo.Members WHERE MemberId = @MemberId;";
        await using var delCmd = new SqlCommand(delSql, conn);
        delCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        await delCmd.ExecuteNonQueryAsync(ct);
    }

    private static string NormalizeStatus(string? status)
    {
        var trimmed = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
        var match = ValidStatuses.FirstOrDefault(s => s.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new BusinessRuleException($"Invalid membership status '{status}'. Allowed: Active, Inactive, Expired.");
        }
        return match;
    }

    private static void BindWriteParams(SqlCommand cmd, CreateMemberRequest request, DateTime expiry, string status)
    {
        cmd.Parameters.Add("@MembershipNumber", SqlDbType.NVarChar, 50).Value = request.MembershipNumber.Trim().ToUpperInvariant();
        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = request.FullName.Trim();
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = request.Email.Trim();
        cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = request.Phone.Trim();
        cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim());
        cmd.Parameters.Add("@MembershipExpiryDate", SqlDbType.DateTime2).Value = expiry;
        cmd.Parameters.Add("@MembershipStatus", SqlDbType.NVarChar, 20).Value = status;
    }

    private static void BindQueryParams(SqlCommand cmd, MemberQueryRequest query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var status = string.IsNullOrWhiteSpace(query.MembershipStatus) ? null : query.MembershipStatus.Trim();
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(status);
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = AdoHelper.DbNullIfNull(search);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 202).Value = search is null ? DBNull.Value : $"%{search}%";
    }

    private static Member MapMember(SqlDataReader reader) => new()
    {
        MemberId = reader.GetInt32(0),
        MembershipNumber = reader.GetString(1),
        FullName = reader.GetString(2),
        Email = reader.GetString(3),
        Phone = reader.GetString(4),
        Address = AdoHelper.GetNullableString(reader, 5),
        RegistrationDate = reader.GetDateTime(6),
        MembershipExpiryDate = reader.GetDateTime(7),
        MembershipStatus = reader.GetString(8),
        CreatedAt = reader.GetDateTime(9),
        UpdatedAt = reader.GetDateTime(10),
        ActiveLoanCount = reader.GetInt32(11),
        OverdueLoanCount = reader.GetInt32(12)
    };
}
