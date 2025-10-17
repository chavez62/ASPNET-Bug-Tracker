using Microsoft.AspNetCore.Mvc;

namespace BugTracker.Controllers
{
    public abstract class BaseApiController : Controller
    {
        protected IActionResult HandleError(Exception ex, string friendlyMessage = null)
        {
            var message = friendlyMessage ?? "An error occurred while processing your request.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message });
            }

            TempData["Error"] = message;
            return RedirectToAction("Index");
        }

        protected IActionResult JsonSuccess(string? message = null)
        {
            return Json(new { success = true, message });
        }

        protected IActionResult JsonError(string message)
        {
            return Json(new { success = false, message });
        }
    }
}