using System;
using BugTracker.Data;
using BugTracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BugTracker.Services;

public partial class BugService : IBugService
{
    private readonly ApplicationDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<BugService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public BugService(
        ApplicationDbContext context,
        IActivityLogService activityLogService,
        ILogger<BugService> logger,
        UserManager<ApplicationUser> userManager)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }
}

