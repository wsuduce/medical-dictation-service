using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using MedicalDictationService.Services;
using MedicalDictationService.Models;
using System.Security.Claims;

namespace MedicalDictationService.Hubs;

/// <summary>
/// SignalR Hub for real-time transcription communication
/// </summary>
// [Authorize] // Commented out for demo mode - will re-enable in production
public class TranscriptionHub : Hub
{
    private readonly IAzureSpeechService _azureSpeechService;
    private readonly ILogger<TranscriptionHub> _logger;
    private static readonly Dictionary<string, string> _connectionSessions = new();

    public TranscriptionHub(
        IAzureSpeechService azureSpeechService,
        ILogger<TranscriptionHub> logger)
    {
        _azureSpeechService = azureSpeechService;
        _logger = logger;
        
        // Subscribe to speech service events
        _azureSpeechService.TranscriptionReceived += OnTranscriptionReceived;
        _azureSpeechService.SessionStatusChanged += OnSessionStatusChanged;
        _azureSpeechService.AudioQualityChanged += OnAudioQualityChanged;
        _azureSpeechService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Simple test method to verify SignalR connectivity
    /// </summary>
    public async Task TestConnection()
    {
        _logger.LogInformation("🧪 TestConnection method called in hub - SignalR is working!");
        await Clients.Caller.SendAsync("TestResponse", "Hello from SignalR Hub!");
    }

    /// <summary>
    /// Test method with exact same name as the failing one
    /// </summary>
    public async Task StartTranscriptionTest()
    {
        _logger.LogInformation("🔥 StartTranscriptionTest method called in hub - THIS WORKS!");
        await Clients.Caller.SendAsync("TestResponse", "StartTranscriptionTest method works!");
    }

    /// <summary>
    /// Starts a new transcription session
    /// </summary>
    public async Task StartTranscription(string? patientId = null)
    {
        _logger.LogInformation("🚀 StartTranscription method called in hub");
        
        try
        {
            var userId = GetUserId();
            _logger.LogInformation($"📋 GetUserId() returned: '{userId}'");
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("❌ User not authenticated - userId is null or empty");
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            _logger.LogInformation($"🔄 Calling AzureSpeechService.StartTranscriptionAsync for user: {userId}");
            var session = await _azureSpeechService.StartTranscriptionAsync(userId, patientId);
            _logger.LogInformation($"✅ AzureSpeechService returned session: {session.Id} with status: {session.Status}");
            
            // Associate this connection with the session
            _connectionSessions[Context.ConnectionId] = session.Id;
            _logger.LogInformation($"🔗 Associated connection {Context.ConnectionId} with session {session.Id}");
            
            _logger.LogInformation($"📤 Sending SessionStarted event to client...");
            await Clients.Caller.SendAsync("SessionStarted", new
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                StartTime = session.StartTime
            });

            _logger.LogInformation($"🎉 Successfully started transcription session {session.Id} for user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 CRITICAL ERROR in StartTranscription method");
            _logger.LogError($"Exception Type: {ex.GetType().Name}");
            _logger.LogError($"Exception Message: {ex.Message}");
            _logger.LogError($"Stack Trace: {ex.StackTrace}");
            
            await Clients.Caller.SendAsync("Error", $"Failed to start transcription session: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the current transcription session
    /// </summary>
    public async Task StopTranscription()
    {
        try
        {
            if (!_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
            {
                await Clients.Caller.SendAsync("Error", "No active session found");
                return;
            }

            var session = await _azureSpeechService.StopTranscriptionAsync(sessionId);
            
            await Clients.Caller.SendAsync("SessionStopped", new
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                EndTime = session.EndTime,
                FinalTranscription = session.FinalTranscription
            });

            _connectionSessions.Remove(Context.ConnectionId);
            _logger.LogInformation($"Stopped transcription session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping transcription session");
            await Clients.Caller.SendAsync("Error", "Failed to stop transcription session");
        }
    }

    /// <summary>
    /// Pauses the current transcription session
    /// </summary>
    public async Task PauseTranscription()
    {
        try
        {
            if (!_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
            {
                await Clients.Caller.SendAsync("Error", "No active session found");
                return;
            }

            var session = await _azureSpeechService.PauseTranscriptionAsync(sessionId);
            
            await Clients.Caller.SendAsync("SessionPaused", new
            {
                SessionId = session.Id,
                Status = session.Status.ToString()
            });

            _logger.LogInformation($"Paused transcription session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing transcription session");
            await Clients.Caller.SendAsync("Error", "Failed to pause transcription session");
        }
    }

    /// <summary>
    /// Resumes the current transcription session
    /// </summary>
    public async Task ResumeTranscription()
    {
        try
        {
            if (!_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
            {
                await Clients.Caller.SendAsync("Error", "No active session found");
                return;
            }

            var session = await _azureSpeechService.ResumeTranscriptionAsync(sessionId);
            
            await Clients.Caller.SendAsync("SessionResumed", new
            {
                SessionId = session.Id,
                Status = session.Status.ToString()
            });

            _logger.LogInformation($"Resumed transcription session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming transcription session");
            await Clients.Caller.SendAsync("Error", "Failed to resume transcription session");
        }
    }

    /// <summary>
    /// Processes audio data from the client
    /// </summary>
    public async Task SendAudioData(byte[] audioData)
    {
        try
        {
            if (!_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
            {
                await Clients.Caller.SendAsync("Error", "No active session found");
                return;
            }

            await _azureSpeechService.ProcessAudioAsync(sessionId, audioData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audio data");
            await Clients.Caller.SendAsync("Error", "Failed to process audio data");
        }
    }

    /// <summary>
    /// Gets the current session status
    /// </summary>
    public async Task GetSessionStatus()
    {
        try
        {
            if (!_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
            {
                await Clients.Caller.SendAsync("SessionStatus", new { Status = "NoSession" });
                return;
            }

            var session = await _azureSpeechService.GetSessionAsync(sessionId);
            if (session == null)
            {
                await Clients.Caller.SendAsync("SessionStatus", new { Status = "NotFound" });
                return;
            }

            await Clients.Caller.SendAsync("SessionStatus", new
            {
                SessionId = session.Id,
                Status = session.Status.ToString(),
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                ResultCount = session.Results.Count,
                FinalTranscription = session.FinalTranscription
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session status");
            await Clients.Caller.SendAsync("Error", "Failed to get session status");
        }
    }

    /// <summary>
    /// Gets all active sessions for the current user
    /// </summary>
    public async Task GetActiveSessions()
    {
        try
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var sessions = await _azureSpeechService.GetActiveSessionsAsync(userId);
            
            await Clients.Caller.SendAsync("ActiveSessions", sessions.Select(s => new
            {
                SessionId = s.Id,
                Status = s.Status.ToString(),
                StartTime = s.StartTime,
                PatientId = s.PatientId,
                ResultCount = s.Results.Count
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active sessions");
            await Clients.Caller.SendAsync("Error", "Failed to get active sessions");
        }
    }

    /// <summary>
    /// Handles client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation($"Client connected: {Context.ConnectionId}, User: {userId}");
        
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = Context.ConnectionId,
            UserId = userId,
            ConnectedAt = DateTime.UtcNow
        });
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handles client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation($"Client disconnected: {Context.ConnectionId}, User: {userId}");

        // Clean up any active sessions for this connection
        if (_connectionSessions.TryGetValue(Context.ConnectionId, out var sessionId))
        {
            try
            {
                await _azureSpeechService.CleanupSessionAsync(sessionId);
                _connectionSessions.Remove(Context.ConnectionId);
                _logger.LogInformation($"Cleaned up session {sessionId} for disconnected client");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cleaning up session {sessionId}");
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    #region Private Event Handlers

    private async void OnTranscriptionReceived(object? sender, TranscriptionEvent e)
    {
        try
        {
            // Find the connection associated with this session
            var connectionId = _connectionSessions.FirstOrDefault(cs => cs.Value == e.SessionId).Key;
            if (!string.IsNullOrEmpty(connectionId))
            {
                await Clients.Client(connectionId).SendAsync("TranscriptionReceived", new
                {
                    Type = e.Type,
                    SessionId = e.SessionId,
                    Text = e.Result?.Text,
                    Confidence = e.Result?.Confidence,
                    DetectedSection = e.Result?.DetectedSection.ToString(),
                    AudioQuality = e.Result?.AudioQuality.ToString(),
                    MedicalTermsCount = e.Result?.MedicalTerms.Count ?? 0,
                    IsIntermediate = e.Result?.IsIntermediate ?? false,
                    Timestamp = e.Timestamp
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transcription result to client");
        }
    }

    private async void OnSessionStatusChanged(object? sender, TranscriptionEvent e)
    {
        try
        {
            var connectionId = _connectionSessions.FirstOrDefault(cs => cs.Value == e.SessionId).Key;
            if (!string.IsNullOrEmpty(connectionId))
            {
                await Clients.Client(connectionId).SendAsync("SessionStatusChanged", new
                {
                    Type = e.Type,
                    SessionId = e.SessionId,
                    Timestamp = e.Timestamp,
                    Data = e.Data
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending session status change to client");
        }
    }

    private async void OnAudioQualityChanged(object? sender, TranscriptionEvent e)
    {
        try
        {
            var connectionId = _connectionSessions.FirstOrDefault(cs => cs.Value == e.SessionId).Key;
            if (!string.IsNullOrEmpty(connectionId))
            {
                await Clients.Client(connectionId).SendAsync("AudioQualityChanged", new
                {
                    SessionId = e.SessionId,
                    AudioQuality = e.AudioQuality?.ToString(),
                    Timestamp = e.Timestamp
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audio quality update to client");
        }
    }

    private async void OnErrorOccurred(object? sender, TranscriptionEvent e)
    {
        try
        {
            var connectionId = _connectionSessions.FirstOrDefault(cs => cs.Value == e.SessionId).Key;
            if (!string.IsNullOrEmpty(connectionId))
            {
                await Clients.Client(connectionId).SendAsync("TranscriptionError", new
                {
                    SessionId = e.SessionId,
                    ErrorMessage = e.ErrorMessage,
                    Timestamp = e.Timestamp
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error notification to client");
        }
    }

    #endregion

    #region Helper Methods

    private string? GetUserId()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Since authorization is disabled for demo mode, provide a default user ID
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("⚠️ No authenticated user found, using demo user ID");
            return "demo-user";
        }
        
        return userId;
    }

    #endregion
} 