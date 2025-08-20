using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BugTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseOptimizationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_BugReportId",
                table: "ActivityLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_StartDate",
                table: "Projects",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_StartDate",
                table: "Projects",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_CreatedDate",
                table: "BugReports",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_CreatedDate_Status",
                table: "BugReports",
                columns: new[] { "CreatedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Severity",
                table: "BugReports",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Status",
                table: "BugReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_Status_Severity_CreatedDate",
                table: "BugReports",
                columns: new[] { "Status", "Severity", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_BugReportId_Timestamp",
                table: "ActivityLogs",
                columns: new[] { "BugReportId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_StartDate",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_StartDate",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_CreatedDate",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_CreatedDate_Status",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_Severity",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_Status",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_BugReports_Status_Severity_CreatedDate",
                table: "BugReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_BugReportId_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_Timestamp",
                table: "ActivityLogs");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_BugReportId",
                table: "ActivityLogs",
                column: "BugReportId");
        }
    }
}
