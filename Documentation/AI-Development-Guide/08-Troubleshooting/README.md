# Book 8: Troubleshooting Guide

> **AI Development Manual → Troubleshooting Guide**  
> *Comprehensive problem-solving guide for common development and deployment issues*

---

## 📍 Navigation Context

**Current Location**: `Documentation/AI-Development-Guide/08-Troubleshooting/`  
**Parent**: [AI Development Guide](../README.md)  
**Purpose**: Provide systematic troubleshooting for development, deployment, and runtime issues

---

## 🚨 Critical Issues & Emergency Responses

### **Production Down Scenarios**

#### **Speech Service Unavailable**
```csharp
// Immediate Response
public class SpeechServiceFallback
{
    public async Task<string> HandleSpeechServiceOutage()
    {
        // 1. Switch to offline mode
        await EnableOfflineMode();
        
        // 2. Notify users
        await NotifyUsersOfServiceIssue("Speech recognition temporarily unavailable");
        
        // 3. Queue audio for later processing
        await QueueAudioForLaterProcessing();
        
        // 4. Alert operations team
        await AlertOpsTeam("CRITICAL: Azure Speech Service unavailable");
        
        return "Switched to emergency offline mode";
    }
}
```

#### **Database Connection Lost**
```bash
# Emergency database recovery steps
# 1. Check connection string
echo $CONNECTION_STRING

# 2. Test database connectivity
sqlcmd -S server -d database -E -Q "SELECT 1"

# 3. Check if database is online
SELECT state_desc FROM sys.databases WHERE name = 'MedicalDictationDb'

# 4. If needed, bring database online
ALTER DATABASE MedicalDictationDb SET ONLINE

# 5. Restart application pool
iisreset /restart
```

#### **Authentication Service Down**
```csharp
// Emergency authentication bypass (DEVELOPMENT ONLY)
public class EmergencyAuthBypass
{
    public void ConfigureEmergencyAuth(IServiceCollection services)
    {
        if (Environment.GetEnvironmentVariable("EMERGENCY_AUTH") == "true")
        {
            services.AddAuthentication("Emergency")
                .AddScheme<EmergencyAuthSchemeOptions, EmergencyAuthHandler>("Emergency", options => { });
        }
    }
}
```

---

## 🔧 Development Environment Issues

### **Common Setup Problems**

#### **Issue: "dotnet command not found"**
**Symptoms**: PowerShell can't find dotnet command
**Solution**:
```powershell
# Download and install .NET 8 SDK
Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile "dotnet-install.ps1"
.\dotnet-install.ps1 -Channel 8.0

# Add to PATH if needed
$env:PATH += ";C:\Program Files\dotnet"

# Verify installation
dotnet --version
```

#### **Issue: Package restore fails**
**Symptoms**: NuGet packages won't restore
**Solution**:
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Remove obj and bin folders
Remove-Item -Recurse -Force .\obj, .\bin

# Restore packages
dotnet restore

# If still failing, check NuGet sources
dotnet nuget list source
```

#### **Issue: Database migration fails**
**Symptoms**: "Cannot connect to database" during migration
**Solution**:
```powershell
# Check connection string in appsettings.json
Get-Content appsettings.json | Select-String "ConnectionString"

# Test SQL Server connection
sqlcmd -S "(localdb)\mssqllocaldb" -Q "SELECT @@VERSION"

# If LocalDB not running, start it
sqllocaldb start mssqllocaldb

# Run migration with verbose output
dotnet ef database update --verbose
```

### **Azure Speech Service Issues**

#### **Issue: Speech recognition not working**
**Symptoms**: No transcription results, timeout errors
**Debugging Steps**:
```csharp
public class SpeechServiceDiagnostics
{
    public async Task<DiagnosticResult> DiagnoseSpeechService()
    {
        var result = new DiagnosticResult();
        
        // Test 1: Check service configuration
        try
        {
            var config = SpeechConfig.FromSubscription(_key, _region);
            result.ConfigurationValid = true;
        }
        catch (Exception ex)
        {
            result.ConfigurationValid = false;
            result.Errors.Add($"Config error: {ex.Message}");
        }
        
        // Test 2: Check network connectivity
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://{_region}.api.cognitive.microsoft.com/");
            result.NetworkConnectivity = response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            result.NetworkConnectivity = false;
            result.Errors.Add($"Network error: {ex.Message}");
        }
        
        // Test 3: Test with sample audio
        try
        {
            var testResult = await TestWithSampleAudio();
            result.ServiceFunctional = testResult.Reason == ResultReason.RecognizedSpeech;
        }
        catch (Exception ex)
        {
            result.ServiceFunctional = false;
            result.Errors.Add($"Service error: {ex.Message}");
        }
        
        return result;
    }
}
```

**Common Solutions**:
```bash
# Check Azure Speech service key
az cognitiveservices account keys list --name YourSpeechService --resource-group YourResourceGroup

# Verify region is correct
az cognitiveservices account show --name YourSpeechService --resource-group YourResourceGroup --query "location"

# Test with REST API
curl -X POST "https://eastus.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1" \
  -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
  -H "Content-Type: audio/wav" \
  --data-binary @test.wav
```

---

## 🔒 Security & Authentication Issues

### **JWT Token Problems**

#### **Issue: "401 Unauthorized" responses**
**Debugging Steps**:
```csharp
public class JwtDiagnostics
{
    public async Task<TokenDiagnostic> DiagnoseJwtToken(string token)
    {
        var diagnostic = new TokenDiagnostic();
        
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            // Decode token without validation
            var jsonToken = handler.ReadJwtToken(token);
            
            diagnostic.IsValidFormat = true;
            diagnostic.Claims = jsonToken.Claims.ToDictionary(c => c.Type, c => c.Value);
            diagnostic.ExpiryTime = jsonToken.ValidTo;
            diagnostic.IsExpired = jsonToken.ValidTo < DateTime.UtcNow;
            diagnostic.Issuer = jsonToken.Issuer;
            diagnostic.Audience = jsonToken.Audiences.FirstOrDefault();
            
            // Check expiry
            if (diagnostic.IsExpired)
            {
                diagnostic.Issues.Add($"Token expired at {diagnostic.ExpiryTime}");
            }
            
            // Check required claims
            if (!diagnostic.Claims.ContainsKey("sub"))
            {
                diagnostic.Issues.Add("Missing 'sub' claim");
            }
            
        }
        catch (Exception ex)
        {
            diagnostic.IsValidFormat = false;
            diagnostic.Issues.Add($"Token format error: {ex.Message}");
        }
        
        return diagnostic;
    }
}
```

**Common JWT Issues & Solutions**:
```csharp
// Issue: Token validation fails
// Solution: Check issuer and audience configuration
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://login.microsoftonline.com/your-tenant";
        options.Audience = "your-app-id";
        
        // For debugging: log validation failures
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                _logger.LogError("JWT validation failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });
```

### **CORS Issues**

#### **Issue: "CORS policy blocks request"**
**Solution**:
```csharp
// Configure CORS properly
public void ConfigureServices(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder
                .WithOrigins("https://localhost:7000", "https://yourdomain.com")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials(); // Important for SignalR
        });
    });
}

public void Configure(IApplicationBuilder app)
{
    app.UseCors(); // Must be before UseAuthentication
    app.UseAuthentication();
    app.UseAuthorization();
}
```

---

## 📡 SignalR & Real-time Communication Issues

### **Connection Problems**

#### **Issue: SignalR connections failing**
**Debugging Steps**:
```javascript
// Client-side debugging
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/transcriptionhub", {
        accessTokenFactory: () => getAccessToken(),
        transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.Debug) // Enable detailed logging
    .build();

connection.onclose(error => {
    console.error("SignalR connection closed:", error);
});

connection.onreconnecting(error => {
    console.warn("SignalR reconnecting:", error);
});

connection.onreconnected(connectionId => {
    console.info("SignalR reconnected:", connectionId);
});
```

**Server-side debugging**:
```csharp
public class TranscriptionHub : Hub
{
    private readonly ILogger<TranscriptionHub> _logger;
    
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.UserIdentifier);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### **Performance Issues**

#### **Issue: SignalR messages delayed or lost**
**Solutions**:
```csharp
// Configure message buffer size
services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
    options.StreamBufferCapacity = 10;
    options.MaximumParallelInvocationsPerClient = 2;
});

// Use groups efficiently
public async Task JoinPatientGroup(string patientId)
{
    var groupName = $"patient-{patientId}";
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    
    // Notify others in group (optional)
    await Clients.OthersInGroup(groupName)
        .SendAsync("UserJoined", Context.UserIdentifier);
}
```

---

## 🗄️ Database Issues

### **Performance Problems**

#### **Issue: Slow database queries**
**Debugging Steps**:
```sql
-- Find slow queries
SELECT 
    st.text,
    qs.execution_count,
    qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY qs.total_elapsed_time DESC;

-- Check index usage
SELECT 
    i.name AS IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates
FROM sys.dm_db_index_usage_stats s
JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE s.database_id = DB_ID()
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;
```

**Solutions**:
```csharp
// Add proper indexes
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Index for patient lookups
    modelBuilder.Entity<Patient>()
        .HasIndex(p => p.MedicalRecordNumber)
        .IsUnique();
    
    // Composite index for transcription queries
    modelBuilder.Entity<Transcription>()
        .HasIndex(t => new { t.PatientId, t.CreatedDate })
        .HasDatabaseName("IX_Transcription_PatientId_CreatedDate");
    
    // Index for audit queries
    modelBuilder.Entity<AuditLog>()
        .HasIndex(a => new { a.UserId, a.Timestamp })
        .HasDatabaseName("IX_AuditLog_UserId_Timestamp");
}
```

### **Entity Framework Issues**

#### **Issue: "Cannot insert NULL value" errors**
**Solution**:
```csharp
// Check entity validation
public class Patient
{
    [Required]
    [StringLength(50)]
    public string MedicalRecordNumber { get; set; }
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; }
    
    // Use nullable for optional fields
    public DateTime? LastVisit { get; set; }
}

// Add validation before saving
public async Task<Patient> CreatePatientAsync(Patient patient)
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(patient);
    
    if (!Validator.TryValidateObject(patient, validationContext, validationResults, true))
    {
        var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
        throw new ValidationException($"Patient validation failed: {errors}");
    }
    
    _context.Patients.Add(patient);
    await _context.SaveChangesAsync();
    return patient;
}
```

---

## 🚀 Performance Troubleshooting

### **Application Performance Issues**

#### **Issue: High memory usage**
**Debugging Tools**:
```bash
# Monitor memory usage (Windows)
Get-Counter -Counter "\Process(w3wp)\Working Set - Private" -SampleInterval 5 -MaxSamples 20

# Monitor memory usage (Linux)
ps aux | grep dotnet
top -p <pid>

# Generate memory dump for analysis
dotnet-dump collect -p <process-id>
```

**Common Memory Issues**:
```csharp
// Issue: Not disposing resources
// Solution: Use 'using' statements
public async Task<string> TranscribeAudioAsync(Stream audioStream)
{
    using var speechConfig = SpeechConfig.FromSubscription(_key, _region);
    using var audioConfig = AudioConfig.FromStreamInput(audioStream);
    using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
    
    var result = await recognizer.RecognizeOnceAsync();
    return result.Text;
}

// Issue: Large object heap pressure
// Solution: Streaming instead of loading entire files
public async Task ProcessLargeAudioFile(Stream audioStream)
{
    const int bufferSize = 4096;
    var buffer = new byte[bufferSize];
    
    while (await audioStream.ReadAsync(buffer, 0, bufferSize) > 0)
    {
        await ProcessAudioChunk(buffer);
    }
}
```

#### **Issue: High CPU usage**
**Common Causes & Solutions**:
```csharp
// Cause: Inefficient LINQ queries
// Bad:
var results = patients.Where(p => p.Transcriptions.Any(t => t.Text.Contains(searchTerm)));

// Good:
var results = await _context.Patients
    .Where(p => p.Transcriptions.Any(t => EF.Functions.Contains(t.Text, searchTerm)))
    .ToListAsync();

// Cause: Blocking async calls
// Bad:
var result = SomeAsyncMethod().Result;

// Good:
var result = await SomeAsyncMethod();
```

---

## 📋 Systematic Troubleshooting Checklist

### **When Something Isn't Working**

#### **1. Identify the Problem**
- [ ] What exactly is failing?
- [ ] When did it start failing?
- [ ] What changed recently?
- [ ] Can you reproduce the issue?
- [ ] Is it affecting all users or just some?

#### **2. Check the Basics**
- [ ] Are all services running?
- [ ] Is the database accessible?
- [ ] Are configuration values correct?
- [ ] Is the network connection working?
- [ ] Are there any recent deployments?

#### **3. Examine Logs**
```bash
# Check application logs
tail -f logs/app.log

# Check IIS logs (Windows)
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\*.log" | Select-Object -Last 100

# Check system event logs
Get-WinEvent -LogName Application -MaxEvents 50
```

#### **4. Test Components Individually**
- [ ] Database connection test
- [ ] Azure Speech service test
- [ ] Authentication test
- [ ] SignalR connection test
- [ ] API endpoint test

#### **5. Monitor Resources**
- [ ] CPU usage
- [ ] Memory usage
- [ ] Disk space
- [ ] Network bandwidth
- [ ] Database connections

---

## 🔧 Diagnostic Tools & Scripts

### **Health Check Endpoint**
```csharp
// Add comprehensive health checks
public void ConfigureServices(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddDbContext<ApplicationDbContext>()
        .AddUrlGroup(new Uri("https://eastus.api.cognitive.microsoft.com/"), "Azure Speech")
        .AddSignalRHub("/transcriptionhub")
        .AddCheck<CustomHealthCheck>("custom-check");
}

public class CustomHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform custom health checks
            var speechServiceHealthy = await CheckSpeechService();
            var databaseHealthy = await CheckDatabase();
            
            if (speechServiceHealthy && databaseHealthy)
                return HealthCheckResult.Healthy("All systems operational");
            else
                return HealthCheckResult.Degraded("Some services degraded");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Health check failed", ex);
        }
    }
}
```

### **Diagnostic PowerShell Script**
```powershell
# Medical Dictation Service Diagnostic Script
function Test-MedicalDictationService {
    Write-Host "=== Medical Dictation Service Diagnostics ===" -ForegroundColor Green
    
    # Test 1: Check if application is running
    $process = Get-Process -Name "w3wp" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "✓ Application process running" -ForegroundColor Green
    } else {
        Write-Host "✗ Application process not found" -ForegroundColor Red
    }
    
    # Test 2: Check database connectivity
    try {
        $connectionString = "Server=(localdb)\mssqllocaldb;Database=MedicalDictationDb;Integrated Security=true"
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        $connection.Close()
        Write-Host "✓ Database connection successful" -ForegroundColor Green
    } catch {
        Write-Host "✗ Database connection failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 3: Check web endpoint
    try {
        $response = Invoke-WebRequest -Uri "https://localhost:7000/health" -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "✓ Web endpoint responding" -ForegroundColor Green
        }
    } catch {
        Write-Host "✗ Web endpoint not responding: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test 4: Check disk space
    $disk = Get-WmiObject -Class Win32_LogicalDisk -Filter "DeviceID='C:'"
    $freeSpaceGB = [math]::Round($disk.FreeSpace / 1GB, 2)
    if ($freeSpaceGB -gt 5) {
        Write-Host "✓ Disk space sufficient: $freeSpaceGB GB free" -ForegroundColor Green
    } else {
        Write-Host "⚠ Low disk space: $freeSpaceGB GB free" -ForegroundColor Yellow
    }
}

# Run diagnostics
Test-MedicalDictationService
```

---

**Having a specific issue?** Use the search function (Ctrl+F) to find relevant solutions, or contact the development team with the diagnostic information from the health check endpoint.

---

> **Troubleshooting Principle**: *"Systematic diagnosis and clear documentation of issues enables faster resolution and prevents recurrence."* 