using BugTracker.Models;

namespace BugTracker.Services
{
    public interface IActivityLogService
    {
        Task LogActivityAsync(int bugReportId, string userId, string action, string details);
        Task<IEnumerable<ActivityLog>> GetActivityLogsForBugAsync(int bugReportId);
        Task AddCommentAsync(int bugReportId, string userId, string comment);

    }
}
