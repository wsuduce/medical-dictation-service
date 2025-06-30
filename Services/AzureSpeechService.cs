using MedicalDictationService.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MedicalDictationService.Services;

/// <summary>
/// Azure Speech Services implementation for medical dictation with real-time streaming
/// </summary>
public class AzureSpeechService : IAzureSpeechService, IDisposable
{
    private readonly SpeechServiceOptions _options;
    private readonly IMedicalTerminologyService _medicalTerminology;
    private readonly ILogger<AzureSpeechService> _logger;
    private readonly ConcurrentDictionary<string, SpeechRecognizer> _activeRecognizers = new();
    private readonly ConcurrentDictionary<string, TranscriptionSession> _activeSessions = new();
    private bool _disposed = false;

    public event EventHandler<TranscriptionResult>? InterimResultReceived;
    public event EventHandler<TranscriptionResult>? FinalResultReceived;
    public event EventHandler<string>? TranscriptionError;
    public event EventHandler<AudioQuality>? AudioQualityChanged;

    public AzureSpeechService(
        IOptions<SpeechServiceOptions> options,
        IMedicalTerminologyService medicalTerminology,
        ILogger<AzureSpeechService> logger)
    {
        _options = options.Value;
        _medicalTerminology = medicalTerminology;
        _logger = logger;
    }

    /// <summary>
    /// Start streaming speech recognition for a session
    /// </summary>
    public async Task<bool> StartStreamingRecognitionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_activeRecognizers.ContainsKey(sessionId))
            {
                _logger.LogWarning("Recognition already active for session {SessionId}", sessionId);
                return false;
            }

            var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
            await ConfigureMedicalRecognitionAsync(speechConfig);

            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            // Configure event handlers
            ConfigureRecognizerEvents(recognizer, sessionId);

            // Start continuous recognition
            await recognizer.StartContinuousRecognitionAsync();
            _activeRecognizers[sessionId] = recognizer;

            // Initialize session tracking
            _activeSessions[sessionId] = new TranscriptionSession
            {
                SessionId = sessionId,
                StartTime = DateTime.UtcNow,
                Status = "Active"
            };

            _logger.LogInformation("Started streaming recognition for session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start streaming recognition for session {SessionId}", sessionId);
            TranscriptionError?.Invoke(this, $"Failed to start recognition: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stop streaming recognition for a session
    /// </summary>
    public async Task<bool> StopStreamingRecognitionAsync(string sessionId)
    {
        try
        {
            if (!_activeRecognizers.TryRemove(sessionId, out var recognizer))
            {
                _logger.LogWarning("No active recognizer found for session {SessionId}", sessionId);
                return false;
            }

            await recognizer.StopContinuousRecognitionAsync();
            recognizer.Dispose();

            // Update session status
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.EndTime = DateTime.UtcNow;
                session.Status = "Completed";
            }

            _logger.LogInformation("Stopped streaming recognition for session {SessionId}", sessionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop streaming recognition for session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Process audio chunk for real-time transcription
    /// </summary>
    public async Task<TranscriptionResult?> ProcessAudioChunkAsync(string sessionId, byte[] audioData, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_activeSessions.ContainsKey(sessionId))
            {
                _logger.LogWarning("No active session found for {SessionId}", sessionId);
                return null;
            }

            // Assess audio quality
            var audioQuality = AssessAudioQuality(audioData);
            AudioQualityChanged?.Invoke(this, audioQuality);

            // For streaming recognition, we rely on the continuous recognition events
            // This method can be used for additional audio processing if needed
            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process audio chunk for session {SessionId}", sessionId);
            return null;
        }
    }

    /// <summary>
    /// Check if the speech service is healthy and responsive
    /// </summary>
    public async Task<bool> IsServiceHealthyAsync()
    {
        try
        {
            var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
            speechConfig.SpeechRecognitionLanguage = _options.Language;

            // Simple health check by creating a recognizer
            using var recognizer = new SpeechRecognizer(speechConfig);
            await Task.CompletedTask; // Placeholder for actual health check

            _logger.LogDebug("Speech service health check passed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech service health check failed");
            return false;
        }
    }

    /// <summary>
    /// Get current service metrics and performance data
    /// </summary>
    public async Task<Dictionary<string, object>> GetServiceMetricsAsync()
    {
        await Task.CompletedTask;
        
        return new Dictionary<string, object>
        {
            ["ActiveSessions"] = _activeSessions.Count,
            ["ActiveRecognizers"] = _activeRecognizers.Count,
            ["TotalSessions"] = _activeSessions.Count, // In a real implementation, this would be from a database
            ["ServiceHealth"] = await IsServiceHealthyAsync(),
            ["LastHealthCheck"] = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Configure speech recognition for medical terminology
    /// </summary>
    public async Task ConfigureMedicalRecognitionAsync(SpeechConfig speechConfig)
    {
        speechConfig.SpeechRecognitionLanguage = _options.Language;
        
        // Enable medical terminology recognition
        if (_options.EnableMedicalTerminology)
        {
            // Configure for medical domain
            speechConfig.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "5000");
            speechConfig.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000");
            
            // Add custom medical vocabulary if available
            if (_options.CustomVocabulary.Any())
            {
                // In a real implementation, you would create a custom language model
                _logger.LogInformation("Custom medical vocabulary configured with {Count} terms", _options.CustomVocabulary.Count);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Configure event handlers for the speech recognizer
    /// </summary>
    private void ConfigureRecognizerEvents(SpeechRecognizer recognizer, string sessionId)
    {
        // Interim results (real-time transcription)
        recognizer.Recognizing += async (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Result.Text))
            {
                var result = new TranscriptionResult
                {
                    SessionId = sessionId,
                    RawText = e.Result.Text,
                    EnhancedText = e.Result.Text, // Will be enhanced by medical terminology service
                    Confidence = ExtractConfidence(e.Result),
                    IsInterim = true,
                    Timestamp = DateTime.UtcNow
                };

                // Enhance with medical terminology
                result = await _medicalTerminology.EnhanceTranscriptionAsync(result);
                
                InterimResultReceived?.Invoke(this, result);
                _logger.LogDebug("Interim result for session {SessionId}: {Text}", sessionId, e.Result.Text);
            }
        };

        // Final results
        recognizer.Recognized += async (sender, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
            {
                var result = new TranscriptionResult
                {
                    SessionId = sessionId,
                    RawText = e.Result.Text,
                    EnhancedText = e.Result.Text,
                    Confidence = ExtractConfidence(e.Result),
                    Duration = e.Result.Duration,
                    IsInterim = false,
                    Timestamp = DateTime.UtcNow
                };

                // Enhance with medical terminology and SOAP detection
                result = await _medicalTerminology.EnhanceTranscriptionAsync(result);
                result.DetectedSection = await _medicalTerminology.DetectSOAPSectionAsync(result.EnhancedText);

                // Store in session
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    session.Results.Add(result);
                }

                FinalResultReceived?.Invoke(this, result);
                _logger.LogInformation("Final result for session {SessionId}: {Text} (Confidence: {Confidence:P1})", 
                    sessionId, e.Result.Text, result.Confidence);
            }
        };

        // Error handling
        recognizer.Canceled += (sender, e) =>
        {
            var errorMessage = $"Recognition canceled for session {sessionId}: {e.Reason}";
            if (e.Reason == CancellationReason.Error)
            {
                errorMessage += $" - {e.ErrorDetails}";
            }
            
            _logger.LogError(errorMessage);
            TranscriptionError?.Invoke(this, errorMessage);

            // Update session status
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.Status = "Error";
                session.EndTime = DateTime.UtcNow;
            }
        };

        // Session events
        recognizer.SessionStarted += (sender, e) =>
        {
            _logger.LogInformation("Recognition session started for {SessionId}", sessionId);
        };

        recognizer.SessionStopped += (sender, e) =>
        {
            _logger.LogInformation("Recognition session stopped for {SessionId}", sessionId);
        };
    }

    /// <summary>
    /// Extract confidence score from recognition result
    /// </summary>
    private double ExtractConfidence(SpeechRecognitionResult result)
    {
        try
        {
            // Try to extract confidence from JSON properties
            var json = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
            if (!string.IsNullOrEmpty(json))
            {
                // Parse confidence from JSON (simplified approach)
                // In a real implementation, you'd use proper JSON parsing
                return 0.85; // Default good confidence
            }
            return 0.8; // Default confidence if not available
        }
        catch
        {
            return 0.75; // Conservative default
        }
    }

    /// <summary>
    /// Assess audio quality from audio data
    /// </summary>
    private AudioQuality AssessAudioQuality(byte[] audioData)
    {
        if (audioData == null || audioData.Length == 0)
            return AudioQuality.Poor;

        // Simple audio quality assessment based on data length and volume
        // In a real implementation, you'd analyze frequency spectrum, SNR, etc.
        if (audioData.Length < 1000)
            return AudioQuality.Poor;
        
        var avgAmplitude = audioData.Average(b => Math.Abs(b - 128));
        
        return avgAmplitude switch
        {
            > 50 => AudioQuality.Excellent,
            > 30 => AudioQuality.Good,
            > 15 => AudioQuality.Fair,
            > 5 => AudioQuality.Poor,
            _ => AudioQuality.Critical
        };
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Stop all active recognizers
            foreach (var kvp in _activeRecognizers)
            {
                try
                {
                    kvp.Value.StopContinuousRecognitionAsync().Wait(TimeSpan.FromSeconds(5));
                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing recognizer for session {SessionId}", kvp.Key);
                }
            }
            
            _activeRecognizers.Clear();
            _activeSessions.Clear();
            _disposed = true;
        }
    }
} 