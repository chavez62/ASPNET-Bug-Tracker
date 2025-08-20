using BugTracker.Services;
using Microsoft.Extensions.Logging;

namespace BugTracker.Services
{
    public class QueryPerformanceService : IQueryPerformanceService
    {
        private readonly ILogger<QueryPerformanceService> _logger;
        private readonly Dictionary<string, QueryExecutionInfo> _queryCache;

        public QueryPerformanceService(ILogger<QueryPerformanceService> logger)
        {
            _logger = logger;
            _queryCache = new Dictionary<string, QueryExecutionInfo>();
        }

        public async Task<QueryPerformanceMetrics> GetQueryMetricsAsync()
        {
            try
            {
                var metrics = new QueryPerformanceMetrics
                {
                    TotalQueries = _queryCache.Values.Sum(q => q.ExecutionCount),
                    TotalExecutionTime = _queryCache.Values.Sum(q => q.TotalExecutionTime),
                    SlowQueryCount = _queryCache.Values.Count(q => q.AverageExecutionTime > 100), // 100ms threshold
                    OptimizedQueryCount = _queryCache.Values.Count(q => q.AverageExecutionTime < 50), // 50ms threshold
                    LastOptimization = DateTime.UtcNow
                };

                if (metrics.TotalQueries > 0)
                {
                    metrics.AverageExecutionTime = (double)metrics.TotalExecutionTime / metrics.TotalQueries;
                }

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting query performance metrics");
                return new QueryPerformanceMetrics();
            }
        }

        public async Task<List<SlowQueryInfo>> GetSlowQueriesAsync(int limit = 10)
        {
            try
            {
                var slowQueries = _queryCache.Values
                    .Where(q => q.AverageExecutionTime > 100) // 100ms threshold
                    .OrderByDescending(q => q.AverageExecutionTime)
                    .Take(limit)
                    .Select(q => new SlowQueryInfo
                    {
                        QueryHash = q.QueryHash,
                        Query = q.Query,
                        ExecutionTime = (long)q.AverageExecutionTime,
                        LastExecuted = q.LastExecuted,
                        ExecutionCount = q.ExecutionCount,
                        Controller = q.Controller,
                        Action = q.Action
                    })
                    .ToList();

                return slowQueries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slow queries");
                return new List<SlowQueryInfo>();
            }
        }

        public async Task<bool> IsQueryOptimizedAsync(string queryHash)
        {
            try
            {
                if (_queryCache.TryGetValue(queryHash, out var queryInfo))
                {
                    return queryInfo.AverageExecutionTime < 50; // 50ms threshold
                }
                return true; // Unknown queries are considered optimized
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking query optimization status");
                return false;
            }
        }

        public async Task LogQueryExecutionAsync(string queryHash, string query, long executionTime)
        {
            try
            {
                if (!_queryCache.ContainsKey(queryHash))
                {
                    _queryCache[queryHash] = new QueryExecutionInfo
                    {
                        QueryHash = queryHash,
                        Query = query,
                        ExecutionCount = 0,
                        TotalExecutionTime = 0,
                        LastExecuted = DateTime.UtcNow
                    };
                }

                var queryInfo = _queryCache[queryHash];
                queryInfo.ExecutionCount++;
                queryInfo.TotalExecutionTime += executionTime;
                queryInfo.LastExecuted = DateTime.UtcNow;

                // Log slow queries
                if (executionTime > 100)
                {
                    _logger.LogWarning("Slow query detected: {QueryHash} took {ExecutionTime}ms", 
                        queryHash, executionTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging query execution");
            }
        }

        private class QueryExecutionInfo
        {
            public string QueryHash { get; set; } = string.Empty;
            public string Query { get; set; } = string.Empty;
            public int ExecutionCount { get; set; }
            public long TotalExecutionTime { get; set; }
            public DateTime LastExecuted { get; set; }
            public string Controller { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;

            public double AverageExecutionTime => ExecutionCount > 0 ? (double)TotalExecutionTime / ExecutionCount : 0;
        }
    }
}
