using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;

namespace LibraryManagement.Data.Repositories;

// Connected ADO.NET repository for publishers and their catalog books.
public sealed class PublisherRepository : IPublisherRepository
{
    private readonly IDbConnectionFactory _factory;

    public PublisherRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<List<Publisher>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                p.PublisherId,
                p.PublisherName,
                p.ContactEmail,
                p.Phone,
                p.Address,
                p.CreatedAt,
                COUNT(b.BookId) AS BookCount
            FROM dbo.Publishers p
            LEFT JOIN dbo.Books b ON b.PublisherId = p.PublisherId AND b.IsActive = 1
            WHERE (@Search IS NULL OR p.PublisherName LIKE @Pattern OR p.ContactEmail LIKE @Pattern)
            GROUP BY p.PublisherId, p.PublisherName, p.ContactEmail, p.Phone, p.Address, p.CreatedAt
            ORDER BY p.PublisherName ASC;
            """;

        var list = new List<Publisher>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = AdoHelper.DbNullIfNull(trimmed);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 202).Value = trimmed is null ? DBNull.Value : $"%{trimmed}%";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapPublisher(reader));
        }

        return list;
    }

    public async Task<Publisher?> GetByIdAsync(int publisherId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                p.PublisherId,
                p.PublisherName,
                p.ContactEmail,
                p.Phone,
                p.Address,
                p.CreatedAt,
                COUNT(b.BookId) AS BookCount
            FROM dbo.Publishers p
            LEFT JOIN dbo.Books b ON b.PublisherId = p.PublisherId AND b.IsActive = 1
            WHERE p.PublisherId = @PublisherId
            GROUP BY p.PublisherId, p.PublisherName, p.ContactEmail, p.Phone, p.Address, p.CreatedAt;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = publisherId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPublisher(reader) : null;
    }

    public async Task<List<Book>> GetBooksByPublisherAsync(int publisherId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                b.BookId,
                b.Title,
                b.ISBN,
                b.CategoryId,
                c.CategoryName,
                b.PublicationYear,
                b.Edition,
                b.Language,
                b.IsActive,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId) AS TotalCopies,
                (SELECT COUNT(1) FROM dbo.BookCopies bc WHERE bc.BookId = b.BookId AND bc.CopyStatus = N'Available') AS AvailableCopies
            FROM dbo.Books b
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            WHERE b.PublisherId = @PublisherId
            ORDER BY b.Title ASC;
            """;

        var list = new List<Book>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = publisherId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Book
            {
                BookId = reader.GetInt32(0),
                Title = reader.GetString(1),
                ISBN = reader.GetString(2),
                CategoryId = reader.GetInt32(3),
                CategoryName = reader.GetString(4),
                PublisherId = publisherId,
                PublicationYear = reader.GetInt32(5),
                Edition = AdoHelper.GetNullableString(reader, 6),
                Language = reader.GetString(7),
                IsActive = reader.GetBoolean(8),
                TotalCopies = reader.GetInt32(9),
                AvailableCopies = reader.GetInt32(10)
            });
        }

        return list;
    }

    public async Task<Publisher> CreateAsync(CreatePublisherRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Publishers (PublisherName, ContactEmail, Phone, Address)
            OUTPUT INSERTED.PublisherId
            VALUES (@PublisherName, @ContactEmail, @Phone, @Address);
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        BindParameters(cmd, request);

        try
        {
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return (await GetByIdAsync(id, ct))!;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Publisher '{request.PublisherName.Trim()}' already exists.");
        }
    }

    public async Task<Publisher> UpdateAsync(int publisherId, UpdatePublisherRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Publishers
            SET PublisherName = @PublisherName,
                ContactEmail = @ContactEmail,
                Phone = @Phone,
                Address = @Address
            WHERE PublisherId = @PublisherId;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = publisherId;
        BindParameters(cmd, request);

        try
        {
            if (await cmd.ExecuteNonQueryAsync(ct) == 0)
            {
                throw new ResourceNotFoundException($"Publisher #{publisherId} was not found.");
            }
            return (await GetByIdAsync(publisherId, ct))!;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Publisher '{request.PublisherName.Trim()}' already exists.");
        }
    }

    public async Task DeleteAsync(int publisherId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Publishers WHERE PublisherId = @PublisherId) THEN 1 ELSE 0 END,
                (SELECT COUNT(1) FROM dbo.Books WHERE PublisherId = @PublisherId);
            """;

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = publisherId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt32(0) == 0)
                {
                    throw new ResourceNotFoundException($"Publisher #{publisherId} was not found.");
                }

                var bookCount = reader.GetInt32(1);
                if (bookCount > 0)
                {
                    throw new ReferentialIntegrityException(
                        $"Cannot delete publisher #{publisherId} because it is referenced by {bookCount} book(s).");
                }
            }
        }

        const string delSql = "DELETE FROM dbo.Publishers WHERE PublisherId = @PublisherId;";
        await using var delCmd = new SqlCommand(delSql, conn);
        delCmd.Parameters.Add("@PublisherId", SqlDbType.Int).Value = publisherId;
        await delCmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindParameters(SqlCommand cmd, CreatePublisherRequest request)
    {
        cmd.Parameters.Add("@PublisherName", SqlDbType.NVarChar, 200).Value = request.PublisherName.Trim();
        cmd.Parameters.Add("@ContactEmail", SqlDbType.NVarChar, 150).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim());
        cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());
        cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim());
    }

    private static Publisher MapPublisher(SqlDataReader reader) => new()
    {
        PublisherId = reader.GetInt32(0),
        PublisherName = reader.GetString(1),
        ContactEmail = AdoHelper.GetNullableString(reader, 2),
        Phone = AdoHelper.GetNullableString(reader, 3),
        Address = AdoHelper.GetNullableString(reader, 4),
        CreatedAt = reader.GetDateTime(5),
        BookCount = reader.GetInt32(6)
    };
}
