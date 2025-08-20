using BugTracker.Models;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace BugTracker.Services
{
    public interface IBugValidationService
    {
        Task<ValidationResult> ValidateBugReportAsync(BugReport bugReport);
        Task<ValidationResult> ValidateFilesAsync(List<IFormFile> files);
        string GetClientValidationRules();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public Dictionary<string, string[]> Errors { get; set; } = new();

        public void AddError(string key, string error)
        {
            if (!Errors.ContainsKey(key))
            {
                Errors[key] = new string[] { error };
            }
            else
            {
                var errors = Errors[key].ToList();
                errors.Add(error);
                Errors[key] = errors.ToArray();
            }
        }
    }

    public class BugValidationService : IBugValidationService
    {
        private const int MaxTitleLength = 200;
        private const int MaxDescriptionLength = 5000;
        private const int MaxFileCount = 5;
        private const int MaxTotalFileSize = 20 * 1024 * 1024; // 20MB
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BugValidationService> _logger;

        public BugValidationService(
            UserManager<ApplicationUser> userManager,
            ILogger<BugValidationService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateBugReportAsync(BugReport bugReport)
        {
            var result = new ValidationResult { IsValid = true };

            if (bugReport == null)
            {
                result.IsValid = false;
                result.AddError("", "Bug report cannot be null");
                return result;
            }

            // Trim strings
            bugReport.Title = bugReport.Title?.Trim();
            bugReport.Description = bugReport.Description?.Trim();

            // Validate Title
            if (string.IsNullOrWhiteSpace(bugReport.Title))
            {
                result.IsValid = false;
                result.AddError(nameof(bugReport.Title), "Title is required");
            }
            else if (bugReport.Title.Length > MaxTitleLength)
            {
                result.IsValid = false;
                result.AddError(nameof(bugReport.Title),
                    $"Title cannot exceed {MaxTitleLength} characters");
            }

            // Validate Description
            if (string.IsNullOrWhiteSpace(bugReport.Description))
            {
                result.IsValid = false;
                result.AddError(nameof(bugReport.Description), "Description is required");
            }
            else if (bugReport.Description.Length > MaxDescriptionLength)
            {
                result.IsValid = false;
                result.AddError(nameof(bugReport.Description),
                    $"Description cannot exceed {MaxDescriptionLength} characters");
            }

            // Validate AssignedTo user
            if (string.IsNullOrEmpty(bugReport.AssignedToId))
            {
                result.IsValid = false;
                result.AddError(nameof(bugReport.AssignedToId),
                    "Please select a user to assign this bug to");
            }
            else
            {
                var assignedUser = await _userManager.FindByIdAsync(bugReport.AssignedToId);
                if (assignedUser == null)
                {
                    result.IsValid = false;
                    result.AddError(nameof(bugReport.AssignedToId),
                        "Selected user does not exist");
                }
            }

            return result;
        }

        public async Task<ValidationResult> ValidateFilesAsync(List<IFormFile> files)
        {
            var result = new ValidationResult { IsValid = true };

            if (files == null || !files.Any())
                return result;

            // Check file count
            if (files.Count > MaxFileCount)
            {
                result.IsValid = false;
                result.AddError("files", $"Maximum {MaxFileCount} files allowed");
                return result;
            }

            // Check total size
            var totalSize = files.Sum(f => f.Length);
            if (totalSize > MaxTotalFileSize)
            {
                result.IsValid = false;
                result.AddError("files",
                    $"Total file size exceeds {MaxTotalFileSize / 1024 / 1024}MB");
                return result;
            }

            foreach (var file in files)
            {
                var fileResult = await ValidateSingleFileAsync(file);
                if (!fileResult.IsValid)
                {
                    result.IsValid = false;
                    foreach (var error in fileResult.Errors)
                    {
                        result.AddError(error.Key, error.Value.First());
                    }
                }
            }

            return result;
        }

        private async Task<ValidationResult> ValidateSingleFileAsync(IFormFile file)
        {
            var result = new ValidationResult { IsValid = true };

            // Check if file is empty
            if (file.Length == 0)
            {
                result.IsValid = false;
                result.AddError("file", $"File '{file.FileName}' is empty");
                return result;
            }

            // Check file size
            const int maxFileSize = 5 * 1024 * 1024; // 5MB
            if (file.Length > maxFileSize)
            {
                result.IsValid = false;
                result.AddError("file",
                    $"File '{file.FileName}' exceeds maximum size of 5MB");
                return result;
            }

            // Check file extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".txt", ".log" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                result.IsValid = false;
                result.AddError("file", $"File type '{extension}' is not allowed");
                return result;
            }

            return result;
        }

        public string GetClientValidationRules()
        {
            var rules = new
            {
                maxTitleLength = MaxTitleLength,
                maxDescriptionLength = MaxDescriptionLength,
                maxFileCount = MaxFileCount,
                maxTotalFileSize = MaxTotalFileSize,
                allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".txt", ".log" }
            };

            return JsonSerializer.Serialize(rules);
        }
    }
}