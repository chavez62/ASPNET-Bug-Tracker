# agent.md – Bug Tracker App Architecture

---

project: Bug Tracker
framework: ASP.NET Core MVC
language: C#
database: SQLite
purpose: Developer and AI architecture reference

---

## Overview

Bug Tracker is a secure, role-aware issue management platform for software teams. It enables authenticated users to submit, triage, and monitor bug reports, while providing administrators with project oversight, analytics, and database maintenance tooling.

## Project Structure

```text
BugTracker/
├── Program.cs
├── Controllers/
│   ├── BaseApiController.cs
│   ├── BugReportsController.cs
│   ├── BugAttachmentsController.cs
│   ├── ProjectsController.cs
│   ├── TagsController.cs
│   ├── DatabaseController.cs
│   └── HomeController.cs
├── Data/
│   ├── ApplicationDbContext.cs
│   └── SeedData.cs
├── Exceptions/
│   └── NotFoundException.cs
├── Migrations/
│   └── *.cs (EF Core migrations)
├── Models/
│   ├── ActivityLog.cs
│   ├── ApplicationUser.cs
│   ├── BugAttachment.cs
│   ├── BugReport.cs
│   ├── Project.cs
│   ├── Tag.cs
│   └── Enums/
├── Services/
│   ├── ActivityLogService.cs
│   ├── BugService.cs
│   ├── BugValidationService.cs
│   ├── FileService.cs
│   ├── ProjectService.cs
│   ├── DatabasePerformanceService.cs
│   └── QueryPerformanceService.cs
├── Views/
│   ├── Shared/
│   ├── BugReports/
│   ├── Projects/
│   ├── Tags/
│   ├── Database/
│   └── Home/
├── Areas/Identity/
├── wwwroot/
└── appsettings*.json
```

## Core Components

- `Program.cs`: Configures services (Identity, EF Core, custom services), middleware pipeline, rate limiting, and startup seeding.
- `ApplicationDbContext.cs`: EF Core context extending Identity, declaring DbSets and configuring relationships, indexes, and cascade rules.
- `BugReportsController.cs`: Authenticated CRUD UI for bug reports, orchestrating validation, service calls, and tag handling.
- `BugService.cs`: Core business logic for querying, creating, updating, and deleting bug reports with audit logging and transactions.
- `ProjectService.cs`: Encapsulates project CRUD, access control checks, and derived statistics for dashboards.
- `FileService.cs`: Validates and persists uploaded attachments with strict security checks and secure deletion routines.
- `ActivityLogService.cs`: Records activity and comment events for bug reports, providing chronological histories.
- `DatabaseController.cs`: Admin-only surface for performance diagnostics, slow query inspection, and SQLite optimization.
- `SeedData.cs`: Bootstraps roles, admin account, and configuration validation during startup.

## Data Flow

1. **User submission**: Authenticated users navigate to `BugReports/Create`; controller resolves lookup data (users, tags) and exposes client validation rules.
2. **Validation**: On POST, `BugValidationService` enforces title/description limits, assignment validity, and optional file constraints. ModelState errors short-circuit back to the form.
3. **Persistence**: Valid submissions pass to `BugService.CreateBugReportAsync`, which verifies project linkage, manages EF Core transactions, attaches selected tags, and saves via `ApplicationDbContext`.
4. **Side effects**: After commit, `ActivityLogService.LogActivityAsync` captures a "Created" event; attachments pass through `FileService` to disk and `BugAttachment` records when applicable.
5. **Response**: Controller redirects to `BugReports/Details`, where `BugService.GetBugReportAsync` loads related entities (project, users, tags, attachments, activity) for display.

## APIs & Integrations

- **MVC routes**: Conventional routing (`{controller}/{action}/{id?}`) exposes endpoints such as `GET /BugReports`, `POST /BugReports/Create`, `POST /BugReports/Edit/{id}`, and admin-only `POST /BugReports/Delete/{id}`.
- **REST-style helpers**: `BaseApiController` provides JSON responses for AJAX workflows (`AddComment`, validation failures) based on the `X-Requested-With` header.
- **Database insights**: `DatabaseController` exposes MVC actions and a JSON endpoint `GET /Database/GetMetrics` for dashboards polling performance data.
- **Health check**: `GET /health` (registered in `Program.cs`) returns app status JSON for monitoring.
- **Security integrations**: ASP.NET Core Identity handles authentication/authorization with role support; rate limiting middleware (`PartitionedRateLimiter`) adds global throttling.

## Database Schema

- **BugReport**: Core entity with severity, status, timestamps, assignee (`AssignedToId`), creator (`CreatedById`), optional `ProjectId`, collections for attachments, activity logs, and tags.
- **Project**: Tracks project metadata, manager, status (`ProjectStatus`), team members (many-to-many with users), and related bug reports.
- **ApplicationUser**: Identity user extended with first/last names and navigation sets for assigned/created bugs.
- **BugAttachment**: Stores file metadata and `BugReportId`; cascade delete from bug.
- **ActivityLog**: Records user actions/comments tied to a bug report.
- **Tag**: Label entity with unique `Name`, color, and many-to-many link to bug reports via `BugReportTag` join table.
- **Indexes**: Extensive indices on bug report fields (status, severity, created date, actors, project) and project status/dates optimize dashboard queries; tag names enforced unique.

## Error Handling & Logging

- Controllers inherit shared helpers (`HandleError`, `JsonError`) to route errors to friendly responses or AJAX payloads.
- Services use `ILogger<T>` for structured messages on success paths and exception branches, with error propagation via custom exceptions (`NotFoundException`) where relevant.
- `Program.cs` adjusts logging verbosity based on authentication strength configuration and enables developer exception pages in Development.
- Activity logs provide user-visible audit trails, while TempData-driven flash messages communicate UI errors and successes.

## Future Enhancements

- Add granular role-based authorization (QA, Developer, Viewer) to refine access to bug/project actions.
- Introduce AI-assisted triage to suggest severity, assignees, or duplicates based on historical data.
- Embed analytics dashboards (charts, burn-down metrics) within `/Database` or project views using cached query results.
- Expand REST API surface for integrations (mobile client, CI pipelines) with token-based authentication.
- Implement background jobs for notification dispatch, SLA monitoring, and archival of resolved defects.

## Agent Context Notes

> Agent Context:
>
> - Recognize controllers ending in \*Controller.cs
> - Business logic is in /Services
> - Database models in /Models
> - Data context = ApplicationDbContext.cs
> - Tag relationships use the `BugReportTag` join table configured in `ApplicationDbContext`
> - Global rate limiting and security headers set in `Program.cs`
