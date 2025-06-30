# Quick Reference Guide

> **Medical Dictation Service - Developer Quick Reference**  
> *Fast access to commands, patterns, and solutions*

---

## 🚀 Quick Start Commands

### **Project Setup (Windows PowerShell)**
```powershell
# Create project
dotnet new webapi -n MedicalDictationService
cd MedicalDictationService

# Add packages
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Microsoft.CognitiveServices.Speech
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# Run project
dotnet run
```

### **Azure Setup**
```powershell
# Login and create resources
az login
az group create --name MedicalDictationRG --location eastus
az cognitiveservices account create --name MedicalSpeech --resource-group MedicalDictationRG --kind SpeechServices --sku S0 --location eastus
```

---

## 🔧 Essential Code Patterns

### **Azure Speech Integration**
```csharp
// Service registration
builder.Services.AddSingleton<SpeechConfig>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    return SpeechConfig.FromSubscription(config["AzureSpeech:Key"], config["AzureSpeech:Region"]);
});

// Real-time transcription
public async Task StartTranscription()
{
    using var recognizer = new SpeechRecognizer(_speechConfig);
    
    recognizer.Recognized += async (s, e) =>
    {
        await _hubContext.Clients.All.SendAsync("TranscriptionUpdate", e.Result.Text);
    };
    
    await recognizer.StartContinuousRecognitionAsync();
}
```

### **SignalR Hub**
```csharp
[Authorize]
public class TranscriptionHub : Hub
{
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }
}
```

### **Entity Framework Setup**
```csharp
public class ApplicationDbContext : DbContext
{
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Transcription> Transcriptions { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Patient>()
            .Property(p => p.FirstName)
            .HasConversion(
                v => EncryptionHelper.Encrypt(v),
                v => EncryptionHelper.Decrypt(v));
    }
}
```

---

## 🔒 Security Essentials

### **JWT Authentication**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AzureAd:Authority"];
        options.Audience = builder.Configuration["AzureAd:Audience"];
    });
```

### **Data Encryption**
```csharp
public static class EncryptionHelper
{
    public static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        // Implementation here
        return Convert.ToBase64String(encryptedData);
    }
}
```

---

## 📊 Configuration Templates

### **appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MedicalDictationDb;Trusted_Connection=true"
  },
  "AzureSpeech": {
    "Key": "your-key-here",
    "Region": "eastus"
  },
  "AzureAd": {
    "Authority": "https://login.microsoftonline.com/your-tenant",
    "Audience": "your-app-id"
  }
}
```

---

## 🧪 Testing Patterns

### **Unit Test Template**
```csharp
public class TranscriptionServiceTests
{
    [Fact]
    public async Task ProcessTranscription_ValidInput_ReturnsResult()
    {
        // Arrange
        var service = new TranscriptionService();
        
        // Act
        var result = await service.ProcessAsync("test audio");
        
        // Assert
        Assert.NotNull(result);
    }
}
```

---

## 🔍 Troubleshooting

### **Common Issues**
- **Database connection fails**: Check connection string, ensure SQL Server running
- **Speech service timeout**: Verify Azure credentials and region
- **CORS errors**: Configure CORS policy correctly
- **SignalR not connecting**: Check authentication and transport settings

### **Diagnostic Commands**
```powershell
# Check database
sqlcmd -S "(localdb)\mssqllocaldb" -Q "SELECT @@VERSION"

# Test web endpoint
Invoke-WebRequest -Uri "https://localhost:7000/health"

# Check process
Get-Process -Name "dotnet"
```

---

## 📈 Performance Targets

| **Operation** | **Target** | **Max** |
|---------------|------------|---------|
| Audio → Text | <2s | 5s |
| DB Query | <500ms | 1s |
| API Response | <200ms | 500ms |

---

**Need more details?** See the full documentation in the [AI Development Guide](README.md).

---

> **Quick Reference Principle**: *"Fast access to essential patterns accelerates development."* 