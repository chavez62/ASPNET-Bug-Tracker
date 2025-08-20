using System.ComponentModel.DataAnnotations;
using BugTracker.Models.Enums;

namespace BugTracker.Models
{
    public class BugReport
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        public string Description { get; set; } = string.Empty;

        [Required]
        public Severity Severity { get; set; }

        [Required]
        public Status Status { get; set; }

        [Required(ErrorMessage = "Please select a user to assign this bug to")]
        public string AssignedToId { get; set; } = string.Empty;
        public virtual ApplicationUser? AssignedTo { get; set; }

        public string CreatedById { get; set; } = string.Empty;
        public virtual ApplicationUser? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }

        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        public virtual ICollection<BugAttachment> Attachments { get; set; }
        public virtual ICollection<ActivityLog> ActivityLogs { get; set; }
        public virtual ICollection<Tag> Tags { get; set; }

        public BugReport()
        {
            Attachments = new HashSet<BugAttachment>();
            ActivityLogs = new HashSet<ActivityLog>();
            Tags = new HashSet<Tag>();
        }
    }
}