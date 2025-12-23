using Microsoft.Data.Sqlite;

namespace DownloadSorter.Core.Data;

public class Repository : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public Repository(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={databasePath}";
        Initialize();
    }

    private SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }
        return _connection;
    }

    private void Initialize()
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS file_history (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                original_name   TEXT NOT NULL,
                final_name      TEXT NOT NULL,
                source_path     TEXT NOT NULL,
                dest_path       TEXT NOT NULL,
                category        TEXT NOT NULL,
                file_size       INTEGER NOT NULL,
                sorted_at       TEXT NOT NULL,
                file_hash       TEXT,
                status          INTEGER NOT NULL DEFAULT 0,
                error_message   TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_file_history_original_name
                ON file_history(original_name);
            CREATE INDEX IF NOT EXISTS idx_file_history_sorted_at
                ON file_history(sorted_at DESC);
            CREATE INDEX IF NOT EXISTS idx_file_history_category
                ON file_history(category);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Insert(FileRecord record)
    {
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            INSERT INTO file_history
                (original_name, final_name, source_path, dest_path, category,
                 file_size, sorted_at, file_hash, status, error_message)
            VALUES
                (@originalName, @finalName, @sourcePath, @destPath, @category,
                 @fileSize, @sortedAt, @fileHash, @status, @errorMessage)
            """;

        cmd.Parameters.AddWithValue("@originalName", record.OriginalName);
        cmd.Parameters.AddWithValue("@finalName", record.FinalName);
        cmd.Parameters.AddWithValue("@sourcePath", record.SourcePath);
        cmd.Parameters.AddWithValue("@destPath", record.DestPath);
        cmd.Parameters.AddWithValue("@category", record.Category);
        cmd.Parameters.AddWithValue("@fileSize", record.FileSize);
        cmd.Parameters.AddWithValue("@sortedAt", record.SortedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@fileHash", record.FileHash ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (int)record.Status);
        cmd.Parameters.AddWithValue("@errorMessage", record.ErrorMessage ?? (object)DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public List<FileRecord> GetRecent(int limit = 50)
    {
        var records = new List<FileRecord>();
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, final_name, source_path, dest_path,
                   category, file_size, sorted_at, file_hash, status, error_message
            FROM file_history
            ORDER BY sorted_at DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public List<FileRecord> Search(string query, int limit = 50)
    {
        var records = new List<FileRecord>();
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, final_name, source_path, dest_path,
                   category, file_size, sorted_at, file_hash, status, error_message
            FROM file_history
            WHERE original_name LIKE @query OR final_name LIKE @query
            ORDER BY sorted_at DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public List<FileRecord> GetByCategory(string category, int limit = 50)
    {
        var records = new List<FileRecord>();
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, final_name, source_path, dest_path,
                   category, file_size, sorted_at, file_hash, status, error_message
            FROM file_history
            WHERE category = @category
            ORDER BY sorted_at DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public List<FileRecord> GetByDateRange(DateTime from, DateTime to, int limit = 100)
    {
        var records = new List<FileRecord>();
        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            SELECT id, original_name, final_name, source_path, dest_path,
                   category, file_size, sorted_at, file_hash, status, error_message
            FROM file_history
            WHERE sorted_at >= @from AND sorted_at <= @to
            ORDER BY sorted_at DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@from", from.ToString("O"));
        cmd.Parameters.AddWithValue("@to", to.ToString("O"));
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }
        return records;
    }

    public DailyStats GetTodayStats()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        using var cmd = GetConnection().CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*) as total,
                SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) as success,
                SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) as skipped,
                SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END) as failed,
                COALESCE(MAX(file_size), 0) as biggest_file
            FROM file_history
            WHERE sorted_at >= @from AND sorted_at < @to
            """;
        cmd.Parameters.AddWithValue("@from", today.ToString("O"));
        cmd.Parameters.AddWithValue("@to", tomorrow.ToString("O"));

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new DailyStats
            {
                Total = reader.GetInt32(0),
                Success = reader.GetInt32(1),
                Skipped = reader.GetInt32(2),
                Failed = reader.GetInt32(3),
                BiggestFileSize = reader.GetInt64(4)
            };
        }
        return new DailyStats();
    }

    public Dictionary<string, int> GetCategoryCounts(DateTime? since = null)
    {
        var counts = new Dictionary<string, int>();
        using var cmd = GetConnection().CreateCommand();

        if (since.HasValue)
        {
            cmd.CommandText = """
                SELECT category, COUNT(*) as count
                FROM file_history
                WHERE sorted_at >= @since AND status = 0
                GROUP BY category
                ORDER BY count DESC
                """;
            cmd.Parameters.AddWithValue("@since", since.Value.ToString("O"));
        }
        else
        {
            cmd.CommandText = """
                SELECT category, COUNT(*) as count
                FROM file_history
                WHERE status = 0
                GROUP BY category
                ORDER BY count DESC
                """;
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            counts[reader.GetString(0)] = reader.GetInt32(1);
        }
        return counts;
    }

    private static FileRecord ReadRecord(SqliteDataReader reader)
    {
        return new FileRecord
        {
            Id = reader.GetInt64(0),
            OriginalName = reader.GetString(1),
            FinalName = reader.GetString(2),
            SourcePath = reader.GetString(3),
            DestPath = reader.GetString(4),
            Category = reader.GetString(5),
            FileSize = reader.GetInt64(6),
            SortedAt = DateTime.Parse(reader.GetString(7)),
            FileHash = reader.IsDBNull(8) ? null : reader.GetString(8),
            Status = (SortStatus)reader.GetInt32(9),
            ErrorMessage = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}

public class DailyStats
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long BiggestFileSize { get; set; }
}
