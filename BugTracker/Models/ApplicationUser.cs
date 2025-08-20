using Microsoft.AspNetCore.Identity;

namespace BugTracker.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public virtual ICollection<BugReport> AssignedBugs { get; set; }
        public virtual ICollection<BugReport> CreatedBugs { get; set; }

        public ApplicationUser()
        {
            AssignedBugs = new List<BugReport>();
            CreatedBugs = new List<BugReport>();
        }
    }
}
