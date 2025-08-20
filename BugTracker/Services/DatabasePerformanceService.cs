using BugTracker.Data;
using BugTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BugTracker.Services
{
    public class DatabasePerformanceService : IDatabasePerformanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabasePerformanceService> _logger;
        private readonly Stopwatch _queryTimer;

        public DatabasePerformanceService(
            ApplicationDbContext context,
            ILogger<DatabasePerformanceService> logger)
        {
            _context = context;
            _logger = logger;
            _queryTimer = new Stopwatch();
        }

        public async Task<DatabasePerformanceMetrics> GetPerformanceMetricsAsync()
        {
            try
            {
                _queryTimer.Restart();

                var metrics = new DatabasePerformanceMetrics
                {
                    TotalDatabaseSize = await GetDatabaseSizeAsync(),
                    TotalBugReports = await GetTableRowCountsAsync("BugReports"),
                    TotalProjects = await GetTableRowCountsAsync("Projects"),
                    TotalUsers = await GetTableRowCountsAsync("AspNetUsers"),
                    TotalAttachments = await GetTableRowCountsAsync("BugAttachments"),
                    TotalActivityLogs = await GetTableRowCountsAsync("ActivityLogs"),
                    LastOptimization = DateTime.UtcNow, // This would come from a settings table in production
                    IsOptimized = true
                };

                _queryTimer.Stop();
                metrics.AverageQueryTime = _queryTimer.ElapsedMilliseconds;

                _logger.LogInformation("Database performance metrics collected in {ElapsedMs}ms", 
                    _queryTimer.ElapsedMilliseconds);

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting database performance metrics");
                throw;
            }
        }

        public async Task<List<string>> GetSlowQueriesAsync()
        {
            // In a production environment, this would query the database's query log
            // For SQLite, we'll return a placeholder list
            return new List<string>
            {
                "SELECT * FROM BugReports WHERE Status = @status AND CreatedDate >= @date",
                "SELECT COUNT(*) FROM BugReports WHERE AssignedToId = @userId"
            };
        }

        public async Task<bool> OptimizeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Starting database optimization");

                // SQLite-specific optimizations
                await _context.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                await _context.Database.ExecuteSqlRawAsync("VACUUM");
                await _context.Database.ExecuteSqlRawAsync("ANALYZE");

                // Update statistics for better query planning
                await _context.Database.ExecuteSqlRawAsync("PRAGMA analysis_limit=1000");
                await _context.Database.ExecuteSqlRawAsync("PRAGMA optimize");

                _logger.LogInformation("Database optimization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database optimization");
                return false;
            }
        }

        public async Task<long> GetDatabaseSizeAsync()
        {
            try
            {
                var dbPath = _context.Database.GetConnectionString();
                if (string.IsNullOrEmpty(dbPath))
                    return 0;

                // Extract the database file path from connection string
                var filePath = dbPath.Replace("Data Source=", "").Split(';')[0];
                
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Length;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database size");
                return 0;
            }
        }

        public async Task<int> GetTableRowCountsAsync(string tableName)
        {
            try
            {
                var sql = $"SELECT COUNT(*) FROM \"{tableName}\"";
                var count = await _context.Database.SqlQueryRaw<int>(sql).FirstOrDefaultAsync();
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting row count for table {TableName}", tableName);
                return 0;
            }
        }

        public async Task<List<IndexUsageInfo>> GetIndexUsageInfoAsync()
        {
            try
            {
                var indexes = new List<IndexUsageInfo>();

                // Get index information from SQLite
                var sql = @"
                    SELECT 
                        m.tbl_name as TableName,
                        i.name as IndexName,
                        i.unique as IsUnique,
                        i.partial as IsPartial
                    FROM sqlite_master i
                    INNER JOIN sqlite_master m ON i.tbl_name = m.tbl_name
                    WHERE i.type = 'index' 
                    AND m.type = 'table'
                    ORDER BY m.tbl_name, i.name";

                var results = await _context.Database.SqlQueryRaw<dynamic>(sql).ToListAsync();

                foreach (var result in results)
                {
                    indexes.Add(new IndexUsageInfo
                    {
                        TableName = result.TableName?.ToString() ?? "",
                        IndexName = result.IndexName?.ToString() ?? "",
                        IsUnique = result.IsUnique == 1,
                        IsActive = true,
                        UsageCount = 0 // SQLite doesn't track index usage statistics
                    });
                }

                return indexes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting index usage information");
                return new List<IndexUsageInfo>();
            }
        }
    }
}
