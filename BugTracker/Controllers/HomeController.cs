using BugTracker.Models;
using BugTracker.Models.Enums;
using BugTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BugTracker.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IBugService _bugService;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            IBugService bugService,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _bugService = bugService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    // Get current user
                    var currentUser = await _userManager.GetUserAsync(User);

                    // Set user's display name
                    if (currentUser != null && !string.IsNullOrEmpty(currentUser.FirstName))
                    {
                        ViewBag.UserDisplayName = $"{currentUser.FirstName} {currentUser.LastName}";
                    }
                    else
                    {
                        ViewBag.UserDisplayName = User.Identity.Name;
                    }

                    // Get summary statistics for the dashboard
                    var statusStats = await _bugService.GetBugStatusStatisticsAsync();
                    ViewBag.TotalBugs = statusStats.Values.Sum();
                    ViewBag.OpenBugs = statusStats.GetValueOrDefault(Status.Open);
                    ViewBag.InProgressBugs = statusStats.GetValueOrDefault(Status.InProgress);
                    ViewBag.ResolvedBugs = statusStats.GetValueOrDefault(Status.Resolved);

                    // Get additional dashboard data
                    ViewBag.RecentBugs = await _bugService.GetRecentBugsAsync(5);
                    ViewBag.TrendPercentage = await _bugService.GetTrendPercentageAsync();

                    ViewBag.CriticalBugs = await _bugService.GetBugCountBySeverityAsync(Severity.Critical);
                    ViewBag.AssignedBugs = await _bugService.GetAssignedBugsCountAsync(User.Identity.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading dashboard data");
                    ViewBag.UserDisplayName = User.Identity?.Name ?? "User";
                    ViewBag.TotalBugs = 0;
                    ViewBag.OpenBugs = 0;
                    ViewBag.InProgressBugs = 0;
                    ViewBag.ResolvedBugs = 0;
                    ViewBag.RecentBugs = Enumerable.Empty<BugReport>();
                    ViewBag.TrendPercentage = 0;
                    ViewBag.CriticalBugs = 0;
                    ViewBag.AssignedBugs = 0;

                    TempData["ErrorMessage"] = "There was an error loading the dashboard data. Please try again later.";
                }
            }

            return View();
        }
    }
}
