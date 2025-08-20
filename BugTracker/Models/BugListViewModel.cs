using BugTracker.Models.Enums;
using System.Collections.Generic;

namespace BugTracker.Models
{
    public class BugListViewModel
    {
        public IEnumerable<BugReport> Bugs { get; set; }
        public BugSearchModel SearchModel { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }

        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public class BugSearchModel
        {
            public string? SearchTerm { get; set; }
            public Status? Status { get; set; }
            public Severity? Severity { get; set; }
            public string? AssignedToId { get; set; }
            public DateTime? DateFrom { get; set; }
            public DateTime? DateTo { get; set; }
            public List<string>? SelectedTags { get; set; }
            public string? SortBy { get; set; }
            public bool SortDescending { get; set; }
            public int PageSize { get; set; } = 10;
            public int Page { get; set; } = 1;
        }
    }
}