using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Services;
using System.Security;
using System.Security.Cryptography;

public class FileService : IFileService
{
    private readonly ApplicationDbContext _context;
    private readonly string _uploadsDirectory;
    private readonly Dictionary<string, string[]> _allowedTypes = new()
    {
        { ".jpg", new[] { "image/jpeg" } },
        { ".jpeg", new[] { "image/jpeg" } },
        { ".png", new[] { "image/png" } },
        { ".pdf", new[] { "application/pdf" } },
        { ".txt", new[] { "text/plain" } },
        { ".log", new[] { "text/plain" } }
    };
    private const long _maxFileSize = 5 * 1024 * 1024; // 5MB
    private readonly ILogger<FileService> _logger;

    public FileService(
    ApplicationDbContext context,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<FileService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            // Get configured upload path or use default within wwwroot
            var configPath = configuration.GetValue<string>("FileStorage:Path");

            if (string.IsNullOrEmpty(configPath))
            {
                // Use wwwroot/uploads as default path
                _uploadsDirectory = Path.Combine(environment.WebRootPath, "uploads");
            }
            else
            {
                // If custom path is provided, make it absolute
                _uploadsDirectory = Path.GetFullPath(configPath);
            }

            // Create the directory if it doesn't exist
            EnsureSecureDirectory();

            _logger.LogInformation("File storage initialized at: {Path}", _uploadsDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize file storage");
            throw;
        }
    }

    private void EnsureSecureDirectory()
    {
        try
        {
            if (!Directory.Exists(_uploadsDirectory))
            {
                Directory.CreateDirectory(_uploadsDirectory);

                // Set restrictive permissions on the directory
                var directoryInfo = new DirectoryInfo(_uploadsDirectory);
                var directorySecurity = directoryInfo.GetAccessControl();
                directorySecurity.SetAccessRuleProtection(true, false); // Enable inheritance protection
                directoryInfo.SetAccessControl(directorySecurity);

                // Create web.config for IIS security (if running on Windows/IIS)
                var webConfigPath = Path.Combine(_uploadsDirectory, "web.config");
                if (!File.Exists(webConfigPath))
                {
                    var webConfigContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                        <configuration>
                            <system.webServer>
                                <security>
                                    <requestFiltering>
                                        <fileExtensions allowUnlisted=""false"">
                                            <add fileExtension="".jpg"" allowed=""true"" />
                                            <add fileExtension="".jpeg"" allowed=""true"" />
                                            <add fileExtension="".png"" allowed=""true"" />
                                            <add fileExtension="".pdf"" allowed=""true"" />
                                            <add fileExtension="".txt"" allowed=""true"" />
                                            <add fileExtension="".log"" allowed=""true"" />
                                        </fileExtensions>
                                    </requestFiltering>
                                </security>
                            </system.webServer>
                        </configuration>";
                    File.WriteAllText(webConfigPath, webConfigContent);
                }
            }

            // Verify directory is accessible
            TestDirectoryAccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/secure uploads directory: {Path}", _uploadsDirectory);
            throw new InvalidOperationException($"Failed to initialize upload directory: {ex.Message}", ex);
        }
    }

    private void TestDirectoryAccess()
    {
        try
        {
            // Test file path
            var testPath = Path.Combine(_uploadsDirectory, $"test_{Guid.NewGuid()}.txt");

            // Try to create and delete a test file
            File.WriteAllText(testPath, "test");
            File.Delete(testPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Upload directory is not accessible: {ex.Message}", ex);
        }
    }

    public async Task<BugAttachment> SaveAttachmentAsync(IFormFile file, int bugReportId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        if (file.Length > _maxFileSize)
            throw new ArgumentException($"File size exceeds maximum limit of {_maxFileSize / 1024 / 1024}MB");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        // Verify file type is allowed
        if (!_allowedTypes.ContainsKey(extension))
            throw new ArgumentException($"File extension {extension} is not allowed");

        // Validate content type matches extension
        var contentType = file.ContentType.ToLower();
        if (!_allowedTypes[extension].Contains(contentType))
            throw new ArgumentException($"Invalid content type {contentType} for extension {extension}");

        // Generate safe filename with GUID and random component
        var randomComponent = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var safeFileName = $"{Guid.NewGuid()}_{randomComponent}{extension}";
        var filePath = Path.Combine(_uploadsDirectory, safeFileName);

        // Validate final path is still within uploads directory
        if (!IsPathWithinUploads(filePath))
            throw new SecurityException("Invalid file path");

        try
        {
            // Save file with exclusive access
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);

                // Additional validation on file contents
                if (!await IsValidFileContentAsync(memoryStream, extension))
                    throw new ArgumentException("File content does not match expected format");

                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(fileStream);
            }

            // Set restrictive file permissions
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            var attachment = new BugAttachment
            {
                BugReportId = bugReportId,
                FileName = Path.GetFileName(file.FileName).Replace(":", "_"), // Sanitize filename
                FilePath = safeFileName,
                ContentType = contentType,
                FileSize = file.Length,
                UploadDate = DateTime.UtcNow
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.BugAttachments.Add(attachment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return attachment;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                await DeleteFileAsync(filePath);
                _logger.LogError(ex, "Failed to save attachment record");
                throw;
            }
        }
        catch (Exception ex)
        {
            await DeleteFileAsync(filePath);
            _logger.LogError(ex, "Failed to save attachment file");
            throw;
        }
    }

    private async Task<bool> IsValidFileContentAsync(MemoryStream stream, string extension)
    {
        if (stream.Length < 4) return false;

        stream.Position = 0;
        var signature = new byte[8];
        await stream.ReadAsync(signature, 0, Math.Min(8, (int)stream.Length));

        return extension.ToLower() switch
        {
            ".jpg" or ".jpeg" when stream.Length >= 2 =>
                signature[0] == 0xFF && signature[1] == 0xD8, // JPEG signature

            ".png" when stream.Length >= 8 =>
                signature[0] == 0x89 && signature[1] == 0x50 &&
                signature[2] == 0x4E && signature[3] == 0x47 &&
                signature[4] == 0x0D && signature[5] == 0x0A &&
                signature[6] == 0x1A && signature[7] == 0x0A, // PNG signature

            ".pdf" when stream.Length >= 4 =>
                signature[0] == 0x25 && signature[1] == 0x50 &&
                signature[2] == 0x44 && signature[3] == 0x46, // PDF signature

            // Text files need content analysis
            ".txt" or ".log" => await IsValidTextFileAsync(stream),

            _ => false
        };
    }

    private async Task<bool> IsValidTextFileAsync(MemoryStream stream)
    {
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            var sample = await reader.ReadToEndAsync();

            // Check for binary content or invalid characters
            return !ContainsBinaryContent(sample);
        }
        catch
        {
            return false;
        }
    }

    private bool ContainsBinaryContent(string content)
    {
        // Check for null bytes and other indicators of binary content
        return content.Any(c => c == '\0' || (c < 32 && c != '\r' && c != '\n' && c != '\t'));
    }

    private bool IsPathWithinUploads(string path)
    {
        var fullUploadsPath = Path.GetFullPath(_uploadsDirectory);
        var targetFullPath = Path.GetFullPath(path);

        if (OperatingSystem.IsWindows())
        {
            return targetFullPath.StartsWith(fullUploadsPath, StringComparison.OrdinalIgnoreCase);
        }

        return targetFullPath.StartsWith(fullUploadsPath, StringComparison.Ordinal);
    }

    public async Task<BugAttachment> GetAttachmentAsync(int attachmentId)
    {
        return await _context.BugAttachments.FindAsync(attachmentId);
    }

    public string GetFilePath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new ArgumentException("Filename cannot be empty", nameof(fileName));

        // Prevent directory traversal attempts
        fileName = Path.GetFileName(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(_uploadsDirectory, fileName));

        // Ensure the resolved path is still within uploads directory
        if (!IsPathWithinUploads(fullPath))
            throw new SecurityException("Invalid file path");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found", fileName);

        return fullPath;
    }

    public async Task DeleteAttachmentAsync(int attachmentId)
    {
        var attachment = await _context.BugAttachments.FindAsync(attachmentId);
        if (attachment == null)
            return;

        var filePath = Path.Combine(_uploadsDirectory, attachment.FilePath);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.BugAttachments.Remove(attachment);
            await _context.SaveChangesAsync();

            // Delete the physical file
            await DeleteFileAsync(filePath);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to delete attachment: {AttachmentId}", attachmentId);
            throw;
        }
    }

    private async Task DeleteFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            // Verify the file path is within the uploads directory
            var fullPath = Path.GetFullPath(filePath);
            if (!IsPathWithinUploads(fullPath))
            {
                _logger.LogError("Attempted to delete file outside uploads directory: {FilePath}", filePath);
                throw new SecurityException("Invalid file path");
            }

            if (File.Exists(filePath))
            {
                // Remove read-only attribute if present
                File.SetAttributes(filePath, File.GetAttributes(filePath) & ~FileAttributes.ReadOnly);

                // Securely delete file contents before removing
                await SecureDeleteAsync(filePath);

                _logger.LogInformation("Successfully deleted file: {FilePath}", filePath);
            }
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            throw new IOException($"Failed to delete file: {ex.Message}", ex);
        }
    }

    private async Task SecureDeleteAsync(string filePath)
    {
        // Overwrite file with random data before deletion
        try
        {
            var fileInfo = new FileInfo(filePath);
            var size = fileInfo.Length;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[4096];
                var rng = RandomNumberGenerator.Create();

                for (long i = 0; i < size; i += buffer.Length)
                {
                    rng.GetBytes(buffer);
                    await fs.WriteAsync(buffer, 0, (int)Math.Min(buffer.Length, size - i));
                }

                await fs.FlushAsync();
            }

            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Secure delete failed, falling back to regular delete for: {FilePath}", filePath);
            File.Delete(filePath);
        }
    }
}