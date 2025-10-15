using BugTracker.Models;
using BugTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            IProjectService projectService,
            UserManager<ApplicationUser> userManager,
            ILogger<ProjectsController> logger)
        {
            _projectService = projectService;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var projects = User.IsInRole("Admin")
                    ? await _projectService.GetAllProjectsAsync()
                    : await _projectService.GetUserProjectsAsync(currentUser.Id ?? string.Empty);

                return View(projects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving projects");
                TempData["Error"] = "An error occurred while retrieving projects.";
                return View(Enumerable.Empty<Project>());
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                    return NotFound();

                if (!await _projectService.CanAccessProject(User, project))
                    return Forbid();

                ViewBag.BugStatistics = await _projectService.GetProjectBugStatisticsAsync(id);
                return View(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving project details: {ProjectId}", id);
                TempData["Error"] = "An error occurred while retrieving project details.";
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await PrepareProjectViewData();
            return View(new Project { StartDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Project project)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    await _projectService.CreateProjectAsync(project);
                    TempData["Success"] = "Project created successfully.";
                    return RedirectToAction(nameof(Details), new { id = project.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project");
                ModelState.AddModelError("", "An error occurred while creating the project.");
            }

            await PrepareProjectViewData();
            return View(project);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var project = await _projectService.GetProjectByIdAsync(id);
                if (project == null)
                    return NotFound();

                await PrepareProjectViewData();
                return View(project);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing edit form for project {ProjectId}", id);
                TempData["Error"] = "An error occurred while preparing the edit form.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Project project)
        {
            if (id != project.Id)
                return NotFound();

            try
            {
                if (ModelState.IsValid)
                {
                    await _projectService.UpdateProjectAsync(project);
                    TempData["Success"] = "Project updated successfully.";
                    return RedirectToAction(nameof(Details), new { id = project.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {ProjectId}", id);
                ModelState.AddModelError("", "An error occurred while updating the project.");
            }

            await PrepareProjectViewData();
            return View(project);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _projectService.DeleteProjectAsync(id);
                TempData["Success"] = "Project deleted successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}", id);
                TempData["Error"] = "An error occurred while deleting the project.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddTeamMember(int projectId, string userId)
        {
            try
            {
                await _projectService.AddTeamMemberAsync(projectId, userId);
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding team member to project {ProjectId}", projectId);
                TempData["Error"] = "An error occurred while adding the team member.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveTeamMember(int projectId, string userId)
        {
            try
            {
                await _projectService.RemoveTeamMemberAsync(projectId, userId);
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing team member from project {ProjectId}", projectId);
                TempData["Error"] = "An error occurred while removing the team member.";
                return RedirectToAction(nameof(Details), new { id = projectId });
            }
        }

        private async Task PrepareProjectViewData()
        {
            try
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

                ViewBag.Users = new SelectList(users, "Value", "Text");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing project view data");
                throw;
            }
        }
    }
}