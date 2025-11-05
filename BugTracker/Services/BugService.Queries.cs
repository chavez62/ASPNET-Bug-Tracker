using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using BugTracker.Exceptions;
using BugTracker.Models;
using BugTracker.Models.Enums;
using Microsoft.EntityFrameworkCore;
using static BugTracker.Models.BugListViewModel;

namespace BugTracker.Services;

public partial class BugService
{
    public async Task<IEnumerable<BugReport>> GetAllBugReportsAsync()
    {
        try
        {
            return await _context.BugReports
                .AsNoTracking()
                .AsSplitQuery()
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
                .AsSplitQuery()
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
            if (user.IsInRole("Admin"))
            {
                return true;
            }

            var currentUser = await _userManager.GetUserAsync(user);
            if (currentUser == null)
            {
                return false;
            }

            return bugReport.CreatedById == currentUser.Id ||
                   bugReport.AssignedToId == currentUser.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking bug report access for user {UserId} and bug {BugId}",
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                bugReport.Id);
            return false;
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
                .AsSplitQuery()
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

            var pageNumber = page < 1 ? 1 : page;
            var size = pageSize < 1 ? (searchModel.PageSize > 0 ? searchModel.PageSize : 10) : pageSize;

            searchModel.Page = pageNumber;
            searchModel.PageSize = size;

            var includesTags = RequiresTagData(searchModel);

            var query = BuildBugReportQuery(searchModel, includesTags);

            query = ApplySorting(query, searchModel);

            var totalCount = await query.CountAsync();

            var bugs = await query
                .Skip((pageNumber - 1) * size)
                .Take(size)
                .ToListAsync();

            if (!includesTags)
            {
                await PopulateTagsForListAsync(bugs);
            }

            return (bugs, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching bug reports with criteria: {@SearchModel}", searchModel);
            throw new ApplicationException("Error retrieving bug reports", ex);
        }
    }

    public async Task<BugReport> GetBugReportAsync(int id)
    {
        try
        {
            var bugReport = await _context.BugReports
                .AsNoTracking()
                .AsSplitQuery()
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
            var searchModel = new BugSearchModel
            {
                SearchTerm = searchTerm,
                Status = status,
                Severity = severity,
                AssignedToId = assignedToId,
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            var query = BuildBugReportQuery(searchModel, includeTags: true);
            query = ApplySorting(query, searchModel);

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving filtered bug reports");
            throw new ApplicationException("Error retrieving filtered bug reports", ex);
        }
    }

    private IQueryable<BugReport> BuildBugReportQuery(
        BugSearchModel? searchModel,
        bool includeTags,
        bool asNoTracking = true)
    {
        searchModel ??= new BugSearchModel();

        var query = _context.BugReports.AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        query = query.AsSplitQuery();

        query = query
            .Include(b => b.AssignedTo)
            .Include(b => b.CreatedBy);

        if (includeTags)
        {
            query = query.Include(b => b.Tags);
        }

        return ApplySearchFilters(query, searchModel);
    }

    private async Task PopulateTagsForListAsync(IList<BugReport> bugs)
    {
        if (bugs == null || bugs.Count == 0)
        {
            return;
        }

        var bugIds = bugs
            .Where(b => b != null)
            .Select(b => b.Id)
            .ToList();

        if (bugIds.Count == 0)
        {
            return;
        }

        var tagLookup = await _context.BugReports
            .Where(b => bugIds.Contains(b.Id))
            .Select(b => new
            {
                BugId = b.Id,
                Tags = b.Tags.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Color
                }).ToList()
            })
            .AsNoTracking()
            .ToListAsync();

        var groupedTags = tagLookup
            .ToDictionary(
                item => item.BugId,
                item => item.Tags.Select(t => new Tag
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color
                }).ToList());

        foreach (var bug in bugs)
        {
            if (bug == null)
            {
                continue;
            }

            if (groupedTags.TryGetValue(bug.Id, out var tagList))
            {
                bug.Tags = tagList;
            }
            else
            {
                bug.Tags = new List<Tag>();
            }
        }
    }

    private static bool RequiresTagData(BugSearchModel searchModel)
    {
        if (searchModel == null)
        {
            return false;
        }

        return searchModel.SelectedTags != null && searchModel.SelectedTags.Any();
    }

    private IQueryable<BugReport> ApplySearchFilters(IQueryable<BugReport> query, BugSearchModel searchModel)
    {
        if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
        {
            var searchTerm = searchModel.SearchTerm.Trim();
            var likePattern = $"%{searchTerm}%";

            if (int.TryParse(searchTerm, out var bugId))
            {
                query = query.Where(b =>
                    b.Id == bugId ||
                    EF.Functions.Like(b.Title, likePattern) ||
                    EF.Functions.Like(b.Description, likePattern));
            }
            else
            {
                query = query.Where(b =>
                    EF.Functions.Like(b.Title, likePattern) ||
                    EF.Functions.Like(b.Description, likePattern));
            }
        }

        if (searchModel.Status.HasValue)
        {
            query = query.Where(b => b.Status == searchModel.Status.Value);
        }

        if (searchModel.Severity.HasValue)
        {
            query = query.Where(b => b.Severity == searchModel.Severity.Value);
        }

        if (!string.IsNullOrEmpty(searchModel.AssignedToId))
        {
            query = query.Where(b => b.AssignedToId == searchModel.AssignedToId);
        }

        if (searchModel.DateFrom.HasValue)
        {
            var fromDate = searchModel.DateFrom.Value.Date;
            query = query.Where(b => b.CreatedDate >= fromDate);
        }

        if (searchModel.DateTo.HasValue)
        {
            var toDateExclusive = searchModel.DateTo.Value.Date.AddDays(1);
            query = query.Where(b => b.CreatedDate < toDateExclusive);
        }

        if (searchModel.SelectedTags != null && searchModel.SelectedTags.Any())
        {
            var selectedTagIds = searchModel.SelectedTags;
            query = query.Where(b => b.Tags.Any(t => selectedTagIds.Contains(t.Id)));
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
            _ => query.OrderByDescending(b => b.CreatedDate)
        };
    }
}

