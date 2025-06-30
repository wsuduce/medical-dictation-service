# Book 7: Quick Reference Guide

> **AI Development Manual → Quick Reference Guide**  
> *Fast access to common patterns, commands, and implementation snippets*

---

## 📍 Navigation Context

**Current Location**: `Documentation/AI-Development-Guide/07-Quick-Reference/`  
**Parent**: [AI Development Guide](../README.md)  
**Purpose**: Provide quick access to frequently needed information during development

---

## 🚀 Quick Start Commands

### **Project Setup Commands (Windows PowerShell)**
```powershell
# Create new ASP.NET Core project
dotnet new webapi -n MedicalDictationService
cd MedicalDictationService

# Add required packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.CognitiveServices.Speech
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Serilog.AspNetCore

# Create solution file
dotnet new sln -n MedicalDictationService
dotnet sln add MedicalDictationService.csproj

# Initialize git repository
git init
git add .
git commit -m "Initial project setup"
```

### **Database Setup Commands**
```powershell
# Install Entity Framework tools
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update

# Generate database script
dotnet ef migrations script
```

### **Azure CLI Setup**
```powershell
# Login to Azure
az login

# Create resource group
az group create --name MedicalDictationRG --location eastus

# Create Azure Speech service
az cognitiveservices account create --name MedicalDictationSpeech --resource-group MedicalDictationRG --kind SpeechServices --sku S0 --location eastus

# Get Speech service keys
az cognitiveservices account keys list --name MedicalDictationSpeech --resource-group MedicalDictationRG
```

---

## 🔧 Common Code Patterns

### **Azure Speech Service Integration**
```csharp
// Service Registration (Program.cs)
builder.Services.AddSingleton<SpeechConfig>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var speechKey = configuration["AzureSpeech:Key"];
    var speechRegion = configuration["AzureSpeech:Region"];
    return SpeechConfig.FromSubscription(speechKey, speechRegion);
});

// Real-time Recognition Service
public class StreamingTranscriptionService
{
    private readonly SpeechConfig _speechConfig;
    private readonly IHubContext<TranscriptionHub> _hubContext;
    
    public async Task StartRecognition(string sessionId)
    {
        using var recognizer = new SpeechRecognizer(_speechConfig);
        
        recognizer.Recognizing += async (s, e) =>
        {
            await _hubContext.Clients.Group(sessionId)
                .SendAsync("InterimResult", e.Result.Text);
        };
        
        recognizer.Recognized += async (s, e) =>
        {
            await _hubContext.Clients.Group(sessionId)
                .SendAsync("FinalResult", e.Result.Text);
        };
        
        await recognizer.StartContinuousRecognitionAsync();
    }
}
```

### **Entity Framework Patient Context**
```csharp
// Patient Entity with Encryption
public class Patient
{
    public Guid Id { get; set; }
    public string MedicalRecordNumber { get; set; }
    
    [PersonalData]
    public string FirstName { get; set; }
    
    [PersonalData]
    public string LastName { get; set; }
    
    [PersonalData]
    public DateTime DateOfBirth { get; set; }
    
    public List<Transcription> Transcriptions { get; set; }
}

// DbContext Configuration
public class ApplicationDbContext : DbContext
{
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Transcription> Transcriptions { get; set; }
    public DbSet<Template> Templates { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure encryption for sensitive fields
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.Property(e => e.FirstName)
                .HasConversion(
                    v => EncryptionHelper.Encrypt(v),
                    v => EncryptionHelper.Decrypt(v));
        });
    }
}
```

### **SignalR Hub for Real-time Communication**
```csharp
[Authorize]
public class TranscriptionHub : Hub
{
    private readonly IPatientContextService _patientContext;
    
    public async Task JoinPatientSession(string patientId)
    {
        // Verify user has access to this patient
        var hasAccess = await _patientContext.UserHasPatientAccessAsync(
            Context.UserIdentifier, patientId);
            
        if (hasAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"patient-{patientId}");
        }
    }
    
    public async Task StartTranscription(string patientId)
    {
        var sessionId = Guid.NewGuid().ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        
        // Start transcription service
        var transcriptionService = Context.GetHttpContext()
            .RequestServices.GetRequiredService<IStreamingTranscriptionService>();
        await transcriptionService.StartRecognition(sessionId);
    }
}
```

---

## 🔒 Security Code Snippets

### **JWT Authentication Setup**
```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience = builder.Configuration["AzureAd:Audience"];
        options.RequireHttpsMetadata = true;
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/transcriptionhub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

### **HIPAA Audit Logging**
```csharp
public class AuditActionFilter : ActionFilterAttribute
{
    private readonly IAuditLogger _auditLogger;
    
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var action = context.ActionDescriptor.DisplayName;
        var patientId = context.ActionArguments.ContainsKey("patientId") 
            ? context.ActionArguments["patientId"]?.ToString() 
            : null;
        
        await _auditLogger.LogActionAsync(new AuditEntry
        {
            UserId = user.GetUserId(),
            Action = action,
            PatientId = patientId,
            Timestamp = DateTime.UtcNow,
            IpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        
        await next();
    }
}
```

### **Data Encryption Helper**
```csharp
public static class EncryptionHelper
{
    private static readonly string _key = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
    
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key);
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
        
        return Convert.ToBase64String(result);
    }
    
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        
        var cipherBytes = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(_key);
        
        var iv = new byte[aes.IV.Length];
        var encrypted = new byte[cipherBytes.Length - iv.Length];
        
        Buffer.BlockCopy(cipherBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, iv.Length, encrypted, 0, encrypted.Length);
        
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
```

---

## 📊 Configuration Templates

### **appsettings.json Template**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MedicalDictationDb;Trusted_Connection=true;MultipleActiveResultSets=true;Encrypt=false"
  },
  "AzureSpeech": {
    "Key": "your-speech-service-key",
    "Region": "eastus",
    "Endpoint": "https://eastus.api.cognitive.microsoft.com/"
  },
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/your-tenant-id",
    "Audience": "your-app-id"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### **Environment Variables Template (.env)**
```bash
# Database
CONNECTION_STRING=Server=(localdb)\\mssqllocaldb;Database=MedicalDictationDb;Trusted_Connection=true

# Azure Speech Services
AZURE_SPEECH_KEY=your-speech-service-key-here
AZURE_SPEECH_REGION=eastus

# Security
JWT_SECRET_KEY=your-very-long-secret-key-here
ENCRYPTION_KEY=your-base64-encryption-key-here

# Application
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:7000;http://localhost:5000
```

---

## 🧪 Testing Patterns

### **Unit Test Template**
```csharp
public class TranscriptionServiceTests
{
    private readonly Mock<IAzureSpeechService> _speechServiceMock;
    private readonly Mock<IPatientContextService> _patientContextMock;
    private readonly TranscriptionService _service;
    
    public TranscriptionServiceTests()
    {
        _speechServiceMock = new Mock<IAzureSpeechService>();
        _patientContextMock = new Mock<IPatientContextService>();
        _service = new TranscriptionService(_speechServiceMock.Object, _patientContextMock.Object);
    }
    
    [Fact]
    public async Task ProcessTranscription_WithValidInput_ReturnsAccurateResult()
    {
        // Arrange
        var audioData = CreateTestAudioData();
        var expectedTranscription = "Patient reports chest pain";
        
        _speechServiceMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(expectedTranscription);
        
        // Act
        var result = await _service.ProcessTranscriptionAsync(audioData);
        
        // Assert
        Assert.Equal(expectedTranscription, result.Text);
        Assert.True(result.Confidence > 0.8);
    }
}
```

### **Integration Test Template**
```csharp
public class TranscriptionIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public TranscriptionIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task StartTranscription_WithAuthenticatedUser_ReturnsSuccess()
    {
        // Arrange
        var token = await GetValidJwtToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        // Act
        var response = await _client.PostAsync("/api/transcription/start", new StringContent(""));
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

---

## 🔍 Debugging & Troubleshooting

### **Common Issues & Solutions**

#### **Azure Speech Service Connection Issues**
```csharp
// Debug Azure Speech connectivity
public class SpeechServiceDiagnostics
{
    public async Task<bool> TestSpeechServiceConnection()
    {
        try
        {
            var config = SpeechConfig.FromSubscription("your-key", "your-region");
            using var recognizer = new SpeechRecognizer(config);
            
            // Test with a simple recognition
            var result = await recognizer.RecognizeOnceAsync();
            return result.Reason == ResultReason.RecognizedSpeech;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Speech service error: {ex.Message}");
            return false;
        }
    }
}
```

#### **Database Connection Debugging**
```csharp
// Test database connectivity
public class DatabaseDiagnostics
{
    private readonly ApplicationDbContext _context;
    
    public async Task<bool> TestDatabaseConnection()
    {
        try
        {
            await _context.Database.CanConnectAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database connection error: {ex.Message}");
            return false;
        }
    }
}
```

### **Performance Monitoring**
```csharp
// Performance monitoring middleware
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        await _next(context);
        
        stopwatch.Stop();
        
        if (stopwatch.ElapsedMilliseconds > 1000) // Log slow requests
        {
            _logger.LogWarning("Slow request: {Method} {Path} took {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

---

## 📱 API Endpoint Quick Reference

### **Transcription Endpoints**
```
POST   /api/transcription/start           # Start new transcription session
POST   /api/transcription/stop            # Stop current transcription
GET    /api/transcription/{id}            # Get transcription by ID
PUT    /api/transcription/{id}            # Update transcription
DELETE /api/transcription/{id}            # Delete transcription
```

### **Patient Endpoints**
```
GET    /api/patients                      # List patients (with search)
GET    /api/patients/{id}                 # Get patient details
POST   /api/patients                      # Create new patient
PUT    /api/patients/{id}                 # Update patient
GET    /api/patients/{id}/transcriptions  # Get patient transcriptions
```

### **Template Endpoints**
```
GET    /api/templates                     # List available templates
GET    /api/templates/{id}                # Get template details
POST   /api/templates                     # Create new template
PUT    /api/templates/{id}                # Update template
DELETE /api/templates/{id}                # Delete template
```

---

## 🎯 Performance Benchmarks

### **Target Performance Metrics**
| **Operation** | **Target Time** | **Max Acceptable** |
|---------------|----------------|--------------------|
| **Audio → Text** | <2 seconds | 5 seconds |
| **Database Query** | <500ms | 1 second |
| **Page Load** | <1 second | 3 seconds |
| **API Response** | <200ms | 500ms |
| **SignalR Message** | <100ms | 300ms |

### **Load Testing Script**
```bash
# Install artillery for load testing
npm install -g artillery

# Create load test config (artillery.yml)
artillery run --config artillery.yml
```

---

**Need something specific?** This quick reference covers the most commonly needed patterns and commands. For detailed implementation guidance, refer to the main documentation books.

---

> **Quick Reference Principle**: *"Fast access to common patterns accelerates development and reduces errors."* 