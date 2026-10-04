using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SASMS.Web.Data;
using SASMS.Web.Models.Entities;
using SASMS.Web.Security;
using SASMS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Optional HTTPS LAN endpoint (dev/demo only — not production TLS) ----
// Only active when a locally-generated mkcert certificate exists under dev-https-cert/
// (gitignored, machine-specific, covers this PC's LAN IP so a phone on the same Wi-Fi can
// reach it with a trusted cert). Absence is silently a no-op, so a clone of this repo
// without that folder generated — or any other environment — behaves exactly as before
// (--urls/launchSettings fully in control, HTTP-only).
//
// IMPORTANT: ASP.NET Core's documented precedence is that explicit Listen/ListenAnyIP
// calls in ConfigureKestrel REPLACE whatever --urls/launchSettings specifies, they don't
// merge with it. So when the cert is present, both the HTTP (5080) and HTTPS (5443)
// listeners are configured explicitly here, together, so HTTP keeps working exactly as
// before instead of silently disappearing once Kestrel is configured in code.
var devCertDir = Path.Combine(builder.Environment.ContentRootPath, "dev-https-cert");
if (Directory.Exists(devCertDir))
{
    var devCertPath = Directory.GetFiles(devCertDir, "*.pem").FirstOrDefault(f => !Path.GetFileName(f).Contains("-key"));
    var devKeyPath = devCertPath is null ? null : Path.Combine(devCertDir, Path.GetFileNameWithoutExtension(devCertPath) + "-key.pem");

    if (devCertPath is not null && devKeyPath is not null && File.Exists(devKeyPath))
    {
        // X509Certificate2.CreateFromPemFile alone produces an "ephemeral" in-memory key that
        // Windows' SChannel provider cannot reliably use for a server-side TLS handshake —
        // confirmed by an actual handshake failure during setup ("schannel: failed to receive
        // handshake"), a documented .NET-on-Windows limitation, not a cert-content or trust
        // problem. Re-importing via a PFX round-trip (still entirely in-memory, no new file)
        // forces the key into a form SChannel can actually use.
        var pemCert = X509Certificate2.CreateFromPemFile(devCertPath, devKeyPath);
        var devCert = new X509Certificate2(pemCert.Export(X509ContentType.Pfx));

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(5080); // HTTP — same port this app has always used
            options.ListenAnyIP(5443, listenOptions =>
            {
                listenOptions.UseHttps(devCert);
            });
        });
    }
}

// ---- Configuration ----
builder.Services.Configure<AccountLockoutSettings>(builder.Configuration.GetSection("AccountLockout"));
builder.Services.Configure<VerificationServiceSettings>(builder.Configuration.GetSection("VerificationService"));
builder.Services.Configure<FaceServiceSettings>(builder.Configuration.GetSection("FaceVerificationService"));

// ---- Database ----
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured. Set it via appsettings.Development.json or dotnet user-secrets.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
        // Transient-fault resilience (dropped connection, brief MySQL restart, etc.) — retries
        // with backoff instead of failing the request outright on the first hiccup.
        mySqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));

// ---- Auth ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
// Stateless (resolves the Malaysia TimeZoneInfo once) — single source of truth for every
// business-calendar-date decision, so the app is correct regardless of the host OS's
// default timezone. See IBusinessClock for what this does and doesn't cover.
builder.Services.AddSingleton<IBusinessClock, BusinessClock>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "SASMS.Auth";
        options.Cookie.HttpOnly = true;
        // SameAsRequest (not Always): the cookie still gets the Secure flag whenever the
        // request is HTTPS (which UseHttpsRedirection enforces below in production), but
        // this also lets the app be reached over plain HTTP for local dev/demo without a
        // trusted cert, where an "Always" policy would silently make login impossible.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            // Sliding expiration alone means a continuously-active session never truly
            // ends. This caps total session lifetime independent of activity, so a stolen
            // cookie can't be kept alive forever just by staying in use.
            OnValidatePrincipal = context =>
            {
                var authTimeClaim = context.Principal?.FindFirst(AppClaimTypes.AuthTime)?.Value;
                if (authTimeClaim is not null
                    && long.TryParse(authTimeClaim, out var authTimeUnix)
                    && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(authTimeUnix) > TimeSpan.FromHours(8))
                {
                    context.RejectPrincipal();
                    return context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---- Rate limiting (per-client-IP, not global — a single abusive caller shouldn't be
// able to exhaust the quota for every other legitimate user) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Guards the shared-secret verification callback: account lockout protects user
    // logins from brute force, but nothing else stopped repeated wrong-secret attempts
    // against this endpoint before this.
    options.AddPolicy("verification-api", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// ---- Application services ----
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();

builder.Services.AddHttpClient<IFaceVerificationClient, FaceVerificationClient>(client =>
{
    // face-service does its own CV work per frame burst; give it real headroom rather
    // than the framework's 100s-ish default-adjacent assumptions for a "fast" API.
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ---- MVC ----
builder.Services.AddControllersWithViews(options =>
{
    // Enforced globally so every POST/PUT/DELETE form requires a valid antiforgery token,
    // not just the ones a developer remembered to decorate individually.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// The face-capture flow POSTs JSON via fetch() rather than a traditional <form>, so there's
// no hidden input to carry the antiforgery token — it travels as a header instead. The
// token value itself is rendered into a <meta> tag in _Layout.cshtml for the page's JS to read.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();

// ---- First-run migration + seed ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(db, hasher, logger);
}

// ---- Pipeline ----

// Must run first, before anything that inspects the request's scheme/IP — the HTTPS
// redirect below, the security-header middleware, rate limiting's per-client-IP
// partitioning, and every HttpContext.Connection.RemoteIpAddress read elsewhere
// (AccountController/VerificationController/CurrentUserService's audit-log IP) — otherwise
// all of those see Nginx's own address/scheme in the Docker Compose deployment instead of
// the real client's. KnownNetworks/KnownProxies are cleared and only re-populated from
// configuration (docker-compose.yml sets ReverseProxy:TrustedNetwork to the compose
// network's own fixed subnet, which only Nginx is on), so this never blindly trusts
// forwarded headers from an arbitrary source — direct internet clients can't reach this app
// at all in that deployment (only Nginx's port is published), and in local dev (no
// ReverseProxy:TrustedNetwork configured) this is a no-op, identical to today's behavior.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
var trustedProxyNetwork = builder.Configuration["ReverseProxy:TrustedNetwork"];
if (!string.IsNullOrWhiteSpace(trustedProxyNetwork))
{
    var networkParts = trustedProxyNetwork.Split('/');
    forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse(networkParts[0]), int.Parse(networkParts[1])));
}
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    // No inline <script> anywhere in the app (confirm-dialogs use an unobtrusive listener
    // in site.js instead) so script-src can stay strict; style-src keeps 'unsafe-inline'
    // because Bootstrap's own JS toggles inline styles for collapse/offcanvas/dropdowns.
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net; " +
        "style-src 'self' https://cdn.jsdelivr.net https://fonts.googleapis.com 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'");
    context.Response.Headers.Append("Permissions-Policy", "camera=(self), microphone=(), geolocation=()");
    await next();
});

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
