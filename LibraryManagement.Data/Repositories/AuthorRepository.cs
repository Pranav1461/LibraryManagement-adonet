using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;

namespace LibraryManagement.Data.Repositories;

// Connected ADO.NET repository for authors and their books.
public sealed class AuthorRepository : IAuthorRepository
{
    private readonly IDbConnectionFactory _factory;

    public AuthorRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<List<Author>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                a.AuthorId,
                a.AuthorName,
                a.Biography,
                a.CreatedAt,
                COUNT(ba.BookId) AS BookCount
            FROM dbo.Authors a
            LEFT JOIN dbo.BookAuthors ba ON ba.AuthorId = a.AuthorId
            WHERE (@Search IS NULL OR a.AuthorName LIKE @Pattern OR a.Biography LIKE @Pattern)
            GROUP BY a.AuthorId, a.AuthorName, a.Biography, a.CreatedAt
            ORDER BY a.AuthorName ASC;
            """;

        var list = new List<Author>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = AdoHelper.DbNullIfNull(trimmed);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 202).Value = trimmed is null ? DBNull.Value : $"%{trimmed}%";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapAuthor(reader));
        }

        return list;
    }

    public async Task<Author?> GetByIdAsync(int authorId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                a.AuthorId,
                a.AuthorName,
                a.Biography,
                a.CreatedAt,
                COUNT(ba.BookId) AS BookCount
            FROM dbo.Authors a
            LEFT JOIN dbo.BookAuthors ba ON ba.AuthorId = a.AuthorId
            WHERE a.AuthorId = @AuthorId
            GROUP BY a.AuthorId, a.AuthorName, a.Biography, a.CreatedAt;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapAuthor(reader) : null;
    }

    public async Task<List<Book>> GetBooksByAuthorAsync(int authorId, CancellationToken ct = default)
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
            FROM dbo.BookAuthors ba
            INNER JOIN dbo.Books b ON b.BookId = ba.BookId
            INNER JOIN dbo.Categories c ON c.CategoryId = b.CategoryId
            WHERE ba.AuthorId = @AuthorId
            ORDER BY b.Title ASC;
            """;

        var list = new List<Book>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;

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

    public async Task<Author> CreateAsync(CreateAuthorRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Authors (AuthorName, Biography)
            OUTPUT INSERTED.AuthorId
            VALUES (@AuthorName, @Biography);
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@AuthorName", SqlDbType.NVarChar, 200).Value = request.AuthorName.Trim();
        cmd.Parameters.Add("@Biography", SqlDbType.NVarChar, -1).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Biography) ? null : request.Biography.Trim());

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<Author> UpdateAsync(int authorId, UpdateAuthorRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Authors
            SET AuthorName = @AuthorName,
                Biography = @Biography
            WHERE AuthorId = @AuthorId;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;
        cmd.Parameters.Add("@AuthorName", SqlDbType.NVarChar, 200).Value = request.AuthorName.Trim();
        cmd.Parameters.Add("@Biography", SqlDbType.NVarChar, -1).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Biography) ? null : request.Biography.Trim());

        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
        {
            throw new ResourceNotFoundException($"Author #{authorId} was not found.");
        }

        return (await GetByIdAsync(authorId, ct))!;
    }

    public async Task DeleteAsync(int authorId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Authors WHERE AuthorId = @AuthorId) THEN 1 ELSE 0 END,
                (SELECT COUNT(1) FROM dbo.BookAuthors WHERE AuthorId = @AuthorId);
            """;

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt32(0) == 0)
                {
                    throw new ResourceNotFoundException($"Author #{authorId} was not found.");
                }

                var linked = reader.GetInt32(1);
                if (linked > 0)
                {
                    throw new ReferentialIntegrityException(
                        $"Cannot delete author #{authorId} because they are linked to {linked} book(s).");
                }
            }
        }

        const string delSql = "DELETE FROM dbo.Authors WHERE AuthorId = @AuthorId;";
        await using var delCmd = new SqlCommand(delSql, conn);
        delCmd.Parameters.Add("@AuthorId", SqlDbType.Int).Value = authorId;
        await delCmd.ExecuteNonQueryAsync(ct);
    }

    private static Author MapAuthor(SqlDataReader reader) => new()
    {
        AuthorId = reader.GetInt32(0),
        AuthorName = reader.GetString(1),
        Biography = AdoHelper.GetNullableString(reader, 2),
        CreatedAt = reader.GetDateTime(3),
        BookCount = reader.GetInt32(4)
    };
}
