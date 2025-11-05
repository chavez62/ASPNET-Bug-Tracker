using BugTracker.Data;
using BugTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Services
{
	public class TagService : ITagService
	{
		private readonly ApplicationDbContext _context;
		private readonly ILogger<TagService> _logger;

		public TagService(ApplicationDbContext context, ILogger<TagService> logger)
		{
			_context = context;
			_logger = logger;
		}

		public async Task<List<Tag>> GetAllAsync()
		{
			return await _context.Tags
				.AsNoTracking()
				.OrderBy(t => t.Name)
				.ToListAsync();
		}

		public async Task<List<Tag>> GetAllWithBugReportsAsync()
		{
			return await _context.Tags
				.AsNoTracking()
				.AsSplitQuery()
				.Include(t => t.BugReports)
				.OrderBy(t => t.Name)
				.ToListAsync();
		}

		public async Task<Tag?> GetByIdWithBugReportsAsync(int id)
		{
			return await _context.Tags
				.AsNoTracking()
				.AsSplitQuery()
				.Include(t => t.BugReports)
				.ThenInclude(b => b.AssignedTo)
				.FirstOrDefaultAsync(t => t.Id == id);
		}

		public async Task<Tag?> FindByIdAsync(int id)
		{
			return await _context.Tags
				.AsNoTracking()
				.FirstOrDefaultAsync(t => t.Id == id);
		}

		public async Task<List<Tag>> GetTagsByIdsAsync(IEnumerable<int> tagIds)
		{
			var ids = tagIds?.Distinct().ToList() ?? new List<int>();
			if (ids.Count == 0) return new List<Tag>();

			return await _context.Tags
				.AsNoTracking()
				.Where(t => ids.Contains(t.Id))
				.OrderBy(t => t.Name)
				.ToListAsync();
		}

		public async Task<List<Tag>> SearchAsync(string term, int take = 10)
		{
			term ??= string.Empty;
			return await _context.Tags
				.AsNoTracking()
				.Where(t => string.IsNullOrEmpty(term) || t.Name.Contains(term))
				.OrderBy(t => t.Name)
				.Take(take)
				.ToListAsync();
		}

		public async Task<Tag> CreateAsync(Tag tag)
		{
			_context.Add(tag);
			await _context.SaveChangesAsync();
			return tag;
		}

		public async Task UpdateAsync(Tag tag)
		{
			_context.Update(tag);
			await _context.SaveChangesAsync();
		}

		public async Task<bool> DeleteAsync(int id)
		{
			var tag = await _context.Tags.FindAsync(id);
			if (tag == null) return false;
			_context.Tags.Remove(tag);
			await _context.SaveChangesAsync();
			return true;
		}
	}
}


