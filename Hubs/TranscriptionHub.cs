using MedicalDictationService.Models;
using MedicalDictationService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MedicalDictationService.Hubs;

/// <summary>
/// SignalR hub for real-time transcription communication
/// </summary>
[Authorize]
public class TranscriptionHub : Hub
{
    private readonly IAzureSpeechService _speechService;
    private readonly ILogger<TranscriptionHub> _logger;

    public TranscriptionHub(IAzureSpeechService speechService, ILogger<TranscriptionHub> logger)
    {
        _speechService = speechService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new transcription session
    /// </summary>
    public async Task StartTranscriptionSession(string sessionId, string? patientId = null)
    {
        try
        {
            _logger.LogInformation("Starting transcription session {SessionId} for user {UserId}", 
                sessionId, Context.UserIdentifier);

            // Join the session group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");

            // Start Azure Speech recognition
            var success = await _speechService.StartStreamingRecognitionAsync(sessionId);
            
            if (success)
            {
                await Clients.Group($"session_{sessionId}").SendAsync("SessionStarted", new TranscriptionEvent
                {
                    SessionId = sessionId,
                    EventType = "session_start",
                    Data = new Dictionary<string, object> { ["PatientId"] = patientId ?? "" }
                });

                _logger.LogInformation("Transcription session {SessionId} started successfully", sessionId);
            }
            else
            {
                await Clients.Caller.SendAsync("SessionError", new TranscriptionEvent
                {
                    SessionId = sessionId,
                    EventType = "error",
                    ErrorMessage = "Failed to start transcription session"
                });

                _logger.LogError("Failed to start transcription session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting transcription session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("SessionError", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "error",
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Stop a transcription session
    /// </summary>
    public async Task StopTranscriptionSession(string sessionId)
    {
        try
        {
            _logger.LogInformation("Stopping transcription session {SessionId} for user {UserId}", 
                sessionId, Context.UserIdentifier);

            // Stop Azure Speech recognition
            var success = await _speechService.StopStreamingRecognitionAsync(sessionId);

            if (success)
            {
                await Clients.Group($"session_{sessionId}").SendAsync("SessionStopped", new TranscriptionEvent
                {
                    SessionId = sessionId,
                    EventType = "session_end"
                });

                _logger.LogInformation("Transcription session {SessionId} stopped successfully", sessionId);
            }

            // Remove from session group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping transcription session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("SessionError", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "error",
                ErrorMessage = ex.Message
            });
        }
    }

    /// <summary>
    /// Pause a transcription session
    /// </summary>
    public async Task PauseTranscriptionSession(string sessionId)
    {
        try
        {
            _logger.LogInformation("Pausing transcription session {SessionId}", sessionId);

            await Clients.Group($"session_{sessionId}").SendAsync("SessionPaused", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "session_pause"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing transcription session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Resume a transcription session
    /// </summary>
    public async Task ResumeTranscriptionSession(string sessionId)
    {
        try
        {
            _logger.LogInformation("Resuming transcription session {SessionId}", sessionId);

            await Clients.Group($"session_{sessionId}").SendAsync("SessionResumed", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "session_resume"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming transcription session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Send audio quality feedback to clients
    /// </summary>
    public async Task SendAudioQualityUpdate(string sessionId, AudioQuality quality)
    {
        try
        {
            await Clients.Group($"session_{sessionId}").SendAsync("AudioQualityUpdate", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "audio_quality",
                Data = new Dictionary<string, object> { ["Quality"] = quality.ToString() }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending audio quality update for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Handle client connection
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}, User: {UserId}", 
            Context.ConnectionId, Context.UserIdentifier);
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Handle client disconnection
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}, User: {UserId}, Exception: {Exception}", 
            Context.ConnectionId, Context.UserIdentifier, exception?.Message);

        // Clean up any active sessions for this connection
        // In a production system, you'd track active sessions per connection
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Get session status
    /// </summary>
    public async Task GetSessionStatus(string sessionId)
    {
        try
        {
            var metrics = await _speechService.GetServiceMetricsAsync();
            
            await Clients.Caller.SendAsync("SessionStatus", new TranscriptionEvent
            {
                SessionId = sessionId,
                EventType = "session_status",
                Data = metrics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session status for {SessionId}", sessionId);
        }
    }
} 