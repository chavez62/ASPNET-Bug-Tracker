using System.ComponentModel.DataAnnotations;
using BugTracker.Models.Enums;

namespace BugTracker.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Display(Name = "End Date")]
        public DateTime? EndDate { get; set; }

        [Required]
        [Display(Name = "Project Manager")]
        public string ManagerId { get; set; } = string.Empty;

        [Required]
        public ProjectStatus Status { get; set; }

        // Navigation properties
        public virtual ApplicationUser? Manager { get; set; }
        public virtual ICollection<ApplicationUser> TeamMembers { get; set; }
        public virtual ICollection<BugReport> BugReports { get; set; }

        public Project()
        {
            TeamMembers = new HashSet<ApplicationUser>();
            BugReports = new HashSet<BugReport>();
            StartDate = DateTime.Now;
            Status = ProjectStatus.Planning;
        }
    }
}