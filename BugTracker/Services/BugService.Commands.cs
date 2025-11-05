using System;
using System.Collections.Generic;
using System.Linq;
using BugTracker.Exceptions;
using BugTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Services;

public partial class BugService
{
    public async Task DeleteBugReportAsync(int id)
    {
        try
        {
            var bugReport = await _context.BugReports
                .Include(b => b.Attachments)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bugReport == null)
                throw new NotFoundException($"Bug report {id} not found");

            _context.BugAttachments.RemoveRange(bugReport.Attachments);
            _context.BugReports.Remove(bugReport);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted bug report {BugId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bug report {BugId}", id);
            throw;
        }
    }

    public async Task<BugReport> CreateBugReportAsync(BugReport bugReport)
    {
        if (bugReport == null) throw new ArgumentNullException(nameof(bugReport));

        try
        {
            bugReport.CreatedDate = DateTime.UtcNow;

            if (bugReport.ProjectId.HasValue)
            {
                var projectExists = await _context.Projects
                    .AsNoTracking()
                    .AnyAsync(p => p.Id == bugReport.ProjectId.Value);

                if (!projectExists)
                {
                    throw new InvalidOperationException($"Project with ID {bugReport.ProjectId.Value} not found");
                }
            }

            await _context.BugReports.AddAsync(bugReport);
            await _context.SaveChangesAsync();

            var createdBug = await _context.BugReports
                .AsNoTracking()
                .AsSplitQuery()
                .Include(b => b.Project)
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .FirstOrDefaultAsync(b => b.Id == bugReport.Id);

            _logger.LogInformation("Created bug report {BugId} for project {ProjectId}",
                bugReport.Id, bugReport.ProjectId);

            return createdBug;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bug report: {@BugReport}", bugReport);
            throw new ApplicationException("Failed to create bug report", ex);
        }
    }

    public async Task UpdateBugReportAsync(
        BugReport bugReport,
        string editorUserId,
        IEnumerable<int>? tagIds = null,
        IEnumerable<int>? attachmentsToRemove = null)
    {
        if (bugReport == null) throw new ArgumentNullException(nameof(bugReport));
        if (string.IsNullOrWhiteSpace(editorUserId)) throw new ArgumentException("Editor user ID is required", nameof(editorUserId));

        try
        {
            var existingBug = await _context.BugReports
                .Include(b => b.AssignedTo)
                .Include(b => b.CreatedBy)
                .Include(b => b.Tags)
                .Include(b => b.Attachments)
                .FirstOrDefaultAsync(b => b.Id == bugReport.Id);

            if (existingBug == null)
                throw new NotFoundException($"Bug report {bugReport.Id} not found");

            var changes = await TrackChangesAsync(existingBug, bugReport);

            if (tagIds != null)
            {
                var tagChanges = await UpdateBugTagsAsync(existingBug, tagIds);
                changes.AddRange(tagChanges);
            }

            if (attachmentsToRemove != null && attachmentsToRemove.Any())
            {
                var attachmentChange = RemoveAttachments(existingBug, attachmentsToRemove);
                if (!string.IsNullOrWhiteSpace(attachmentChange))
                {
                    changes.Add(attachmentChange);
                }
            }

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update bug report {BugId}", bugReport.Id);
            throw;
        }
    }

    private async Task<List<string>> TrackChangesAsync(BugReport existingBug, BugReport updatedBug)
    {
        var changes = new List<string>();

        if (!string.Equals(existingBug.Title, updatedBug.Title, StringComparison.Ordinal))
        {
            changes.Add($"Title changed from '{existingBug.Title}' to '{updatedBug.Title}'");
        }

        if (!string.Equals(existingBug.Description, updatedBug.Description, StringComparison.Ordinal))
        {
            changes.Add("Description updated");
        }

        if (existingBug.Status != updatedBug.Status)
        {
            changes.Add($"Status changed from '{existingBug.Status}' to '{updatedBug.Status}'");
        }

        if (existingBug.Severity != updatedBug.Severity)
        {
            changes.Add($"Severity changed from '{existingBug.Severity}' to '{updatedBug.Severity}'");
        }

        if (existingBug.ProjectId != updatedBug.ProjectId)
        {
            var fromProject = existingBug.ProjectId?.ToString() ?? "None";
            var toProject = updatedBug.ProjectId?.ToString() ?? "None";
            changes.Add($"Project changed from '{fromProject}' to '{toProject}'");
        }

        if (!string.Equals(existingBug.AssignedToId, updatedBug.AssignedToId, StringComparison.Ordinal))
        {
            var previousAssignee = existingBug.AssignedTo?.Email ?? existingBug.AssignedToId ?? "Unassigned";
            var newAssignee = updatedBug.AssignedTo?.Email;

            if (string.IsNullOrEmpty(newAssignee) && !string.IsNullOrEmpty(updatedBug.AssignedToId))
            {
                var user = await _userManager.FindByIdAsync(updatedBug.AssignedToId);
                newAssignee = user?.Email ?? updatedBug.AssignedToId;
            }

            newAssignee ??= "Unassigned";

            changes.Add($"Reassigned from '{previousAssignee}' to '{newAssignee}'");
        }

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

    private async Task<List<string>> UpdateBugTagsAsync(BugReport existingBug, IEnumerable<int> tagIds)
    {
        var changes = new List<string>();
        var desiredIds = tagIds?.Distinct().ToList() ?? new List<int>();

        existingBug.Tags ??= new List<Tag>();

        var currentIds = existingBug.Tags.Select(t => t.Id).ToList();

        var tagsToRemove = existingBug.Tags
            .Where(t => !desiredIds.Contains(t.Id))
            .ToList();

        var removedNames = tagsToRemove.Select(t => t.Name).ToList();

        foreach (var tag in tagsToRemove)
        {
            existingBug.Tags.Remove(tag);
        }

        var tagsToAddIds = desiredIds.Except(currentIds).ToList();
        var tagsToAdd = new List<Tag>();

        if (tagsToAddIds.Count > 0)
        {
            tagsToAdd = await _context.Tags
                .Where(t => tagsToAddIds.Contains(t.Id))
                .ToListAsync();

            foreach (var tag in tagsToAdd)
            {
                existingBug.Tags.Add(tag);
            }
        }

        if (tagsToRemove.Any() || tagsToAdd.Any())
        {
            var addedNames = tagsToAdd.Select(t => t.Name).ToList();
            var messageParts = new List<string>();

            if (addedNames.Any())
            {
                messageParts.Add($"added [{string.Join(", ", addedNames)}]");
            }

            if (removedNames.Any())
            {
                messageParts.Add($"removed [{string.Join(", ", removedNames)}]");
            }

            changes.Add($"Tags updated {string.Join("; ", messageParts)}");
        }

        return changes;
    }

    private string? RemoveAttachments(BugReport existingBug, IEnumerable<int> attachmentIds)
    {
        existingBug.Attachments ??= new List<BugAttachment>();

        var ids = attachmentIds.Distinct().ToList();
        if (!ids.Any())
        {
            return null;
        }

        var attachmentsToRemove = existingBug.Attachments
            .Where(a => ids.Contains(a.Id))
            .ToList();

        if (!attachmentsToRemove.Any())
        {
            return null;
        }

        foreach (var attachment in attachmentsToRemove)
        {
            existingBug.Attachments.Remove(attachment);
        }

        _context.BugAttachments.RemoveRange(attachmentsToRemove);

        var removedNames = attachmentsToRemove
            .Select(a => string.IsNullOrWhiteSpace(a.FileName) ? $"Attachment #{a.Id}" : a.FileName)
            .ToList();

        return $"Attachments removed [{string.Join(", ", removedNames)}]";
    }
}

