using BugTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[AutoValidateAntiforgeryToken]
public class SecureFileController : Controller
{
    private readonly IFileService _fileService;
    private readonly IBugService _bugService;
    private readonly ILogger<SecureFileController> _logger;

    public SecureFileController(
        IFileService fileService,
        IBugService bugService,
        ILogger<SecureFileController> logger)
    {
        _fileService = fileService;
        _bugService = bugService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFile(int attachmentId)
    {
        try
        {
            // Get attachment with null check
            var attachment = await _fileService.GetAttachmentAsync(attachmentId);
            if (attachment == null)
            {
                _logger.LogWarning("Attachment not found: {AttachmentId}", attachmentId);
                return NotFound();
            }

            // Get associated bug report with null check
            var bugReport = await _bugService.GetBugReportAsync(attachment.BugReportId);
            if (bugReport == null)
            {
                _logger.LogWarning("Bug report not found for attachment: {AttachmentId}", attachmentId);
                return NotFound();
            }

            // Check authorization
            if (!await _bugService.CanAccessBugReport(User, bugReport))
            {
                _logger.LogWarning("Unauthorized access attempt to attachment: {AttachmentId} by user: {User}",
                    attachmentId, User.Identity?.Name);
                return Forbid();
            }

            // Validate and sanitize file path
            var filePath = _fileService.GetFilePath(attachment.FilePath);
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("File not found for attachment: {AttachmentId}", attachmentId);
                return NotFound();
            }

            // Set cache control headers for security
            Response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            // Set Content-Disposition header to prevent XSS
            var contentDisposition = new System.Net.Mime.ContentDisposition
            {
                FileName = attachment.FileName,
                Inline = attachment.ContentType.StartsWith("image/") // Allow inline display for images
            };

            // Return file with content type
            return PhysicalFile(filePath, attachment.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file for attachment: {AttachmentId}", attachmentId);
            return StatusCode(500, "An error occurred while retrieving the file.");
        }
    }
}