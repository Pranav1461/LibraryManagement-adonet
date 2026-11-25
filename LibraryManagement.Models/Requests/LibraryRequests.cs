using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Models.Requests;

public class BookQueryRequest
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? PublisherId { get; set; }
    public int? AuthorId { get; set; }
    public bool? IncludeInactive { get; set; } = false;
    public string SortBy { get; set; } = "Title";
    public string SortDirection { get; set; } = "ASC";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CreateBookRequest
{
    [Required(ErrorMessage = "Book title is required.")]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "ISBN is required.")]
    [RegularExpression(@"^(?:\d{9}[\dXx]|\d{13}|[\d-]{10,17})$", ErrorMessage = "ISBN must be a valid 10- or 13-digit ISBN.")]
    public string ISBN { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Category is required.")]
    public int CategoryId { get; set; }

    public int? PublisherId { get; set; }

    [Range(1450, 2100, ErrorMessage = "Publication year must be between 1450 and 2100.")]
    public int PublicationYear { get; set; } = DateTime.UtcNow.Year;

    [StringLength(50)]
    public string? Edition { get; set; }

    [Required]
    [StringLength(50)]
    public string Language { get; set; } = "English";

    public string? Description { get; set; }

    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    [Required(ErrorMessage = "At least one author is required.")]
    [MinLength(1, ErrorMessage = "Select at least one author.")]
    public List<int> AuthorIds { get; set; } = new();

    [Range(0, 50)]
    public int InitialCopies { get; set; } = 0;

    [StringLength(30)]
    public string? AccessionPrefix { get; set; }
}

public class UpdateBookRequest
{
    [Required(ErrorMessage = "Book title is required.")]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "ISBN is required.")]
    [RegularExpression(@"^(?:\d{9}[\dXx]|\d{13}|[\d-]{10,17})$", ErrorMessage = "ISBN must be a valid 10- or 13-digit ISBN.")]
    public string ISBN { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Category is required.")]
    public int CategoryId { get; set; }

    public int? PublisherId { get; set; }

    [Range(1450, 2100, ErrorMessage = "Publication year must be between 1450 and 2100.")]
    public int PublicationYear { get; set; }

    [StringLength(50)]
    public string? Edition { get; set; }

    [Required]
    [StringLength(50)]
    public string Language { get; set; } = "English";

    public string? Description { get; set; }

    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "At least one author is required.")]
    [MinLength(1, ErrorMessage = "Select at least one author.")]
    public List<int> AuthorIds { get; set; } = new();
}

public class CreateAuthorRequest
{
    [Required(ErrorMessage = "Author name is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string AuthorName { get; set; } = string.Empty;

    public string? Biography { get; set; }
}

public class UpdateAuthorRequest : CreateAuthorRequest { }

public class CreateCategoryRequest
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }
}

public class UpdateCategoryRequest : CreateCategoryRequest { }

public class CreatePublisherRequest
{
    [Required(ErrorMessage = "Publisher name is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string PublisherName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(150)]
    public string? ContactEmail { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

public class UpdatePublisherRequest : CreatePublisherRequest { }

public class BookCopyQueryRequest
{
    public string? Search { get; set; }
    public int? BookId { get; set; }
    public string? CopyStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

public class CreateBookCopyRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Book is required.")]
    public int BookId { get; set; }

    [Required(ErrorMessage = "Accession number is required.")]
    [StringLength(50, MinimumLength = 2)]
    public string AccessionNumber { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    public DateTime? AcquisitionDate { get; set; }

    [StringLength(20)]
    public string CopyStatus { get; set; } = "Available";

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class BulkCreateBookCopiesRequest
{
    [Range(1, int.MaxValue)]
    public int BookId { get; set; }

    [Required]
    [MinLength(1)]
    public List<CreateBookCopyRequest> Copies { get; set; } = new();
}

public class UpdateBookCopyRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string AccessionNumber { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ShelfLocation { get; set; }

    public DateTime? AcquisitionDate { get; set; }

    [Required]
    public string CopyStatus { get; set; } = "Available";

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class MemberQueryRequest
{
    public string? Search { get; set; }
    public string? MembershipStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CreateMemberRequest
{
    [Required(ErrorMessage = "Membership number is required.")]
    [StringLength(50, MinimumLength = 2)]
    public string MembershipNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [StringLength(30, MinimumLength = 5)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }

    public DateTime? MembershipExpiryDate { get; set; }
    public string MembershipStatus { get; set; } = "Active";
}

public class UpdateMemberRequest : CreateMemberRequest { }

public class LoanQueryRequest
{
    public string? Search { get; set; }
    public int? MemberId { get; set; }
    public int? BookId { get; set; }
    public int? CopyId { get; set; }
    public string? LoanStatus { get; set; }
    public bool? OverdueOnly { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

public class IssueBookRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Member is required.")]
    public int MemberId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Physical copy is required.")]
    public int CopyId { get; set; }

    public DateTime? IssueDate { get; set; }

    [Range(1, 90, ErrorMessage = "Borrowing period must be between 1 and 90 days.")]
    public int? DurationDays { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class ReturnBookRequest
{
    public int? LoanId { get; set; }
    public string? AccessionNumber { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string ReturnCondition { get; set; } = "Available";
    [StringLength(500)]
    public string? Notes { get; set; }
}

public class RenewLoanRequest
{
    [Range(1, 30, ErrorMessage = "Renewal days must be between 1 and 30.")]
    public int? ExtensionDays { get; set; }
}

public class CreateReservationRequest
{
    [Range(1, int.MaxValue)]
    public int MemberId { get; set; }

    [Range(1, int.MaxValue)]
    public int BookId { get; set; }
}

public class ReportFilterRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? BookId { get; set; }
    public int? CategoryId { get; set; }
    public int? AuthorId { get; set; }
    public int? MemberId { get; set; }
    public string? CopyStatus { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "DESC";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
