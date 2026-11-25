using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Repositories;
using LibraryManagement.Models.Configuration;
using LibraryManagement.Models.Requests;
using Xunit;

namespace LibraryManagement.Tests;

// Integration tests against LibraryManagementDB (enabled when LMS_RUN_DB_TESTS=true).
public sealed class SqlServerIntegrationTests
{
    private static bool ShouldRun =>
        string.Equals(Environment.GetEnvironmentVariable("LMS_RUN_DB_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    private static IDbConnectionFactory CreateFactory()
    {
        var connStr = Environment.GetEnvironmentVariable("LMS_TEST_CONNECTION")
            ?? "Server=.\\SQLEXPRESS;Database=LibraryManagementDB;Trusted_Connection=True;TrustServerCertificate=True;";

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connStr
            })
            .Build();

        return new SqlConnectionFactory(cfg);
    }

    private static IOptions<LibraryPolicySettings> CreatePolicy() =>
        Options.Create(new LibraryPolicySettings());

    [Fact]
    public async Task ExpiredMember_CannotBorrowBook()
    {
        if (!ShouldRun) return;

        var factory = CreateFactory();
        var loanRepo = new LoanRepository(factory, CreatePolicy());
        var memberRepo = new MemberRepository(factory);
        var copyRepo = new BookCopyRepository(factory);

        var expiredMembers = await memberRepo.GetPagedAsync(new MemberQueryRequest { MembershipStatus = "Expired", PageSize = 5 });
        var availCopies = await copyRepo.GetPagedAsync(new BookCopyQueryRequest { CopyStatus = "Available", PageSize = 5 });

        Assert.NotEmpty(expiredMembers.Items);
        Assert.NotEmpty(availCopies.Items);

        await Assert.ThrowsAsync<BusinessRuleException>(() => loanRepo.IssueBookAsync(new IssueBookRequest
        {
            MemberId = expiredMembers.Items[0].MemberId,
            CopyId = availCopies.Items[0].CopyId
        }));
    }

    [Fact]
    public async Task SimultaneousIssue_SameCopy_AllowsOnlyOne()
    {
        if (!ShouldRun) return;

        var factory = CreateFactory();
        var loanRepo = new LoanRepository(factory, CreatePolicy());
        var copyRepo = new BookCopyRepository(factory);
        var memberRepo = new MemberRepository(factory);

        var availCopies = await copyRepo.GetPagedAsync(new BookCopyQueryRequest { CopyStatus = "Available", PageSize = 10 });
        var activeMembers = await memberRepo.GetPagedAsync(new MemberQueryRequest { MembershipStatus = "Active", PageSize = 20 });

        var copy = availCopies.Items.Last();
        var eligibleMembers = activeMembers.Items.Where(m => m.ActiveLoanCount < 2).Take(2).ToList();
        Assert.Equal(2, eligibleMembers.Count);

        var t1 = TryIssueAsync(loanRepo, eligibleMembers[0].MemberId, copy.CopyId);
        var t2 = TryIssueAsync(loanRepo, eligibleMembers[1].MemberId, copy.CopyId);
        var results = await Task.WhenAll(t1, t2);

        var successes = results.Where(r => r.LoanId.HasValue).ToList();
        var failures = results.Where(r => !r.LoanId.HasValue).ToList();

        Assert.Single(successes);
        Assert.Single(failures);

        // Clean up by returning the test loan so sample data stays consistent
        await loanRepo.ReturnBookAsync(new ReturnBookRequest
        {
            LoanId = successes[0].LoanId!.Value,
            ReturnCondition = "Available"
        });
    }

    [Fact]
    public void DisconnectedReportRepository_ReturnsBookCatalogue()
    {
        if (!ShouldRun) return;

        var reportRepo = new ReportRepository(CreateFactory());
        var report = reportRepo.GetBookCatalogueReport(new ReportFilterRequest());

        Assert.NotEmpty(report.Rows);
        Assert.True(report.Summary.TotalTitles >= 25);
    }

    private static async Task<(int? LoanId, Exception? Error)> TryIssueAsync(LoanRepository repo, int memberId, int copyId)
    {
        try
        {
            var loan = await repo.IssueBookAsync(new IssueBookRequest { MemberId = memberId, CopyId = copyId });
            return (loan.LoanId, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }
}
