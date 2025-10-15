using BugTracker.Data;
using BugTracker.Exceptions;
using BugTracker.Models;
using BugTracker.Models.Enums;
using BugTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static BugTracker.Models.BugListViewModel;

public class BugService : IBugService
{
    private readonly ApplicationDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<BugService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public BugService(
        ApplicationDbContext context,
        IActivityLogService activityLogService,
        ILogger<BugService> logger,
        UserManager<ApplicationUser> userManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<IEnumerable<BugReport>> GetAllBugReportsAsync()
    {
        try
        {
            return await _context.BugReports
                .AsNoTracking()
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all bug reports");
            throw new ApplicationException("Error retrieving bug reports", ex);
        }
    }

    public async Task<IEnumerable<BugReport>> GetRecentBugsAsync(int count)
    {
        try
        {
            return await _context.BugReports
                .AsNoTracking()
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .OrderByDescending(b => b.CreatedDate)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent bugs");
            throw new ApplicationException("Error retrieving recent bugs", ex);
        }
    }

    public async Task<int> GetBugCountBySeverityAsync(Severity severity)
    {
        try
        {
            return await _context.BugReports
                .AsNoTracking()
                .CountAsync(b => b.Severity == severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bug count by severity {Severity}", severity);
            throw new ApplicationException($"Error getting bug count for severity {severity}", ex);
        }
    }

    public async Task<int> GetAssignedBugsCountAsync(string username)
    {
        try
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return 0;

            return await _context.BugReports
                .AsNoTracking()
                .CountAsync(b => b.AssignedToId == user.Id && b.Status != Status.Resolved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assigned bugs count for user {Username}", username);
            throw new ApplicationException($"Error getting assigned bugs count for user {username}", ex);
        }
    }

    public async Task<double> GetTrendPercentageAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);
            var sixtyDaysAgo = now.AddDays(-60);

            var currentPeriodCount = await _context.BugReports
                .AsNoTracking()
                .CountAsync(b => b.CreatedDate >= thirtyDaysAgo);

            var previousPeriodCount = await _context.BugReports
                .AsNoTracking()
                .CountAsync(b => b.CreatedDate >= sixtyDaysAgo && b.CreatedDate < thirtyDaysAgo);

            if (previousPeriodCount == 0) return 0;

            var percentageChange = ((double)(currentPeriodCount - previousPeriodCount) / previousPeriodCount) * 100;
            return Math.Round(percentageChange, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating bug trend percentage");
            throw new ApplicationException("Error calculating bug trend", ex);
        }
    }




    public async Task<bool> CanAccessBugReport(ClaimsPrincipal user, BugReport bugReport)
    {
        if (user == null) return false;
        if (bugReport == null) return false;

        try
        {
            // Admin users can access all bug reports
            if (user.IsInRole("Admin"))
                return true;

            // Get the current user
            var currentUser = await _userManager.GetUserAsync(user);
            if (currentUser == null)
                return false;

            // Users can access bugs if they:
            // 1. Created the bug
            // 2. Are assigned to the bug
            return bugReport.CreatedById == currentUser.Id ||
                   bugReport.AssignedToId == currentUser.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking bug report access for user {UserId} and bug {BugId}",
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value, bugReport.Id);
            return false;
        }
    }
    public async Task DeleteBugReportAsync(int id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var bugReport = await _context.BugReports
                .Include(b => b.Attachments)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bugReport == null)
                throw new NotFoundException($"Bug report {id} not found");

            // Remove attachments first
            _context.BugAttachments.RemoveRange(bugReport.Attachments);

            // Remove the bug report
            _context.BugReports.Remove(bugReport);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            _logger.LogInformation("Deleted bug report {BugId}", id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to delete bug report {BugId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<BugReport>> GetBugReportsByUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        try
        {
            return await _context.BugReports
                .AsNoTracking()
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .Where(b => b.AssignedToId == userId || b.CreatedById == userId)
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bug reports for user {UserId}", userId);
            throw;
        }
    }

    public async Task<Dictionary<Status, int>> GetBugStatusStatisticsAsync()
    {
        try
        {
            var statistics = await _context.BugReports
                .AsNoTracking()
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            // Ensure all status values are represented in the dictionary
            foreach (Status status in Enum.GetValues(typeof(Status)))
            {
                if (!statistics.ContainsKey(status))
                {
                    statistics[status] = 0;
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bug status statistics");
            throw;
        }
    }
    public async Task<Dictionary<Severity, int>> GetBugSeverityStatisticsAsync()
    {
        try
        {
            var statistics = await _context.BugReports
                .AsNoTracking()
                .GroupBy(b => b.Severity)
                .Select(g => new { Severity = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Severity, x => x.Count);

            // Ensure all severity values are represented in the dictionary
            foreach (Severity severity in Enum.GetValues(typeof(Severity)))
            {
                if (!statistics.ContainsKey(severity))
                {
                    statistics[severity] = 0;
                }
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bug severity statistics");
            throw;
        }
    }

    public async Task<(IEnumerable<BugReport> Bugs, int TotalCount)> SearchBugReportsAsync(
        BugSearchModel searchModel,
        int page = 1,
        int pageSize = 10)
    {
        try
        {
            searchModel ??= new BugSearchModel();

            var query = _context.BugReports
                .AsNoTracking() // Performance optimization for read-only queries
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .AsQueryable();

            // Apply search filters
            query = ApplySearchFilters(query, searchModel);

            // Apply sorting
            query = ApplySorting(query, searchModel);

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and materialize the query
            var bugs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (bugs, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching bug reports with criteria: {@SearchModel}", searchModel);
            throw new ApplicationException("Error retrieving bug reports", ex);
        }
    }

    private IQueryable<BugReport> ApplySearchFilters(IQueryable<BugReport> query, BugSearchModel searchModel)
    {
        // Search term filter - optimized for better performance
        if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
        {
            var searchTerm = searchModel.SearchTerm.Trim();
            query = query.Where(b =>
                b.Title.Contains(searchTerm) ||
                b.Description.Contains(searchTerm) ||
                b.Id.ToString() == searchTerm
            );
        }

        // Status filter
        if (searchModel.Status.HasValue)
        {
            query = query.Where(b => b.Status == searchModel.Status.Value);
        }

        // Severity filter
        if (searchModel.Severity.HasValue)
        {
            query = query.Where(b => b.Severity == searchModel.Severity.Value);
        }

        // Assigned user filter
        if (!string.IsNullOrEmpty(searchModel.AssignedToId))
        {
            query = query.Where(b => b.AssignedToId == searchModel.AssignedToId);
        }

        // Date range filter
        if (searchModel.DateFrom.HasValue)
        {
            var fromDate = searchModel.DateFrom.Value.Date;
            query = query.Where(b => b.CreatedDate.Date >= fromDate);
        }

        if (searchModel.DateTo.HasValue)
        {
            var toDate = searchModel.DateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(b => b.CreatedDate <= toDate);
        }

        // Tag filter
        if (searchModel.SelectedTags != null && searchModel.SelectedTags.Any())
        {
            query = query.Where(b => b.Tags.Any(t => searchModel.SelectedTags.Contains(t.Name)));
        }

        return query;
    }

    private IQueryable<BugReport> ApplySorting(IQueryable<BugReport> query, BugSearchModel searchModel)
    {
        return (searchModel.SortBy?.ToLower(), searchModel.SortDescending) switch
        {
            ("title", true) => query.OrderByDescending(b => b.Title),
            ("title", false) => query.OrderBy(b => b.Title),
            ("status", true) => query.OrderByDescending(b => b.Status),
            ("status", false) => query.OrderBy(b => b.Status),
            ("severity", true) => query.OrderByDescending(b => b.Severity),
            ("severity", false) => query.OrderBy(b => b.Severity),
            ("assignedto", true) => query.OrderByDescending(b => b.AssignedTo.Email),
            ("assignedto", false) => query.OrderBy(b => b.AssignedTo.Email),
            _ => query.OrderByDescending(b => b.CreatedDate) // Default sorting
        };
    }

    public async Task<BugReport> GetBugReportAsync(int id)
    {
        try
        {
            var bugReport = await _context.BugReports
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Attachments)
                .Include(b => b.Tags)
                .Include(b => b.ActivityLogs)
                    .ThenInclude(log => log.User)
                .Include(b => b.Project)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bugReport == null)
            {
                throw new NotFoundException($"Bug report with ID {id} not found");
            }

            bugReport.ActivityLogs = bugReport.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            return bugReport;
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            _logger.LogError(ex, "Error retrieving bug report {BugId}", id);
            throw new ApplicationException($"Error retrieving bug report {id}", ex);
        }
    }

    public async Task<BugReport> CreateBugReportAsync(BugReport bugReport)
    {
        if (bugReport == null) throw new ArgumentNullException(nameof(bugReport));

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            bugReport.CreatedDate = DateTime.UtcNow;

            // If ProjectId is provided, verify the project exists
            if (bugReport.ProjectId.HasValue)
            {
                var project = await _context.Projects
                    .Include(p => p.BugReports)
                    .FirstOrDefaultAsync(p => p.Id == bugReport.ProjectId.Value);

                if (project == null)
                {
                    throw new InvalidOperationException($"Project with ID {bugReport.ProjectId.Value} not found");
                }

                // Ensure the navigation property is set
                bugReport.Project = project;
            }

            // Add the bug report
            await _context.BugReports.AddAsync(bugReport);
            await _context.SaveChangesAsync();

            // Reload the complete bug report with all related data
            var createdBug = await _context.BugReports
                .Include(b => b.Project)
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == bugReport.Id);

            await transaction.CommitAsync();

            _logger.LogInformation("Created bug report {BugId} for project {ProjectId}",
                bugReport.Id, bugReport.ProjectId);

            return createdBug;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create bug report: {@BugReport}", bugReport);
            throw new ApplicationException("Failed to create bug report", ex);
        }
    }

    public async Task UpdateBugReportAsync(BugReport bugReport, string editorUserId)
    {
        if (bugReport == null) throw new ArgumentNullException(nameof(bugReport));
        if (string.IsNullOrWhiteSpace(editorUserId)) throw new ArgumentException("Editor user ID is required", nameof(editorUserId));

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var existingBug = await _context.BugReports
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == bugReport.Id);

            if (existingBug == null)
                throw new NotFoundException($"Bug report {bugReport.Id} not found");

            var changes = TrackChanges(existingBug, bugReport);
            UpdateBugProperties(existingBug, bugReport);

            await _context.SaveChangesAsync();

            if (changes.Any())
            {
                await _activityLogService.LogActivityAsync(
                    bugReport.Id,
                    editorUserId,
                    "Updated",
                    string.Join(", ", changes)
                );
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update bug report {BugId}", bugReport.Id);
            throw;
        }
    }

    private List<string> TrackChanges(BugReport existingBug, BugReport updatedBug)
    {
        var changes = new List<string>();

        if (existingBug.Title != updatedBug.Title)
            changes.Add($"Title changed from '{existingBug.Title}' to '{updatedBug.Title}'");

        if (existingBug.Status != updatedBug.Status)
            changes.Add($"Status changed from '{existingBug.Status}' to '{updatedBug.Status}'");

        if (existingBug.Severity != updatedBug.Severity)
            changes.Add($"Severity changed from '{existingBug.Severity}' to '{updatedBug.Severity}'");

        if (existingBug.AssignedToId != updatedBug.AssignedToId)
            changes.Add($"Reassigned from '{existingBug.AssignedTo?.Email}' to '{updatedBug.AssignedTo?.Email}'");

        return changes;
    }

    private void UpdateBugProperties(BugReport existingBug, BugReport updatedBug)
    {
        existingBug.Title = updatedBug.Title;
        existingBug.Description = updatedBug.Description;
        existingBug.Status = updatedBug.Status;
        existingBug.Severity = updatedBug.Severity;
        existingBug.AssignedToId = updatedBug.AssignedToId;
        existingBug.ProjectId = updatedBug.ProjectId;
        existingBug.UpdatedDate = DateTime.UtcNow;
    }
    public async Task<IEnumerable<BugReport>> GetBugReportsByFilterAsync(
        string? searchTerm = null,
        Status? status = null,
        Severity? severity = null,
        string? assignedToId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        try
        {
            var query = _context.BugReports
                .AsNoTracking()
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim().ToLower();
                query = query.Where(b =>
                    EF.Functions.Like(b.Title.ToLower(), $"%{searchTerm}%") ||
                    EF.Functions.Like(b.Description.ToLower(), $"%{searchTerm}%") ||
                    b.Id.ToString() == searchTerm
                );
            }

            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }

            if (severity.HasValue)
            {
                query = query.Where(b => b.Severity == severity.Value);
            }

            if (!string.IsNullOrEmpty(assignedToId))
            {
                query = query.Where(b => b.AssignedToId == assignedToId);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(b => b.CreatedDate.Date >= dateFrom.Value.Date);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(b => b.CreatedDate.Date <= dateTo.Value.Date);
            }

            return await query
                .OrderByDescending(b => b.CreatedDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving filtered bug reports");
            throw new ApplicationException("Error retrieving filtered bug reports", ex);
        }
    }




}