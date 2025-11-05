using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter(); // This line will now work
// Configure Identity
var authConfig = builder.Configuration.GetSection("Authentication");
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = authConfig.GetValue<bool>("RequireConfirmedAccount");
    options.Password.RequireDigit = authConfig.GetValue<bool>("Password:RequireDigit");
    options.Password.RequireLowercase = authConfig.GetValue<bool>("Password:RequireLowercase");
    options.Password.RequireUppercase = authConfig.GetValue<bool>("Password:RequireUppercase");
    options.Password.RequireNonAlphanumeric = authConfig.GetValue<bool>("Password:RequireNonAlphanumeric");
    options.Password.RequiredLength = authConfig.GetValue<int>("Password:RequiredLength");

    // Configure lockout settings
    options.Lockout.MaxFailedAccessAttempts = authConfig.GetValue<int>("Lockout:MaxFailedAccessAttempts");
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(authConfig.GetValue<int>("Lockout:DefaultLockoutTimeSpanInMinutes"));
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Validate authentication configuration
var isStrongAuth = authConfig.GetValue<bool>("RequireConfirmedAccount") &&
    authConfig.GetValue<bool>("Password:RequireDigit") &&
    authConfig.GetValue<bool>("Password:RequireLowercase") &&
    authConfig.GetValue<bool>("Password:RequireUppercase") &&
    authConfig.GetValue<bool>("Password:RequireNonAlphanumeric") &&
    authConfig.GetValue<int>("Password:RequiredLength") >= 8;

if (isStrongAuth)
{
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Information);
}
else
{
    builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
}

// Add custom services
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IBugValidationService, BugValidationService>();
builder.Services.AddScoped<ITagService, TagService>();

// Add configuration validation
builder.Services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();

// Add database performance services
builder.Services.AddScoped<IDatabasePerformanceService, DatabasePerformanceService>();
builder.Services.AddScoped<IQueryPerformanceService, QueryPerformanceService>();

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

// Add security services
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

// Add rate limiting for security
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter("GlobalLimiter",
            partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Add rate limiting middleware
app.UseRateLimiter();

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// Add health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Timestamp = DateTime.UtcNow });

// Seed initial roles and admin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();


        context.Database.Migrate();
        await SeedData.Initialize(userManager, roleManager);

        // Validate configuration
        var configValidator = services.GetRequiredService<IConfigurationValidator>();
        configValidator.LogConfigurationStatus();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();