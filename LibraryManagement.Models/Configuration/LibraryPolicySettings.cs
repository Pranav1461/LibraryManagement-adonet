namespace LibraryManagement.Models.Configuration;

public sealed class LibraryPolicySettings
{
    public const string SectionName = "LibraryPolicy";

    public int DefaultBorrowingDays { get; set; } = 14;
    public int MaxActiveLoansPerMember { get; set; } = 3;
    public decimal DailyFineRate { get; set; } = 5.00m;
    public int MaxRenewals { get; set; } = 2;
    public int DefaultRenewalDays { get; set; } = 7;
}
