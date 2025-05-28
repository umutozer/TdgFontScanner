using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;
using TdgFontScanner.Models;

namespace TdgFontScanner.Services
{
    /// <summary>
    /// Analiz sonuçlarını SQLite veritabanında saklamak için kullanılan servis sınıfı
    /// </summary>
    public class AnalysisStorageService
    {
        private readonly string _dbPath;

        public AnalysisStorageService()
        {
            // Veritabanı dosyasının yolu
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AnalysisResults.db");
            InitializeDatabase();
        }

        /// <summary>
        /// Veritabanını ve tabloyu oluşturur
        /// </summary>
        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS AnalysisResults (
                        Id TEXT PRIMARY KEY,
                        Domain TEXT NOT NULL,
                        MaxPage INTEGER NOT NULL,
                        MaxDeep INTEGER NOT NULL,
                        AnalysisData TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Analiz sonucunu veritabanına kaydeder
        /// </summary>
        public async Task<string> SaveAnalysisResultAsync(AnalysisResult result)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO AnalysisResults (Id, Domain, MaxPage, MaxDeep, AnalysisData, CreatedAt)
                    VALUES (@Id, @Domain, @MaxPage, @MaxDeep, @AnalysisData, @CreatedAt);";

                command.Parameters.AddWithValue("@Id", result.Id);
                command.Parameters.AddWithValue("@Domain", result.Domain);
                command.Parameters.AddWithValue("@MaxPage", result.MaxPage);
                command.Parameters.AddWithValue("@MaxDeep", result.MaxDeep);
                command.Parameters.AddWithValue("@AnalysisData", result.AnalysisData);
                command.Parameters.AddWithValue("@CreatedAt", result.CreatedAt.ToString("o"));

                await command.ExecuteNonQueryAsync();
                return result.Id;
            }
        }

        /// <summary>
        /// ID'ye göre analiz sonucunu getirir
        /// </summary>
        public async Task<AnalysisResult> GetAnalysisResultAsync(string id)
        {
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Domain, MaxPage, MaxDeep, AnalysisData, CreatedAt
                    FROM AnalysisResults
                    WHERE Id = @Id;";

                command.Parameters.AddWithValue("@Id", id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new AnalysisResult
                        {
                            Id = reader.GetString(0),
                            Domain = reader.GetString(1),
                            MaxPage = reader.GetInt32(2),
                            MaxDeep = reader.GetInt32(3),
                            AnalysisData = reader.GetString(4),
                            CreatedAt = DateTime.Parse(reader.GetString(5))
                        };
                    }
                }
            }
            return null;
        }

       /// <summary>
/// Belirli bir süre önceki analiz sonuçlarını temizler
/// </summary>
public async Task CleanupOldResultsAsync(int daysToKeep = 7)
{
    using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
    {
        await connection.OpenAsync();
                var date = DateTime.Now.AddDays(-daysToKeep);
        var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM AnalysisResults
            WHERE CreatedAt <= @CutoffDate;";

        command.Parameters.AddWithValue("@CutoffDate", date);
                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch 
                {
                   
                }
        
    }
}

    }
} 