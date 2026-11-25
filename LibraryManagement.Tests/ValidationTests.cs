using System.ComponentModel.DataAnnotations;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;
using Xunit;

namespace LibraryManagement.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void Loan_OverdueDays_ComputesZeroWhenNotOverdue()
    {
        var onTime = new Loan
        {
            IssueDate = DateTime.UtcNow.AddDays(-5),
            DueDate = DateTime.UtcNow.AddDays(5),
            ReturnDate = null
        };

        Assert.False(onTime.IsOverdue);
        Assert.Equal(0, onTime.OverdueDays);
    }

    [Fact]
    public void CreateBookRequest_RejectsInvalidIsbnAndEmptyAuthors()
    {
        var req = new CreateBookRequest
        {
            Title = "Test Book",
            ISBN = "BAD-ISBN",
            CategoryId = 1,
            PublicationYear = 2024,
            AuthorIds = new List<int>()
        };

        var results = Validate(req);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateBookRequest.ISBN)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateBookRequest.AuthorIds)));
    }

    [Fact]
    public void CreateMemberRequest_RejectsBadEmail()
    {
        var req = new CreateMemberRequest
        {
            MembershipNumber = "MEM-999",
            FullName = "Test User",
            Email = "bad-email",
            Phone = "9999999999"
        };

        var results = Validate(req);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateMemberRequest.Email)));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model);
        var list = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, list, validateAllProperties: true);
        return list;
    }
}
