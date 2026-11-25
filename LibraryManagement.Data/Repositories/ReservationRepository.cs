using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Data.Interfaces;
using LibraryManagement.Models.Entities;
using LibraryManagement.Models.Requests;

namespace LibraryManagement.Data.Repositories;

// Connected ADO.NET repository for optional book reservation queue entries.
public sealed class ReservationRepository : IReservationRepository
{
    private readonly IDbConnectionFactory _factory;

    public ReservationRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<List<Reservation>> GetAllAsync(
        string? status = null,
        int? memberId = null,
        int? bookId = null,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                r.ReservationId,
                r.MemberId,
                m.MembershipNumber,
                m.FullName,
                r.BookId,
                b.Title,
                b.ISBN,
                r.ReservationDate,
                r.ReservationStatus,
                r.FulfilledDate
            FROM dbo.Reservations r
            INNER JOIN dbo.Members m ON m.MemberId = r.MemberId
            INNER JOIN dbo.Books b ON b.BookId = r.BookId
            WHERE (@Status IS NULL OR r.ReservationStatus = @Status)
              AND (@MemberId IS NULL OR r.MemberId = @MemberId)
              AND (@BookId IS NULL OR r.BookId = @BookId)
            ORDER BY
                CASE WHEN r.ReservationStatus = N'Pending' THEN 0 ELSE 1 END ASC,
                r.ReservationDate ASC;
            """;

        var list = new List<Reservation>();
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = AdoHelper.DbNullIfNull(normalized);
        cmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(memberId);
        cmd.Parameters.Add("@BookId", SqlDbType.Int).Value = AdoHelper.DbNullIfNull(bookId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapReservation(reader));
        }

        return list;
    }

    public async Task<Reservation> CreateAsync(CreateReservationRequest request, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string validateSql = """
            SELECT
                (SELECT MembershipStatus FROM dbo.Members WHERE MemberId = @MemberId) AS MemberStatus,
                (SELECT IsActive FROM dbo.Books WHERE BookId = @BookId) AS BookActive,
                (SELECT COUNT(1) FROM dbo.Reservations WHERE MemberId = @MemberId AND BookId = @BookId AND ReservationStatus = N'Pending') AS ExistingPending;
            """;

        await using (var valCmd = new SqlCommand(validateSql, conn))
        {
            valCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = request.MemberId;
            valCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = request.BookId;

            await using var reader = await valCmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            if (reader.IsDBNull(0)) throw new ResourceNotFoundException($"Member #{request.MemberId} was not found.");
            if (reader.IsDBNull(1)) throw new ResourceNotFoundException($"Book #{request.BookId} was not found.");
            if (!reader.GetString(0).Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("Only active members can reserve books.");
            }
            if (!reader.GetBoolean(1))
            {
                throw new BusinessRuleException("Inactive books cannot be reserved.");
            }
            if (reader.GetInt32(2) > 0)
            {
                throw new DuplicateResourceException("This member already has a pending reservation for the selected book.");
            }
        }

        const string insertSql = """
            INSERT INTO dbo.Reservations (MemberId, BookId, ReservationStatus)
            OUTPUT INSERTED.ReservationId
            VALUES (@MemberId, @BookId, N'Pending');
            """;

        await using var insCmd = new SqlCommand(insertSql, conn);
        insCmd.Parameters.Add("@MemberId", SqlDbType.Int).Value = request.MemberId;
        insCmd.Parameters.Add("@BookId", SqlDbType.Int).Value = request.BookId;
        var id = Convert.ToInt32(await insCmd.ExecuteScalarAsync(ct));

        return (await GetByIdAsync(conn, id, ct))!;
    }

    public Task<Reservation> CancelAsync(int reservationId, CancellationToken ct = default) =>
        UpdateStatusAsync(reservationId, "Cancelled", null, ct);

    public Task<Reservation> FulfillAsync(int reservationId, CancellationToken ct = default) =>
        UpdateStatusAsync(reservationId, "Fulfilled", DateTime.UtcNow, ct);

    private async Task<Reservation> UpdateStatusAsync(
        int reservationId,
        string targetStatus,
        DateTime? fulfilledDate,
        CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);

        const string sql = """
            UPDATE dbo.Reservations
            SET ReservationStatus = @Status,
                FulfilledDate = @FulfilledDate
            WHERE ReservationId = @ReservationId
              AND ReservationStatus = N'Pending';
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@ReservationId", SqlDbType.Int).Value = reservationId;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = targetStatus;
        cmd.Parameters.Add("@FulfilledDate", SqlDbType.DateTime2).Value = AdoHelper.DbNullIfNull(fulfilledDate);

        if (await cmd.ExecuteNonQueryAsync(ct) == 0)
        {
            var existing = await GetByIdAsync(conn, reservationId, ct);
            if (existing is null) throw new ResourceNotFoundException($"Reservation #{reservationId} was not found.");
            throw new BusinessRuleException($"Reservation #{reservationId} is already '{existing.ReservationStatus}'.");
        }

        return (await GetByIdAsync(conn, reservationId, ct))!;
    }

    private static async Task<Reservation?> GetByIdAsync(SqlConnection conn, int id, CancellationToken ct)
    {
        const string sql = """
            SELECT
                r.ReservationId, r.MemberId, m.MembershipNumber, m.FullName,
                r.BookId, b.Title, b.ISBN, r.ReservationDate, r.ReservationStatus, r.FulfilledDate
            FROM dbo.Reservations r
            INNER JOIN dbo.Members m ON m.MemberId = r.MemberId
            INNER JOIN dbo.Books b ON b.BookId = r.BookId
            WHERE r.ReservationId = @Id;
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapReservation(reader) : null;
    }

    private static Reservation MapReservation(SqlDataReader reader) => new()
    {
        ReservationId = reader.GetInt32(0),
        MemberId = reader.GetInt32(1),
        MembershipNumber = reader.GetString(2),
        MemberName = reader.GetString(3),
        BookId = reader.GetInt32(4),
        BookTitle = reader.GetString(5),
        ISBN = reader.GetString(6),
        ReservationDate = reader.GetDateTime(7),
        ReservationStatus = reader.GetString(8),
        FulfilledDate = AdoHelper.GetNullableDateTime(reader, 9)
    };
}
