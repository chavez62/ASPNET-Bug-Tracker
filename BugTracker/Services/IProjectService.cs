using BugTracker.Models;
using BugTracker.Models.Enums;
using System.Security.Claims;

namespace BugTracker.Services
{
    public interface IProjectService
    {
        Task<IEnumerable<Project>> GetAllProjectsAsync();
        Task<IEnumerable<Project>> GetUserProjectsAsync(string userId);
        Task<Project> GetProjectByIdAsync(int id);
        Task<Project> CreateProjectAsync(Project project);
        Task UpdateProjectAsync(Project project);
        Task DeleteProjectAsync(int id);
        Task<bool> CanAccessProject(ClaimsPrincipal user, Project project);
        Task<Dictionary<Status, int>> GetProjectBugStatisticsAsync(int projectId);
        Task AddTeamMemberAsync(int projectId, string userId);
        Task RemoveTeamMemberAsync(int projectId, string userId);
    }
}