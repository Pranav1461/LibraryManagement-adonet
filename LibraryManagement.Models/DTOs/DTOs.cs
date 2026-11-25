namespace LibraryManagement.Models.DTOs
{
    public class BookDTO
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string PublisherName { get; set; } = string.Empty;
        public int AvailableCopies { get; set; }
    }

    public class LoanRequest
    {
        public int MemberId { get; set; }
        public int CopyId { get; set; }
        public int DurationDays { get; set; } = 14;
    }
}
