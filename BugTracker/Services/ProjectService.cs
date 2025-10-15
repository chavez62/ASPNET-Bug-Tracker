using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BugTracker.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ProjectService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IEnumerable<Project>> GetAllProjectsAsync()
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Manager)
                .Include(p => p.TeamMembers)
                .Include(p => p.BugReports)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Project>> GetUserProjectsAsync(string userId)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Manager)
                .Include(p => p.TeamMembers)
                .Include(p => p.BugReports)
                .Where(p => p.ManagerId == userId || p.TeamMembers.Any(m => m.Id == userId))
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
        }

        public async Task<Project> GetProjectByIdAsync(int id)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Manager)
                .Include(p => p.TeamMembers)
                .Include(p => p.BugReports)
                    .ThenInclude(b => b.AssignedTo)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Project> CreateProjectAsync(Project project)
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
            return project;
        }

        public async Task UpdateProjectAsync(Project project)
        {
            var existingProject = await _context.Projects
                .Include(p => p.TeamMembers)
                .FirstOrDefaultAsync(p => p.Id == project.Id);

            if (existingProject == null)
                throw new InvalidOperationException($"Project with ID {project.Id} not found");

            // Update properties
            existingProject.Name = project.Name;
            existingProject.Description = project.Description;
            existingProject.StartDate = project.StartDate;
            existingProject.EndDate = project.EndDate;
            existingProject.ManagerId = project.ManagerId;
            existingProject.Status = project.Status;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteProjectAsync(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project != null)
            {
                _context.Projects.Remove(project);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> CanAccessProject(ClaimsPrincipal user, Project project)
        {
            if (user == null || project == null)
                return false;

            // Admin can access all projects
            if (user.IsInRole("Admin"))
                return true;

            var currentUser = await _userManager.GetUserAsync(user);
            if (currentUser == null)
                return false;

            // User can access if they are the project manager or a team member
            return project.ManagerId == currentUser.Id ||
                   project.TeamMembers.Any(m => m.Id == currentUser.Id);
        }

        public async Task<Dictionary<Status, int>> GetProjectBugStatisticsAsync(int projectId)
        {
            var bugs = await _context.BugReports
                .AsNoTracking()
                .Where(b => b.ProjectId == projectId)
                .ToListAsync();

            var stats = new Dictionary<Status, int>();

            // Initialize counts for all statuses to 0
            foreach (Status status in Enum.GetValues(typeof(Status)))
            {
                stats[status] = bugs.Count(b => b.Status == status);
            }

            return stats;
        }

        public async Task AddTeamMemberAsync(int projectId, string userId)
        {
            var project = await _context.Projects
                .Include(p => p.TeamMembers)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new InvalidOperationException($"Project with ID {projectId} not found");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new InvalidOperationException($"User with ID {userId} not found");

            if (!project.TeamMembers.Any(m => m.Id == userId))
            {
                project.TeamMembers.Add(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task RemoveTeamMemberAsync(int projectId, string userId)
        {
            var project = await _context.Projects
                .Include(p => p.TeamMembers)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new InvalidOperationException($"Project with ID {projectId} not found");

            var user = project.TeamMembers.FirstOrDefault(m => m.Id == userId);
            if (user != null)
            {
                project.TeamMembers.Remove(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}