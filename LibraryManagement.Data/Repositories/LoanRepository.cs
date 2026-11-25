using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Configuration;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.Data.Repositories;

// Transactional ADO.NET repository for issuing, returning, and renewing book loans.
public sealed class LoanRepository : ILoanRepository
{
    private static readonly HashSet<string> AllowedReturnConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Available", "Damaged", "Lost"
    };

    private readonly IDbConnectionFactory _factory;
    private readonly LibraryPolicySettings _policy;

    public LoanRepository(IDbConnectionFactory factory, IOptions<LibraryPolicySettings> policy)
    {
        _factory = factory;
        _policy = policy.Value;
    }

    public async Task<PagedResponse<Loan>> GetPagedAsync(LoanQueryRequest query, CancellationToken ct = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 15 : query.PageSize;
        var offset = (page - 1) * pageSize;

        const string whereClause = """
            WHERE (@MemberId IS NULL OR l.MemberId = @MemberId)
              AND (@BookId IS NULL OR bc.BookId = @BookId)
              AND (@CopyId IS NULL OR l.CopyId = @CopyId)
              AND (@LoanStatus IS NULL
                    OR (@LoanStatus = 'Active' AND l.ReturnDate IS NULL)
                    OR (@LoanStatus = 'Overdue' AND l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE))
                    OR (l.LoanStatus = @LoanStatus))
              AND (@OverdueOnly IS NULL OR @OverdueOnly = 0 OR (l.ReturnDate IS NULL AND l.DueDate < CAST(SYSUTCDATETIME() AS DATE)))
              AND (@StartDate IS NULL OR l.IssueDate >= @StartDate)
              AND (@EndDate IS NULL OR l.IssueDate < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
              AND (@Search IS NULL
                    OR m.FullName LIKE @Pattern
                    OR m.MembershipNumber LIKE @Pattern
                    OR bc.AccessionNumber LIKE @Pattern
                    OR b.Title LIKE @Pattern
                    OR b.ISBN LIKE @Pattern)
            """;

        var countSql = $"""
            SELECT COUNT(1)
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            {whereClause}
            ORDER BY
                CASE WHEN l.ReturnDate IS NULL THEN 0 ELSE 1 END ASC,
                l.DueDate ASC,
                l.LoanId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        int total;
        await using (var countCmd = new SqlCommand(countSql, conn))
        {
            BindQueryParams(countCmd, query);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));
        }

        var items = new List<Loan>();
        await using (var dataCmd = new SqlCommand(dataSql, conn))
        {
            BindQueryParams(dataCmd, query);
            dataCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = offset;
            dataCmd.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

            await using var reader = await dataCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(MapLoan(reader));
            }
        }

        return new PagedResponse<Loan>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<LoanDetailResponse?> GetDetailByIdAsync(int loanId, CancellationToken ct = default)
    {
        const string loanSql = """
            SELECT
                l.LoanId, l.MemberId, m.MembershipNumber, m.FullName,
                l.CopyId, bc.AccessionNumber, b.BookId, b.Title, b.ISBN,
                l.IssueDate, l.DueDate, l.ReturnDate, l.LoanStatus,
                l.FineAmount, l.RenewalCount, l.Notes, l.CreatedAt
            FROM dbo.Loans l
            INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
            INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
            INNER JOIN dbo.Books b ON b.BookId = bc.BookId
            WHERE l.LoanId = @LoanId;
            """;

        const string renewalSql = """
            SELECT RenewalId, LoanId, PreviousDueDate, NewDueDate, RenewalDate
            FROM dbo.LoanRenewals
            WHERE LoanId = @LoanId
            ORDER BY RenewalDate DESC, RenewalId DESC;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        Loan? loan = null;
        await using (var cmd = new SqlCommand(loanSql, conn))
        {
            cmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                loan = MapLoan(reader);
            }
        }

        if (loan is null) return null;

        var renewals = new List<LoanRenewal>();
        await using (var renCmd = new SqlCommand(renewalSql, conn))
        {
            renCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
            await using var reader = await renCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                renewals.Add(new LoanRenewal
                {
                    RenewalId = reader.GetInt32(0),
                    LoanId = reader.GetInt32(1),
                    PreviousDueDate = reader.GetDateTime(2),
                    NewDueDate = reader.GetDateTime(3),
                    RenewalDate = reader.GetDateTime(4)
                });
            }
        }

        return new LoanDetailResponse { Loan = loan, Renewals = renewals };
    }

    public async Task<Loan> IssueBookAsync(IssueBookRequest request, CancellationToken ct = default)
    {
        var issueDate = request.IssueDate ?? DateTime.UtcNow;
        var duration = request.DurationDays ?? _policy.DefaultBorrowingDays;
        if (duration <= 0)
        {
            throw new BusinessRuleException("Borrowing duration must be at least 1 day.");
        }
        var dueDate = issueDate.AddDays(duration);

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            // 1. Check member exists, is active, hasn't expired, and is under the borrowing limit
            const string memberSql = """
                SELECT
                    m.FullName,
                    m.MembershipStatus,
                    m.MembershipExpiryDate,
                    (SELECT COUNT(1) FROM dbo.Loans l WHERE l.MemberId = m.MemberId AND l.ReturnDate IS NULL) AS ActiveLoans
                FROM dbo.Members m
                WHERE m.MemberId = @MemberId;
                """;

            await using (var memCmd = new SqlCommand(memberSql, conn, tx))
            {
                memCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = request.MemberId;
                await using var reader = await memCmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new ResourceNotFoundException($"Member #{request.MemberId} was not found.");
                }

                var memberName = reader.GetString(0);
                var status = reader.GetString(1);
                var expiry = reader.GetDateTime(2);
                var activeLoans = reader.GetInt32(3);

                if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BusinessRuleException($"Member '{memberName}' cannot borrow because membership status is '{status}'.");
                }

                if (expiry.Date < issueDate.Date)
                {
                    throw new BusinessRuleException($"Member '{memberName}' has an expired membership (expired {expiry:yyyy-MM-dd}).");
                }

                if (activeLoans >= _policy.MaxActiveLoansPerMember)
                {
                    throw new BusinessRuleException(
                        $"Member '{memberName}' has reached the maximum borrowing limit of {_policy.MaxActiveLoansPerMember} active books.");
                }
            }

            // 2. Atomically claim the physical copy only if it is Available and its book title is active
            const string claimCopySql = """
                UPDATE bc
                SET bc.CopyStatus = N'Borrowed'
                OUTPUT INSERTED.BookId
                FROM dbo.BookCopies bc
                INNER JOIN dbo.Books b ON b.BookId = bc.BookId
                WHERE bc.CopyId = @CopyId
                  AND bc.CopyStatus = N'Available'
                  AND b.IsActive = 1;
                """;

            int bookId;
            object? claimedBookObj;
            await using (var claimCmd = new SqlCommand(claimCopySql, conn, tx))
            {
                claimCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = request.CopyId;
                claimedBookObj = await claimCmd.ExecuteScalarAsync(ct);
            }

            if (claimedBookObj is null)
            {
                const string inspectCopySql = """
                    SELECT bc.AccessionNumber, bc.CopyStatus, b.IsActive
                    FROM dbo.BookCopies bc
                    INNER JOIN dbo.Books b ON b.BookId = bc.BookId
                    WHERE bc.CopyId = @CopyId;
                    """;
                await using var inspectCmd = new SqlCommand(inspectCopySql, conn, tx);
                inspectCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = request.CopyId;
                await using var reader = await inspectCmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new ResourceNotFoundException($"Physical copy #{request.CopyId} was not found.");
                }

                var accession = reader.GetString(0);
                var copyStatus = reader.GetString(1);
                var isActive = reader.GetBoolean(2);
                if (!isActive)
                {
                    throw new BusinessRuleException($"Copy '{accession}' belongs to an inactive book title and cannot be issued.");
                }
                throw new BusinessRuleException($"Copy '{accession}' is currently '{copyStatus}' and is not available for borrowing.");
            }

            bookId = Convert.ToInt32(claimedBookObj);

            // 3. Create the loan record (UX_Loans_ActiveCopy enforces uniqueness at DB level too)
            const string insertLoanSql = """
                INSERT INTO dbo.Loans (MemberId, CopyId, IssueDate, DueDate, LoanStatus, FineAmount, RenewalCount, Notes)
                OUTPUT INSERTED.LoanId
                VALUES (@MemberId, @CopyId, @IssueDate, @DueDate, N'Issued', 0, 0, @Notes);
                """;

            int loanId;
            await using (var loanCmd = new SqlCommand(insertLoanSql, conn, tx))
            {
                loanCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = request.MemberId;
                loanCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = request.CopyId;
                loanCmd.Parameters.Add("@IssueDate", SqlDbType.DateTime2).Value = issueDate;
                loanCmd.Parameters.Add("@DueDate", SqlDbType.DateTime2).Value = dueDate;
                loanCmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());
                loanId = Convert.ToInt32(await loanCmd.ExecuteScalarAsync(ct));
            }

            // 4. Fulfill any pending reservation for this member & book if one exists
            const string fulfillResSql = """
                UPDATE dbo.Reservations
                SET ReservationStatus = N'Fulfilled',
                    FulfilledDate = @Now
                WHERE MemberId = @MemberId
                  AND BookId = @BookId
                  AND ReservationStatus = N'Pending';
                """;
            await using (var resCmd = new SqlCommand(fulfillResSql, conn, tx))
            {
                resCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = request.MemberId;
                resCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = bookId;
                resCmd.Parameters.Add("@Now", SqlDbType.DateTime2).Value = issueDate;
                await resCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (await GetDetailByIdAsync(loanId, ct))!.Loan;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct);
            throw new BusinessRuleException("This physical copy already has an active loan.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Loan> ReturnBookAsync(ReturnBookRequest request, CancellationToken ct = default)
    {
        if (!request.LoanId.HasValue && string.IsNullOrWhiteSpace(request.AccessionNumber))
        {
            throw new BusinessRuleException("Provide either a LoanId or an AccessionNumber to return a book.");
        }

        var condition = AllowedReturnConditions.FirstOrDefault(
            c => c.Equals(request.ReturnCondition?.Trim() ?? "Available", StringComparison.OrdinalIgnoreCase));
        if (condition is null)
        {
            throw new BusinessRuleException("ReturnCondition must be 'Available', 'Damaged', or 'Lost'.");
        }

        var returnDate = request.ReturnDate ?? DateTime.UtcNow;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string findLoanSql = """
                SELECT l.LoanId, l.CopyId, l.IssueDate, l.DueDate, l.ReturnDate
                FROM dbo.Loans l
                INNER JOIN dbo.BookCopies bc ON bc.CopyId = l.CopyId
                WHERE (@LoanId IS NOT NULL AND l.LoanId = @LoanId)
                   OR (@LoanId IS NULL AND bc.AccessionNumber = @AccessionNumber AND l.ReturnDate IS NULL);
                """;

            int loanId;
            int copyId;
            DateTime issueDate;
            DateTime dueDate;
            DateTime? existingReturnDate;

            await using (var findCmd = new SqlCommand(findLoanSql, conn, tx))
            {
                findCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(request.LoanId);
                findCmd.Parameters.Add("@AccessionNumber", SqlDbType.NVarChar, 50).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.AccessionNumber) ? null : request.AccessionNumber.Trim().ToUpperInvariant());

                await using var reader = await findCmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new ResourceNotFoundException("No matching active loan was found.");
                }

                loanId = reader.GetInt32(0);
                copyId = reader.GetInt32(1);
                issueDate = reader.GetDateTime(2);
                dueDate = reader.GetDateTime(3);
                existingReturnDate = AdoHelper.GetNullableDateTime(reader, 4);
            }

            if (existingReturnDate.HasValue)
            {
                throw new BusinessRuleException($"Loan #{loanId} has already been returned on {existingReturnDate.Value:yyyy-MM-dd}.");
            }

            if (returnDate.Date < issueDate.Date)
            {
                throw new BusinessRuleException("Return date cannot be earlier than the issue date.");
            }

            var overdueDays = Math.Max(0, (returnDate.Date - dueDate.Date).Days);
            var fineAmount = Math.Round(overdueDays * _policy.DailyFineRate, 2);

            const string updateLoanSql = """
                UPDATE dbo.Loans
                SET ReturnDate = @ReturnDate,
                    LoanStatus = N'Returned',
                    FineAmount = @FineAmount,
                    Notes = COALESCE(@Notes, Notes)
                WHERE LoanId = @LoanId
                  AND ReturnDate IS NULL;
                """;

            await using (var updLoanCmd = new SqlCommand(updateLoanSql, conn, tx))
            {
                updLoanCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
                updLoanCmd.Parameters.Add("@ReturnDate", SqlDbType.DateTime2).Value = returnDate;
                var fineParam = updLoanCmd.Parameters.Add("@FineAmount", SqlDbType.Decimal);
                fineParam.Precision = 18;
                fineParam.Scale = 2;
                fineParam.Value = fineAmount;
                updLoanCmd.Parameters.Add("@Notes", SqlDbType.NVarChar, 500).Value =
                    AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

                if (await updLoanCmd.ExecuteNonQueryAsync(ct) == 0)
                {
                    throw new BusinessRuleException($"Loan #{loanId} was already returned.");
                }
            }

            const string updateCopySql = """
                UPDATE dbo.BookCopies
                SET CopyStatus = @CopyStatus
                WHERE CopyId = @CopyId;
                """;

            await using (var updCopyCmd = new SqlCommand(updateCopySql, conn, tx))
            {
                updCopyCmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = copyId;
                updCopyCmd.Parameters.Add("@CopyStatus", SqlDbType.NVarChar, 20).Value = condition;
                await updCopyCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (await GetDetailByIdAsync(loanId, ct))!.Loan;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<LoanDetailResponse> RenewLoanAsync(int loanId, RenewLoanRequest request, CancellationToken ct = default)
    {
        var extensionDays = request.ExtensionDays ?? _policy.DefaultRenewalDays;
        if (extensionDays <= 0)
        {
            throw new BusinessRuleException("Extension days must be greater than zero.");
        }

        var now = DateTime.UtcNow;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            const string fetchSql = """
                SELECT
                    l.DueDate,
                    l.ReturnDate,
                    l.RenewalCount,
                    m.MembershipStatus,
                    m.MembershipExpiryDate
                FROM dbo.Loans l
                INNER JOIN dbo.Members m ON m.MemberId = l.MemberId
                WHERE l.LoanId = @LoanId;
                """;

            DateTime currentDueDate;
            await using (var fetchCmd = new SqlCommand(fetchSql, conn, tx))
            {
                fetchCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
                await using var reader = await fetchCmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                {
                    throw new ResourceNotFoundException($"Loan #{loanId} was not found.");
                }

                currentDueDate = reader.GetDateTime(0);
                var returnDate = AdoHelper.GetNullableDateTime(reader, 1);
                var renewalCount = reader.GetInt32(2);
                var memberStatus = reader.GetString(3);
                var memberExpiry = reader.GetDateTime(4);

                if (returnDate.HasValue)
                {
                    throw new BusinessRuleException("Returned loans cannot be renewed.");
                }

                if (currentDueDate.Date < now.Date)
                {
                    throw new BusinessRuleException("Overdue loans cannot be renewed. Please return the book first.");
                }

                if (renewalCount >= _policy.MaxRenewals)
                {
                    throw new BusinessRuleException($"This loan has reached the maximum renewal limit ({_policy.MaxRenewals}).");
                }

                if (!memberStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) || memberExpiry.Date < now.Date)
                {
                    throw new BusinessRuleException("Cannot renew loan because the member's membership is inactive or expired.");
                }
            }

            var newDueDate = currentDueDate.AddDays(extensionDays);

            const string updateSql = """
                UPDATE dbo.Loans
                SET DueDate = @NewDueDate,
                    RenewalCount = RenewalCount + 1
                WHERE LoanId = @LoanId;
                """;

            await using (var updCmd = new SqlCommand(updateSql, conn, tx))
            {
                updCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
                updCmd.Parameters.Add("@NewDueDate", SqlDbType.DateTime2).Value = newDueDate;
                await updCmd.ExecuteNonQueryAsync(ct);
            }

            const string insertRenewalSql = """
                INSERT INTO dbo.LoanRenewals (LoanId, PreviousDueDate, NewDueDate, RenewalDate)
                VALUES (@LoanId, @PreviousDueDate, @NewDueDate, @RenewalDate);
                """;

            await using (var renCmd = new SqlCommand(insertRenewalSql, conn, tx))
            {
                renCmd.Parameters.Add("@LoanId", SqlDbType.Int).Value = loanId;
                renCmd.Parameters.Add("@PreviousDueDate", SqlDbType.DateTime2).Value = currentDueDate;
                renCmd.Parameters.Add("@NewDueDate", SqlDbType.DateTime2).Value = newDueDate;
                renCmd.Parameters.Add("@RenewalDate", SqlDbType.DateTime2).Value = now;
                await renCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (await GetDetailByIdAsync(loanId, ct))!;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static void BindQueryParams(SqlCommand cmd, LoanQueryRequest query)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var status = string.IsNullOrWhiteSpace(query.LoanStatus) ? null : query.LoanStatus.Trim();
        cmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.MemberId);
        cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.BookId);
        cmd.Parameters.Add("@CopyId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(query.CopyId);
        cmd.Parameters.Add("@LoanStatus", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(status);
        cmd.Parameters.Add("@OverdueOnly", SqlDbType.Bit).Value = query.OverdueOnly == true ? 1 : 0;
        cmd.Parameters.Add("@StartDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(query.StartDate?.Date);
        cmd.Parameters.Add("@EndDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(query.EndDate?.Date);
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 300).Value = AdoHelper.DbNullIfNull(search);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 302).Value = search is null ? DBNull.Value : $"%{search}%";
    }

    internal static Loan MapLoan(SqlDataReader reader) => new()
    {
        LoanId = reader.GetInt32(0),
        MemberId = reader.GetInt32(1),
        MembershipNumber = reader.GetString(2),
        MemberName = reader.GetString(3),
        CopyId = reader.GetInt32(4),
        AccessionNumber = reader.GetString(5),
        BookId = reader.GetInt32(6),
        BookTitle = reader.GetString(7),
        ISBN = reader.GetString(8),
        IssueDate = reader.GetDateTime(9),
        DueDate = reader.GetDateTime(10),
        ReturnDate = AdoHelper.GetNullableDateTime(reader, 11),
        LoanStatus = reader.GetString(12),
        FineAmount = reader.GetDecimal(13),
        RenewalCount = reader.GetInt32(14),
        Notes = AdoHelper.GetNullableString(reader, 15),
        CreatedAt = reader.GetDateTime(16)
    };
}
