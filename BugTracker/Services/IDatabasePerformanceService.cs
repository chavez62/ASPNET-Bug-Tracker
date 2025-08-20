namespace BugTracker.Services
{
    public interface IDatabasePerformanceService
    {
        Task<DatabasePerformanceMetrics> GetPerformanceMetricsAsync();
        Task<List<string>> GetSlowQueriesAsync();
        Task<bool> OptimizeDatabaseAsync();
        Task<long> GetDatabaseSizeAsync();
        Task<int> GetTableRowCountsAsync(string tableName);
        Task<List<IndexUsageInfo>> GetIndexUsageInfoAsync();
    }

    public class DatabasePerformanceMetrics
    {
        public long TotalDatabaseSize { get; set; }
        public int TotalBugReports { get; set; }
        public int TotalProjects { get; set; }
        public int TotalUsers { get; set; }
        public int TotalAttachments { get; set; }
        public int TotalActivityLogs { get; set; }
        public double AverageQueryTime { get; set; }
        public int SlowQueryCount { get; set; }
        public DateTime LastOptimization { get; set; }
        public bool IsOptimized { get; set; }
    }

    public class IndexUsageInfo
    {
        public string TableName { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;
        public long UsageCount { get; set; }
        public bool IsUnique { get; set; }
        public bool IsActive { get; set; }
    }
}
