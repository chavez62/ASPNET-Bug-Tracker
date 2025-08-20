namespace BugTracker.Models
{
    public class BugAttachment
    {
        public int Id { get; set; }
        public int BugReportId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }
        public virtual BugReport BugReport { get; set; }
    }
}
