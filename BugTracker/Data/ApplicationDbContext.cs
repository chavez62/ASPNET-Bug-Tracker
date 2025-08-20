using BugTracker.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BugReport> BugReports { get; set; }
        public DbSet<BugAttachment> BugAttachments { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Project relationships
            builder.Entity<Project>()
                .HasOne(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Project-TeamMembers many-to-many relationship
            builder.Entity<Project>()
                .HasMany(p => p.TeamMembers)
                .WithMany()
                .UsingEntity(j => j.ToTable("ProjectTeamMembers"));

            // Project-BugReports one-to-many relationship
            builder.Entity<Project>()
                .HasMany(p => p.BugReports)
                .WithOne(b => b.Project)
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // Bug Report relationships
            builder.Entity<BugReport>()
                .HasOne(b => b.AssignedTo)
                .WithMany(u => u.AssignedBugs)
                .HasForeignKey(b => b.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BugReport>()
                .HasOne(b => b.CreatedBy)
                .WithMany(u => u.CreatedBugs)
                .HasForeignKey(b => b.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BugAttachment>()
                .HasOne(a => a.BugReport)
                .WithMany(b => b.Attachments)
                .HasForeignKey(a => a.BugReportId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ActivityLog>()
                .HasOne(a => a.BugReport)
                .WithMany(b => b.ActivityLogs)
                .HasForeignKey(a => a.BugReportId)
                .OnDelete(DeleteBehavior.Cascade);

            // Tag relationships
            builder.Entity<BugReport>()
                .HasMany(b => b.Tags)
                .WithMany(t => t.BugReports)
                .UsingEntity(j => j.ToTable("BugReportTag"));

            // Ensure tag names are unique
            builder.Entity<Tag>()
                .HasIndex(t => t.Name)
                .IsUnique();

            // Performance optimization indexes for BugReports
            builder.Entity<BugReport>()
                .HasIndex(b => b.Status)
                .HasDatabaseName("IX_BugReports_Status");

            builder.Entity<BugReport>()
                .HasIndex(b => b.Severity)
                .HasDatabaseName("IX_BugReports_Severity");

            builder.Entity<BugReport>()
                .HasIndex(b => b.CreatedDate)
                .HasDatabaseName("IX_BugReports_CreatedDate");

            builder.Entity<BugReport>()
                .HasIndex(b => b.AssignedToId)
                .HasDatabaseName("IX_BugReports_AssignedToId");

            builder.Entity<BugReport>()
                .HasIndex(b => b.CreatedById)
                .HasDatabaseName("IX_BugReports_CreatedById");

            builder.Entity<BugReport>()
                .HasIndex(b => b.ProjectId)
                .HasDatabaseName("IX_BugReports_ProjectId");

            // Composite index for common search combinations
            builder.Entity<BugReport>()
                .HasIndex(b => new { b.Status, b.Severity, b.CreatedDate })
                .HasDatabaseName("IX_BugReports_Status_Severity_CreatedDate");

            // Index for date range queries
            builder.Entity<BugReport>()
                .HasIndex(b => new { b.CreatedDate, b.Status })
                .HasDatabaseName("IX_BugReports_CreatedDate_Status");

            // Performance optimization indexes for ActivityLogs
            builder.Entity<ActivityLog>()
                .HasIndex(a => a.Timestamp)
                .HasDatabaseName("IX_ActivityLogs_Timestamp");

            builder.Entity<ActivityLog>()
                .HasIndex(a => new { a.BugReportId, a.Timestamp })
                .HasDatabaseName("IX_ActivityLogs_BugReportId_Timestamp");

            // Performance optimization indexes for BugAttachments
            builder.Entity<BugAttachment>()
                .HasIndex(a => a.BugReportId)
                .HasDatabaseName("IX_BugAttachments_BugReportId");

            // Performance optimization indexes for Projects
            builder.Entity<Project>()
                .HasIndex(p => p.Status)
                .HasDatabaseName("IX_Projects_Status");

            builder.Entity<Project>()
                .HasIndex(p => p.StartDate)
                .HasDatabaseName("IX_Projects_StartDate");

            // Composite index for project queries
            builder.Entity<Project>()
                .HasIndex(p => new { p.Status, p.StartDate })
                .HasDatabaseName("IX_Projects_Status_StartDate");
        }
    }
}