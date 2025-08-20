namespace BugTracker.Services
{
    public interface IQueryPerformanceService
    {
        Task<QueryPerformanceMetrics> GetQueryMetricsAsync();
        Task<List<SlowQueryInfo>> GetSlowQueriesAsync(int limit = 10);
        Task<bool> IsQueryOptimizedAsync(string queryHash);
        Task LogQueryExecutionAsync(string queryHash, string query, long executionTime);
    }

    public class QueryPerformanceMetrics
    {
        public int TotalQueries { get; set; }
        public double AverageExecutionTime { get; set; }
        public long TotalExecutionTime { get; set; }
        public int SlowQueryCount { get; set; }
        public int OptimizedQueryCount { get; set; }
        public DateTime LastOptimization { get; set; }
    }

    public class SlowQueryInfo
    {
        public string QueryHash { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public long ExecutionTime { get; set; }
        public DateTime LastExecuted { get; set; }
        public int ExecutionCount { get; set; }
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
