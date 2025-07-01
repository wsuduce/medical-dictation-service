using MedicalDictationService.Models;

namespace MedicalDictationService.Services;

/// <summary>
/// Interface for Azure Speech Services integration
/// </summary>
public interface IAzureSpeechService
{
    /// <summary>
    /// Starts a new transcription session
    /// </summary>
    Task<TranscriptionSession> StartTranscriptionAsync(string userId, string? patientId = null, AudioConfig? config = null);

    /// <summary>
    /// Stops an active transcription session
    /// </summary>
    Task<TranscriptionSession> StopTranscriptionAsync(string sessionId);

    /// <summary>
    /// Pauses an active transcription session
    /// </summary>
    Task<TranscriptionSession> PauseTranscriptionAsync(string sessionId);

    /// <summary>
    /// Resumes a paused transcription session
    /// </summary>
    Task<TranscriptionSession> ResumeTranscriptionAsync(string sessionId);

    /// <summary>
    /// Processes audio data for continuous recognition
    /// </summary>
    Task ProcessAudioAsync(string sessionId, byte[] audioData);

    /// <summary>
    /// Gets the current status of a transcription session
    /// </summary>
    Task<TranscriptionSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Gets all active sessions for a user
    /// </summary>
    Task<List<TranscriptionSession>> GetActiveSessionsAsync(string userId);

    /// <summary>
    /// Event fired when transcription results are available
    /// </summary>
    event EventHandler<TranscriptionEvent> TranscriptionReceived;

    /// <summary>
    /// Event fired when audio quality changes
    /// </summary>
    event EventHandler<TranscriptionEvent> AudioQualityChanged;

    /// <summary>
    /// Event fired when session status changes
    /// </summary>
    event EventHandler<TranscriptionEvent> SessionStatusChanged;

    /// <summary>
    /// Event fired when an error occurs
    /// </summary>
    event EventHandler<TranscriptionEvent> ErrorOccurred;

    /// <summary>
    /// Checks if the service is healthy and can connect to Azure
    /// </summary>
    Task<bool> HealthCheckAsync();

    /// <summary>
    /// Cleans up resources for a session
    /// </summary>
    Task CleanupSessionAsync(string sessionId);
} 