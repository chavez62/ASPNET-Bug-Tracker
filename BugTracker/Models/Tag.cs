using System.ComponentModel.DataAnnotations;

namespace BugTracker.Models
{
    public class Tag
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(7)]
        public string Color { get; set; } = "#6c757d";

        public virtual ICollection<BugReport> BugReports { get; set; }

        public Tag()
        {
            BugReports = new HashSet<BugReport>();
        }
    }
}