using BugTracker.Models;
using BugTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BugTracker.Controllers
{
    [Authorize]
    public class BugAttachmentsController : BaseApiController
    {
        private readonly IFileService _fileService;
        private readonly IActivityLogService _activityLogService;
        private readonly IBugService _bugService;
        private readonly ILogger<BugAttachmentsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public BugAttachmentsController(
            IFileService fileService,
            IActivityLogService activityLogService,
            IBugService bugService,
            UserManager<ApplicationUser> userManager,
            ILogger<BugAttachmentsController> logger)
        {
            _fileService = fileService;
            _activityLogService = activityLogService;
            _bugService = bugService;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAttachment(int id, int bugId)
        {
            try
            {
                var attachment = await _fileService.GetAttachmentAsync(id);
                if (attachment == null)
                {
                    return NotFound();
                }

                if (attachment.BugReportId != bugId)
                {
                    _logger.LogWarning("Attempted to delete attachment {AttachmentId} from wrong bug report {BugId}",
                        id, bugId);
                    return BadRequest();
                }

                await _fileService.DeleteAttachmentAsync(id);
                await _activityLogService.LogActivityAsync(
                    bugId,
                    _userManager.GetUserId(User),
                    "DeletedAttachment",
                    "Deleted attachment"
                );

                return JsonSuccess("Attachment deleted successfully");
            }
            catch (Exception ex)
            {
                return HandleError(ex, "Error deleting attachment");
            }
        }
    }
}