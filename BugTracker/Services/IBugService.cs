using BugTracker.Models;
using BugTracker.Models.Enums;
using System.Security.Claims;
using static BugTracker.Models.BugListViewModel;

namespace BugTracker.Services
{
    public interface IBugService
    {
        // New dashboard methods
        Task<IEnumerable<BugReport>> GetRecentBugsAsync(int count);
        Task<int> GetBugCountBySeverityAsync(Severity severity);
        Task<int> GetAssignedBugsCountAsync(string username);
        Task<double> GetTrendPercentageAsync();

        // Existing methods
        Task<Dictionary<Status, int>> GetBugStatusStatisticsAsync();
        Task<Dictionary<Severity, int>> GetBugSeverityStatisticsAsync();
        Task<IEnumerable<BugReport>> GetAllBugReportsAsync();
        Task<BugReport> GetBugReportAsync(int id);
        Task<BugReport> CreateBugReportAsync(BugReport bugReport);
        Task UpdateBugReportAsync(BugReport bugReport);
        Task DeleteBugReportAsync(int id);
        Task<bool> CanAccessBugReport(ClaimsPrincipal user, BugReport bugReport);
        Task<IEnumerable<BugReport>> GetBugReportsByUserAsync(string userId);
        Task<(IEnumerable<BugReport> Bugs, int TotalCount)> SearchBugReportsAsync(
            BugSearchModel searchModel,
            int page = 1,
            int pageSize = 10);
        Task<IEnumerable<BugReport>> GetBugReportsByFilterAsync(
            string? searchTerm = null,
            Status? status = null,
            Severity? severity = null,
            string? assignedToId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null);
    }
}
