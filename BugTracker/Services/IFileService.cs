using BugTracker.Models;

namespace BugTracker.Services
{
    public interface IFileService
    {
        Task<BugAttachment> SaveAttachmentAsync(IFormFile file, int bugReportId);
        Task DeleteAttachmentAsync(int attachmentId);
        Task<BugAttachment> GetAttachmentAsync(int attachmentId);
        string GetFilePath(string fileName);
    }
}
