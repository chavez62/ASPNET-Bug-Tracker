using BugTracker.Data;
using BugTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Controllers
{
    [Authorize]
    public class TagsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TagsController> _logger;

        public TagsController(ApplicationDbContext context, ILogger<TagsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var tags = await _context.Tags
                .Include(t => t.BugReports)
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(tags);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tag = await _context.Tags
                .Include(t => t.BugReports)
                .ThenInclude(b => b.AssignedTo)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tag == null)
            {
                return NotFound();
            }

            return View(tag);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Color")] Tag tag)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(tag);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Tag '{tag.Name}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
                {
                    ModelState.AddModelError("Name", "A tag with this name already exists.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating tag");
                    ModelState.AddModelError("", "An error occurred while creating the tag.");
                }
            }
            return View(tag);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tag = await _context.Tags.FindAsync(id);
            if (tag == null)
            {
                return NotFound();
            }
            return View(tag);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Color")] Tag tag)
        {
            if (id != tag.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tag);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Tag '{tag.Name}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TagExists(tag.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
                {
                    ModelState.AddModelError("Name", "A tag with this name already exists.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating tag {TagId}", id);
                    ModelState.AddModelError("", "An error occurred while updating the tag.");
                }
            }
            return View(tag);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tag = await _context.Tags
                .Include(t => t.BugReports)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (tag == null)
            {
                return NotFound();
            }

            return View(tag);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var tag = await _context.Tags.FindAsync(id);
                if (tag != null)
                {
                    _context.Tags.Remove(tag);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Tag '{tag.Name}' deleted successfully.";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tag {TagId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the tag.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTags(string term = "")
        {
            var tags = await _context.Tags
                .Where(t => string.IsNullOrEmpty(term) || t.Name.Contains(term))
                .Select(t => new { id = t.Id, text = t.Name, color = t.Color })
                .Take(10)
                .ToListAsync();

            return Json(tags);
        }

        private bool TagExists(int id)
        {
            return _context.Tags.Any(e => e.Id == id);
        }
    }
}