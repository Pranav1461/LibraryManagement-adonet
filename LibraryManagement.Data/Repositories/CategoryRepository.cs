using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;

namespace LibraryManagement.Data.Repositories;

// Connected ADO.NET repository for library categories.
public sealed class CategoryRepository : ICategoryRepository
{
    private readonly IDbConnectionFactory _factory;

    public CategoryRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<List<Category>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                c.CategoryId,
                c.CategoryName,
                c.Description,
                c.CreatedAt,
                COUNT(b.BookId) AS BookCount
            FROM dbo.Categories c
            LEFT JOIN dbo.Books b ON b.CategoryId = c.CategoryId AND b.IsActive = 1
            WHERE (@Search IS NULL OR c.CategoryName LIKE @Pattern OR c.Description LIKE @Pattern)
            GROUP BY c.CategoryId, c.CategoryName, c.Description, c.CreatedAt
            ORDER BY c.CategoryName ASC;
            """;

        var list = new List<Category>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        var trimmed = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        cmd.Parameters.Add("@Search", SqlDbType.NVarChar, 100).Value = AdoHelper.DbNullIfNull(trimmed);
        cmd.Parameters.Add("@Pattern", SqlDbType.NVarChar, 102).Value = trimmed is null ? DBNull.Value : $"%{trimmed}%";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapCategory(reader));
        }

        return list;
    }

    public async Task<Category?> GetByIdAsync(int categoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                c.CategoryId,
                c.CategoryName,
                c.Description,
                c.CreatedAt,
                COUNT(b.BookId) AS BookCount
            FROM dbo.Categories c
            LEFT JOIN dbo.Books b ON b.CategoryId = c.CategoryId AND b.IsActive = 1
            WHERE c.CategoryId = @CategoryId
            GROUP BY c.CategoryId, c.CategoryName, c.Description, c.CreatedAt;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapCategory(reader) : null;
    }

    public async Task<Category> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.Categories (CategoryName, Description)
            OUTPUT INSERTED.CategoryId
            VALUES (@CategoryName, @Description);
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 100).Value = request.CategoryName.Trim();
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());

        try
        {
            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            return (await GetByIdAsync(id, ct))!;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Category '{request.CategoryName.Trim()}' already exists.");
        }
    }

    public async Task<Category> UpdateAsync(int categoryId, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.Categories
            SET CategoryName = @CategoryName,
                Description = @Description
            WHERE CategoryId = @CategoryId;
            """;

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
        cmd.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 100).Value = request.CategoryName.Trim();
        cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
            AdoHelper.DbNullIfNull(string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim());

        try
        {
            if (await cmd.ExecuteNonQueryAsync(ct) == 0)
            {
                throw new ResourceNotFoundException($"Category #{categoryId} was not found.");
            }
            return (await GetByIdAsync(categoryId, ct))!;
        }
        catch (SqlException ex) when (AdoHelper.IsUniqueViolation(ex))
        {
            throw new DuplicateResourceException($"Category '{request.CategoryName.Trim()}' already exists.");
        }
    }

    public async Task DeleteAsync(int categoryId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string checkSql = """
            SELECT
                CASE WHEN EXISTS (SELECT 1 FROM dbo.Categories WHERE CategoryId = @CategoryId) THEN 1 ELSE 0 END,
                (SELECT COUNT(1) FROM dbo.Books WHERE CategoryId = @CategoryId);
            """;

        await using (var checkCmd = new SqlCommand(checkSql, conn))
        {
            checkCmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
            await using var reader = await checkCmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetInt32(0) == 0)
                {
                    throw new ResourceNotFoundException($"Category #{categoryId} was not found.");
                }

                var assignedBooks = reader.GetInt32(1);
                if (assignedBooks > 0)
                {
                    throw new ReferentialIntegrityException(
                        $"Cannot delete category #{categoryId} because {assignedBooks} book(s) are assigned to it.");
                }
            }
        }

        const string delSql = "DELETE FROM dbo.Categories WHERE CategoryId = @CategoryId;";
        await using var delCmd = new SqlCommand(delSql, conn);
        delCmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
        await delCmd.ExecuteNonQueryAsync(ct);
    }

    private static Category MapCategory(SqlDataReader reader) => new()
    {
        CategoryId = reader.GetInt32(0),
        CategoryName = reader.GetString(1),
        Description = AdoHelper.GetNullableString(reader, 2),
        CreatedAt = reader.GetDateTime(3),
        BookCount = reader.GetInt32(4)
    };
}
