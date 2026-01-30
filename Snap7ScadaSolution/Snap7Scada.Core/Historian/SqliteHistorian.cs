using Microsoft.Data.Sqlite;
using Snap7ClientLib.Tags;

namespace Snap7ClientLib.Historian;

/// <summary>
/// Lưu lịch sử tag PLC vào SQLite
/// </summary>
public class SqliteHistorian
{
    private readonly string _dbPath;

    public SqliteHistorian(string dbPath)
    {
        _dbPath = dbPath;
        Init();
    }

    private void Init()
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();

        var cmd = con.CreateCommand();
        cmd.CommandText =
        """
        CREATE TABLE IF NOT EXISTS History (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TagName TEXT,
            Value TEXT,
            Timestamp DATETIME
        );
        """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ghi lịch sử tag
    /// </summary>
    public void Log(PlcTag tag)
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();

        var cmd = con.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO History (TagName, Value, Timestamp)
        VALUES ($tag, $value, $time)
        """;

        cmd.Parameters.AddWithValue("$tag", tag.Name);
        cmd.Parameters.AddWithValue("$value", tag.NewValue?.ToString());
        cmd.Parameters.AddWithValue("$time", DateTime.Now);

        cmd.ExecuteNonQuery();
    }
}
