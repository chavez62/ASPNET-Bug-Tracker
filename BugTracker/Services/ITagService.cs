using BugTracker.Models;

namespace BugTracker.Services
{
	public interface ITagService
	{
		Task<List<Tag>> GetAllAsync();
		Task<List<Tag>> GetAllWithBugReportsAsync();
		Task<Tag?> GetByIdWithBugReportsAsync(int id);
		Task<Tag?> FindByIdAsync(int id);
		Task<List<Tag>> GetTagsByIdsAsync(IEnumerable<int> tagIds);
		Task<List<Tag>> SearchAsync(string term, int take = 10);
		Task<Tag> CreateAsync(Tag tag);
		Task UpdateAsync(Tag tag);
		Task<bool> DeleteAsync(int id);
	}
}


