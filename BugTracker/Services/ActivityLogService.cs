using BugTracker.Data;
using BugTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogActivityAsync(int bugReportId, string userId, string action, string details)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
            }

            var activityLog = new ActivityLog
            {
                BugReportId = bugReportId,
                UserId = userId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ActivityLog>> GetActivityLogsForBugAsync(int bugReportId)
        {
            return await _context.ActivityLogs
                .AsNoTracking()
                .AsSplitQuery()
                .Where(log => log.BugReportId == bugReportId)
                .Include(log => log.User)
                .OrderByDescending(log => log.Timestamp)
                .ToListAsync();
        }

        
        public async Task AddCommentAsync(int bugReportId, string userId, string comment)
        {
            var activityLog = new ActivityLog
            {
                BugReportId = bugReportId,
                UserId = userId,
                Action = "Commented",
                Details = comment,
                Timestamp = DateTime.UtcNow
            };

            _context.ActivityLogs.Add(activityLog);
            await _context.SaveChangesAsync();
        }
    }
}
