namespace LibraryManagement.API.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "CacheSettings";

    public bool Enabled { get; set; } = true;
    public int DashboardExpirationSeconds { get; set; } = 60;
    public int LookupExpirationSeconds { get; set; } = 300;
    public int BooksExpirationSeconds { get; set; } = 120;
    public int ReportsExpirationSeconds { get; set; } = 90;
    public int SlidingExpirationSeconds { get; set; } = 45;
}
