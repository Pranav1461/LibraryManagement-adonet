namespace LibraryManagement.Models.Entities;

public class Category
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int BookCount { get; set; }
}

public class Publisher
{
    public int PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public int BookCount { get; set; }
}

public class Author
{
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? Biography { get; set; }
    public DateTime CreatedAt { get; set; }
    public int BookCount { get; set; }
}

public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public int PublicationYear { get; set; }
    public string? Edition { get; set; }
    public string Language { get; set; } = "English";
    public string? Description { get; set; }
    public string? ShelfLocation { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<AuthorSummary> Authors { get; set; } = new();
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
}

public class AuthorSummary
{
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
}

public class BookCopy
{
    public int CopyId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public string? ShelfLocation { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public string CopyStatus { get; set; } = "Available";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Member
{
    public int MemberId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime RegistrationDate { get; set; }
    public DateTime MembershipExpiryDate { get; set; }
    public string MembershipStatus { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ActiveLoanCount { get; set; }
    public int OverdueLoanCount { get; set; }
}

public class Loan
{
    public int LoanId { get; set; }
    public int MemberId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public int CopyId { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string LoanStatus { get; set; } = "Issued";
    public decimal FineAmount { get; set; }
    public int RenewalCount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOverdue => ReturnDate is null && DueDate.Date < DateTime.UtcNow.Date;
    public int OverdueDays
    {
        get
        {
            var compareDate = (ReturnDate ?? DateTime.UtcNow).Date;
            var diff = (compareDate - DueDate.Date).Days;
            return diff > 0 ? diff : 0;
        }
    }
}

public class LoanRenewal
{
    public int RenewalId { get; set; }
    public int LoanId { get; set; }
    public DateTime PreviousDueDate { get; set; }
    public DateTime NewDueDate { get; set; }
    public DateTime RenewalDate { get; set; }
}

public class Reservation
{
    public int ReservationId { get; set; }
    public int MemberId { get; set; }
    public string MembershipNumber { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public string ReservationStatus { get; set; } = "Pending";
    public DateTime? FulfilledDate { get; set; }
}
