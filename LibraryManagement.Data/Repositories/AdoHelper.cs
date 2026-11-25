using System.Data;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Connection;

namespace LibraryManagement.Data.Repositories;

// Shared ADO.NET utilities for disconnected DataTable/DataSet fills and SQL error inspection.
internal static class AdoHelper
{
    public static DataTable FillDataTable(
        IDbConnectionFactory factory,
        string sql,
        Action<SqlParameterCollection>? configureParameters = null,
        string tableName = "Result")
    {
        var table = new DataTable(tableName);
        using var connection = factory.CreateConnection();
        using var command = new SqlCommand(sql, connection);
        configureParameters?.Invoke(command.Parameters);

        using var adapter = new SqlDataAdapter(command);
        adapter.Fill(table);
        return table;
    }

    public static DataSet FillDataSet(
        IDbConnectionFactory factory,
        string sql,
        Action<SqlParameterCollection>? configureParameters = null,
        params string[] tableNames)
    {
        var dataSet = new DataSet();
        using var connection = factory.CreateConnection();
        using var command = new SqlCommand(sql, connection);
        configureParameters?.Invoke(command.Parameters);

        using var adapter = new SqlDataAdapter(command);
        adapter.Fill(dataSet);

        for (var i = 0; i < tableNames.Length && i < dataSet.Tables.Count; i++)
        {
            dataSet.Tables[i].TableName = tableNames[i];
        }

        return dataSet;
    }

    public static object DbNullIfNull(object? value) => value ?? DBNull.Value;

    public static string? GetNullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static int? GetNullableInt32(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    public static DateTime? GetNullableDateTime(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    public static bool IsUniqueViolation(SqlException ex) => ex.Number is 2627 or 2601;
}
