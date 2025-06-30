using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MedicalDictationService.Data;
using MedicalDictationService.Services;
using MedicalDictationService.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Configure Azure Speech Services
builder.Services.Configure<SpeechServiceOptions>(options =>
{
    options.SubscriptionKey = builder.Configuration["AzureSpeech:SubscriptionKey"] ?? "";
    options.Region = builder.Configuration["AzureSpeech:Region"] ?? "eastus";
    options.Language = "en-US";
    options.EnableMedicalTerminology = true;
    options.EnableNoiseReduction = true;
    options.ConfidenceThreshold = 0.7;
});

// Add application services
builder.Services.AddScoped<IMedicalTerminologyService, MedicalTerminologyService>();
builder.Services.AddScoped<IAzureSpeechService, AzureSpeechService>();

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Add Razor Components and interactive server components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Application Insights logging
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map Razor Components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Identity pages
app.MapRazorPages();

// Map SignalR Hub
app.MapHub<TranscriptionHub>("/transcriptionHub");

// Seed initial data
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run(); 