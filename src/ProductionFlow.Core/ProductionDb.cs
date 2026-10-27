using Microsoft.Data.SqlClient;
using System.Data;

namespace ProductionFlow.Api.Services;

public sealed class ProductionDb(string connectionString)
{
    private readonly string _connectionString = connectionString;

    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    public static SqlCommand Command(SqlConnection conn, SqlTransaction? tx, string sql, params (string name, object? value)[] parameters)
    {
        var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 30 };
        foreach (var (name,value) in parameters)
        {
            var p = cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            if (value is decimal) { p.SqlDbType = SqlDbType.Decimal; p.Precision = 18; p.Scale = 4; }
            if (value is Guid) p.SqlDbType = SqlDbType.UniqueIdentifier;
        }
        return cmd;
    }
    public static async Task<int> ExecAsync(SqlConnection conn, SqlTransaction? tx, string sql, CancellationToken ct, params (string name, object? value)[] args)
    {
        await using var cmd = Command(conn,tx,sql,args);return await cmd.ExecuteNonQueryAsync(ct);
    }
    public static async Task<T?> ScalarAsync<T>(SqlConnection conn,SqlTransaction? tx,string sql,CancellationToken ct,params (string name,object? value)[] args)
    {
        await using var cmd = Command(conn,tx,sql,args);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? default : (T)Convert.ChangeType(result,typeof(T));
    }
}
