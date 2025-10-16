namespace BugTracker.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public int BugReportId { get; set; }
        public string? UserId { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }

        public virtual BugReport? BugReport { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
