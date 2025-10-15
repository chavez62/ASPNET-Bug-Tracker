using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Models.Enums;
using BugTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static BugTracker.Models.BugListViewModel;

namespace BugTracker.Controllers
{
    [Authorize]
    public class BugReportsController : BaseApiController
    {
        private readonly IBugService _bugService;
        private readonly IBugValidationService _validationService;
        private readonly IActivityLogService _activityLogService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BugReportsController> _logger;
        private readonly ApplicationDbContext _context;

        public BugReportsController(
            IBugService bugService,
            IBugValidationService validationService,
            IActivityLogService activityLogService,
            UserManager<ApplicationUser> userManager,
            ILogger<BugReportsController> logger,
            ApplicationDbContext context)
        {
            _bugService = bugService;
            _validationService = validationService;
            _activityLogService = activityLogService;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index(BugSearchModel searchModel = null, int page = 1)
        {
            try
            {
                searchModel ??= new BugSearchModel();
                var pageSize = searchModel.PageSize;
                var result = await _bugService.SearchBugReportsAsync(searchModel, page, pageSize);
                await PrepareViewBags();

                return View(new BugListViewModel
                {
                    Bugs = result.Bugs,
                    SearchModel = searchModel ?? new(),
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = result.TotalCount,
                    TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> Create(int? projectId)
        {
            try
            {
                var currentUserId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(currentUserId))
                    return Challenge();

                var newBug = new BugReport
                {
                    Status = Status.Open,
                    CreatedById = currentUserId,
                    CreatedDate = DateTime.UtcNow,
                    ProjectId = projectId
                };

                await PrepareViewBags();
                ViewBag.ValidationRules = _validationService.GetClientValidationRules();

                // Initialize empty selected tags for new bug
                ViewBag.SelectedTagIds = new List<int>();

                return View(newBug);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Description,Severity,Status,AssignedToId,ProjectId")] BugReport bugReport, List<IFormFile> files, List<int> SelectedTagIds)
        {
            try
            {
                // Validate bug report and files
                var validationResult = await _validationService.ValidateBugReportAsync(bugReport);
                if (files?.Any() == true)
                {
                    var fileValidation = await _validationService.ValidateFilesAsync(files);
                    if (!fileValidation.IsValid)
                    {
                        foreach (var error in fileValidation.Errors)
                        {
                            ModelState.AddModelError(error.Key, error.Value.First());
                        }
                    }
                }

                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        ModelState.AddModelError(error.Key, error.Value.First());
                    }

                    await PrepareViewBags();
                    ViewBag.SelectedTagIds = SelectedTagIds ?? new List<int>();
                    return View(bugReport);
                }

                // Set creation metadata
                bugReport.CreatedById = _userManager.GetUserId(User);
                bugReport.CreatedDate = DateTime.UtcNow;

                // Handle tag associations
                if (SelectedTagIds != null && SelectedTagIds.Any())
                {
                    var tags = await _context.Tags
                        .Where(t => SelectedTagIds.Contains(t.Id))
                        .ToListAsync();
                    bugReport.Tags = tags;
                }

                // Create bug report
                var createdBug = await _bugService.CreateBugReportAsync(bugReport);

                // Log activity
                await _activityLogService.LogActivityAsync(
                    createdBug.Id,
                    bugReport.CreatedById,
                    "Created",
                    $"Bug report created{(bugReport.ProjectId.HasValue ? $" for project {bugReport.ProjectId}" : "")}"
                );

                return RedirectToAction(nameof(Details), new { id = createdBug.Id });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var bugReport = await _bugService.GetBugReportAsync(id);
                if (bugReport == null)
                    return NotFound();

                if (!await _bugService.CanAccessBugReport(User, bugReport))
                    return Forbid();

                return View(bugReport);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var bugReport = await _bugService.GetBugReportAsync(id);
                if (bugReport == null)
                    return NotFound();

                if (!await _bugService.CanAccessBugReport(User, bugReport))
                    return Forbid();

                await PrepareViewBags();
                ViewBag.ValidationRules = _validationService.GetClientValidationRules();

                // Set selected tags for the dropdown
                ViewBag.SelectedTagIds = bugReport.Tags.Select(t => t.Id).ToList();

                return View(bugReport);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Severity,Status,AssignedToId,CreatedById,CreatedDate,ProjectId")] BugReport bugReport, List<int> SelectedTagIds)
        {
            if (id != bugReport.Id)
                return NotFound();

            try
            {
                var validationResult = await _validationService.ValidateBugReportAsync(bugReport);
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        ModelState.AddModelError(error.Key, error.Value.First());
                    }

                    await PrepareViewBags();
                    ViewBag.SelectedTagIds = SelectedTagIds ?? new List<int>();
                    return View(bugReport);
                }

                // Handle tag associations
                var existingBug = await _context.BugReports
                    .Include(b => b.Tags)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (existingBug != null)
                {
                    // Clear existing tags
                    existingBug.Tags.Clear();

                    // Add selected tags
                    if (SelectedTagIds != null && SelectedTagIds.Any())
                    {
                        var tags = await _context.Tags
                            .Where(t => SelectedTagIds.Contains(t.Id))
                            .ToListAsync();
                        foreach (var tag in tags)
                        {
                            existingBug.Tags.Add(tag);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                var editorUserId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(editorUserId))
                {
                    return Challenge();
                }

                await _bugService.UpdateBugReportAsync(bugReport, editorUserId);

                return RedirectToAction(nameof(Details), new { id = bugReport.Id });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _bugService.DeleteBugReportAsync(id);
                TempData["Success"] = "Bug report deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int bugId, string comment)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(comment))
                    return JsonError("Comment cannot be empty");

                await _activityLogService.LogActivityAsync(
                    bugId,
                    _userManager.GetUserId(User),
                    "Commented",
                    comment.Trim()
                );

                return JsonSuccess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to bug {BugId}", bugId);
                return JsonError("Error adding comment");
            }
        }

        private async Task PrepareViewBags()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = !string.IsNullOrEmpty(u.FirstName) && !string.IsNullOrEmpty(u.LastName)
                        ? $"{u.FirstName} {u.LastName} ({u.Email})"
                        : u.Email
                })
                .ToListAsync();

            var tags = await _context.Tags
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                })
                .ToListAsync();

            ViewBag.Users = new SelectList(users, "Value", "Text");
            ViewBag.Tags = new SelectList(tags, "Value", "Text");
            ViewBag.Statuses = new SelectList(Enum.GetValues<Status>());
            ViewBag.Severities = new SelectList(Enum.GetValues<Severity>());
        }
    }
}