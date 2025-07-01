using MedicalDictationService.Components;
using MedicalDictationService.Data;
using MedicalDictationService.Services;
using MedicalDictationService.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add authentication services for Blazor
builder.Services.AddCascadingAuthenticationState();

// Add Razor Pages for Identity UI
builder.Services.AddRazorPages();

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Add Azure Speech Services and Medical Terminology Services
builder.Services.AddSingleton<IMedicalTerminologyService, MedicalTerminologyService>();
builder.Services.AddSingleton<IAzureSpeechService, AzureSpeechService>();

// Add Entity Framework and SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add ASP.NET Core Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    // Configure password requirements for medical users
    options.SignIn.RequireConfirmedAccount = false; // Will be true in production
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Identity Razor Pages
app.MapRazorPages();

// Map SignalR Hub
app.MapHub<TranscriptionHub>("/transcriptionHub");

// Seed initial data (admin user)
using (var scope = app.Services.CreateScope())
{
    await MedicalDictationService.Data.SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();
