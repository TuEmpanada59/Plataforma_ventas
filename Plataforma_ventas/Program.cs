using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Plataforma_ventas.Services.IEmailService, Plataforma_ventas.Services.SmtpEmailService>();
builder.Services.AddScoped<Plataforma_ventas.Services.IAuditoriaService, Plataforma_ventas.Services.AuditoriaService>();

builder.Services.AddSession(options =>
{
    // Sesión de 20 minutos (alineado con el banner de expiración y la documentación).
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // La cookie de sesión solo viaja por HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddSignalR();

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

// ── Data Protection ──
// In development: persist keys to disk so antiforgery tokens survive dotnet watch
// restarts. In production (Azure App Service): skip file persistence — the
// filesystem is read-only when deployed as a zip package, causing IOException
// on the first request that crashes antiforgery validation. Ephemeral in-memory
// keys work correctly for a single-instance App Service; users simply need to
// log in again after an instance recycle.
var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("PlataformaVentas")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

if (builder.Environment.IsDevelopment())
{
    var keysDir = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "Keys"));
    keysDir.Create(); // ensure directory exists before registering
    dpBuilder.PersistKeysToFileSystem(keysDir);
}

QuestPDF.Settings.License = LicenseType.Community;
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Show full exception details only in development — never expose stack traces in production.
    app.UseDeveloperExceptionPage();
}
else
{
    // Production: show a user-friendly error page, set HSTS headers.
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle 404, 403, etc. with a friendly re-executed route instead of blank responses.
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    // 'unsafe-inline' es necesario porque las vistas usan CSS/JS inline;
    // aun así la CSP bloquea cargas de orígenes no listados
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'";
    await next();
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllerRoute(
    name: "areas",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapHub<Plataforma_ventas.Hubs.VentasHub>("/ventasHub");

app.Run();
