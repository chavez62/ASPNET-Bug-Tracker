using BugTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BugTracker.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DatabaseController : Controller
    {
        private readonly IDatabasePerformanceService _dbPerformanceService;
        private readonly IQueryPerformanceService _queryPerformanceService;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(
            IDatabasePerformanceService dbPerformanceService,
            IQueryPerformanceService queryPerformanceService,
            ILogger<DatabaseController> logger)
        {
            _dbPerformanceService = dbPerformanceService;
            _queryPerformanceService = queryPerformanceService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var dbMetrics = await _dbPerformanceService.GetPerformanceMetricsAsync();
                var queryMetrics = await _queryPerformanceService.GetQueryMetricsAsync();
                var slowQueries = await _queryPerformanceService.GetSlowQueriesAsync(5);
                var indexInfo = await _dbPerformanceService.GetIndexUsageInfoAsync();

                ViewBag.DatabaseMetrics = dbMetrics;
                ViewBag.QueryMetrics = queryMetrics;
                ViewBag.SlowQueries = slowQueries;
                ViewBag.IndexInfo = indexInfo;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading database performance data");
                TempData["ErrorMessage"] = "Error loading database performance data";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Optimize()
        {
            try
            {
                var success = await _dbPerformanceService.OptimizeDatabaseAsync();
                
                if (success)
                {
                    TempData["SuccessMessage"] = "Database optimization completed successfully";
                    _logger.LogInformation("Database optimization completed by user {UserId}", User.Identity?.Name);
                }
                else
                {
                    TempData["ErrorMessage"] = "Database optimization failed";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database optimization");
                TempData["ErrorMessage"] = "Error during database optimization";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var dbMetrics = await _dbPerformanceService.GetPerformanceMetricsAsync();
                var queryMetrics = await _queryPerformanceService.GetQueryMetricsAsync();
                
                return Json(new
                {
                    Database = dbMetrics,
                    Queries = queryMetrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                return BadRequest("Error retrieving metrics");
            }
        }
    }
}
