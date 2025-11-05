using BugTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BugTracker.Services;

namespace BugTracker.Controllers
{
	[Authorize]
	public class TagsController : Controller
	{
		private readonly ITagService _tagService;
		private readonly ILogger<TagsController> _logger;

		public TagsController(ITagService tagService, ILogger<TagsController> logger)
		{
			_tagService = tagService;
			_logger = logger;
		}

		public async Task<IActionResult> Index()
		{
			var tags = await _tagService.GetAllWithBugReportsAsync();

			return View(tags);
		}

		public async Task<IActionResult> Details(int? id)
		{
			if (id == null)
			{
				return NotFound();
			}

			var tag = await _tagService.GetByIdWithBugReportsAsync(id.Value);

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
					await _tagService.CreateAsync(tag);
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

			var tag = await _tagService.FindByIdAsync(id.Value);
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
					await _tagService.UpdateAsync(tag);
					TempData["SuccessMessage"] = $"Tag '{tag.Name}' updated successfully.";
					return RedirectToAction(nameof(Index));
				}
				catch (DbUpdateConcurrencyException)
				{
					if (await _tagService.FindByIdAsync(tag.Id) == null)
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

			var tag = await _tagService.GetByIdWithBugReportsAsync(id.Value);

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
				var deleted = await _tagService.DeleteAsync(id);
				if (deleted)
				{
					TempData["SuccessMessage"] = "Tag deleted successfully.";
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
			var tags = await _tagService.SearchAsync(term ?? string.Empty, 10);
			var result = tags
				.Select(t => new { id = t.Id, text = t.Name, color = t.Color })
				.ToList();

			return Json(result);
		}
	}
}