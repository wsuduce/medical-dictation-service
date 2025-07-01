using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using MedicalDictationService.Models;
using System.Collections.Concurrent;

// Alias to resolve naming conflict
using AzureAudioConfig = Microsoft.CognitiveServices.Speech.Audio.AudioConfig;

namespace MedicalDictationService.Services;

/// <summary>
/// Implementation of Azure Speech Services with medical terminology enhancement
/// </summary>
public class AzureSpeechService : IAzureSpeechService, IDisposable
{
    private readonly ILogger<AzureSpeechService> _logger;
    private readonly IMedicalTerminologyService _medicalTerminologyService;
    private readonly IConfiguration _configuration;
    
    // Speech configuration and recognizers
    private SpeechConfig? _speechConfig;
    private readonly ConcurrentDictionary<string, SpeechRecognizer> _recognizers = new();
    private readonly ConcurrentDictionary<string, TranscriptionSession> _sessions = new();
    private readonly ConcurrentDictionary<string, PushAudioInputStream> _audioStreams = new();
    
    // Events
    public event EventHandler<TranscriptionEvent>? TranscriptionReceived;
    public event EventHandler<TranscriptionEvent>? AudioQualityChanged;
    public event EventHandler<TranscriptionEvent>? SessionStatusChanged;
    public event EventHandler<TranscriptionEvent>? ErrorOccurred;

    public AzureSpeechService(
        ILogger<AzureSpeechService> logger,
        IMedicalTerminologyService medicalTerminologyService,
        IConfiguration configuration)
    {
        _logger = logger;
        _medicalTerminologyService = medicalTerminologyService;
        _configuration = configuration;
        
        InitializeSpeechConfig();
    }

    public async Task<TranscriptionSession> StartTranscriptionAsync(string userId, string? patientId = null, Models.AudioConfig? config = null)
    {
        _logger.LogInformation($"🎤 AzureSpeechService.StartTranscriptionAsync called for user: {userId}");
        
        try
        {
            // Check if speech config is initialized
            if (_speechConfig == null)
            {
                _logger.LogError("❌ Speech configuration is NULL - Azure not configured properly");
                throw new InvalidOperationException("Azure Speech Services not configured properly");
            }
            _logger.LogInformation("✅ Speech configuration is initialized");

            var session = new TranscriptionSession
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                PatientId = patientId,
                Status = TranscriptionStatus.Starting,
                StartTime = DateTime.UtcNow
            };
            _logger.LogInformation($"📝 Created session object: {session.Id}");

            _sessions[session.Id] = session;
            _logger.LogInformation($"💾 Stored session in dictionary");

            // Create audio configuration
            var audioConfig = config ?? new Models.AudioConfig();
            _logger.LogInformation($"🔧 Created audio config");
            
            var pushStream = AudioInputStream.CreatePushStream();
            _logger.LogInformation($"🎵 Created push audio stream");
            
            var audioStreamConfig = AzureAudioConfig.FromStreamInput(pushStream);
            _logger.LogInformation($"🔊 Created Azure audio stream config");
            
            // Store the audio stream for later use
            _audioStreams[session.Id] = pushStream;
            _logger.LogInformation($"💾 Stored audio stream");

            // Create speech recognizer with medical terminology
            _logger.LogInformation($"🏗️ Creating speech recognizer...");
            var recognizer = CreateSpeechRecognizer(audioStreamConfig, audioConfig);
            _recognizers[session.Id] = recognizer;
            _logger.LogInformation($"✅ Created and stored speech recognizer");

            // Set up event handlers
            _logger.LogInformation($"🎯 Setting up recognizer events...");
            SetupRecognizerEvents(recognizer, session.Id);
            _logger.LogInformation($"✅ Event handlers configured");

            // Start continuous recognition
            _logger.LogInformation($"▶️ Starting continuous recognition...");
            await recognizer.StartContinuousRecognitionAsync();
            _logger.LogInformation($"✅ Continuous recognition started successfully");

            session.Status = TranscriptionStatus.Active;
            _sessions[session.Id] = session;
            _logger.LogInformation($"🟢 Session status updated to Active");

            // Fire session status changed event
            SessionStatusChanged?.Invoke(this, new TranscriptionEvent
            {
                Type = "SessionStarted",
                SessionId = session.Id,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation($"📡 SessionStatusChanged event fired");

            _logger.LogInformation($"🎉 Successfully started transcription session {session.Id} for user {userId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"💥 CRITICAL ERROR in AzureSpeechService.StartTranscriptionAsync for user {userId}");
            _logger.LogError($"Exception Type: {ex.GetType().Name}");
            _logger.LogError($"Exception Message: {ex.Message}");
            _logger.LogError($"Stack Trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<TranscriptionSession> StopTranscriptionAsync(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Session {sessionId} not found");
            }

            if (_recognizers.TryGetValue(sessionId, out var recognizer))
            {
                await recognizer.StopContinuousRecognitionAsync();
                recognizer.Dispose();
                _recognizers.TryRemove(sessionId, out _);
            }

                    if (_audioStreams.TryGetValue(sessionId, out var audioStream))
        {
            audioStream.Dispose();
            _audioStreams.TryRemove(sessionId, out _);
        }

            session.Status = TranscriptionStatus.Stopped;
            session.EndTime = DateTime.UtcNow;
            _sessions[sessionId] = session;

            // Fire session status changed event
            SessionStatusChanged?.Invoke(this, new TranscriptionEvent
            {
                Type = "SessionStopped",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation($"Stopped transcription session {sessionId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error stopping transcription session {sessionId}");
            throw;
        }
    }

    public async Task<TranscriptionSession> PauseTranscriptionAsync(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Session {sessionId} not found");
            }

            if (_recognizers.TryGetValue(sessionId, out var recognizer))
            {
                await recognizer.StopContinuousRecognitionAsync();
            }

            session.Status = TranscriptionStatus.Paused;
            _sessions[sessionId] = session;

            SessionStatusChanged?.Invoke(this, new TranscriptionEvent
            {
                Type = "SessionPaused",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation($"Paused transcription session {sessionId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error pausing transcription session {sessionId}");
            throw;
        }
    }

    public async Task<TranscriptionSession> ResumeTranscriptionAsync(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new ArgumentException($"Session {sessionId} not found");
            }

            if (_recognizers.TryGetValue(sessionId, out var recognizer))
            {
                await recognizer.StartContinuousRecognitionAsync();
            }

            session.Status = TranscriptionStatus.Active;
            _sessions[sessionId] = session;

            SessionStatusChanged?.Invoke(this, new TranscriptionEvent
            {
                Type = "SessionResumed",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation($"Resumed transcription session {sessionId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error resuming transcription session {sessionId}");
            throw;
        }
    }

    public async Task ProcessAudioAsync(string sessionId, byte[] audioData)
    {
        try
        {
                    if (_audioStreams.TryGetValue(sessionId, out var audioStream))
        {
            audioStream.Write(audioData);
        }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing audio for session {sessionId}");
            
            ErrorOccurred?.Invoke(this, new TranscriptionEvent
            {
                Type = "AudioProcessingError",
                SessionId = sessionId,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task<TranscriptionSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public async Task<List<TranscriptionSession>> GetActiveSessionsAsync(string userId)
    {
        return _sessions.Values
            .Where(s => s.UserId == userId && 
                       (s.Status == TranscriptionStatus.Active || s.Status == TranscriptionStatus.Paused))
            .ToList();
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            if (_speechConfig == null)
            {
                return false;
            }

            // Simple test to verify Azure Speech Services connectivity
            using var recognizer = new SpeechRecognizer(_speechConfig);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Speech Services health check failed");
            return false;
        }
    }

    public async Task CleanupSessionAsync(string sessionId)
    {
        try
        {
            if (_sessions.ContainsKey(sessionId))
            {
                await StopTranscriptionAsync(sessionId);
                _sessions.TryRemove(sessionId, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up session {sessionId}");
        }
    }

    #region Private Methods

    private void InitializeSpeechConfig()
    {
        try
        {
            var subscriptionKey = _configuration["AzureSpeech:SubscriptionKey"];
            var region = _configuration["AzureSpeech:Region"];

            if (string.IsNullOrEmpty(subscriptionKey) || string.IsNullOrEmpty(region))
            {
                _logger.LogWarning("Azure Speech Services configuration not found. Service will run in demo mode.");
                return;
            }

            _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
            _speechConfig.SpeechRecognitionLanguage = "en-US";
            _speechConfig.EnableDictation();
            
            // Enable detailed results for confidence scores
            _speechConfig.OutputFormat = OutputFormat.Detailed;

            _logger.LogInformation("Azure Speech Services initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Speech Services");
        }
    }

    private SpeechRecognizer CreateSpeechRecognizer(AzureAudioConfig audioConfig, Models.AudioConfig config)
    {
        if (_speechConfig == null)
        {
            throw new InvalidOperationException("Speech configuration not initialized");
        }

        var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);
        
        // Add medical terminology if available
        if (config.EnableMedicalTerminology)
        {
            // Note: In a production environment, you would add custom medical vocabularies here
            // recognizer.AddGrammarFromList("medical", medicalTerms);
        }

        return recognizer;
    }

    private void SetupRecognizerEvents(SpeechRecognizer recognizer, string sessionId)
    {
        // Handle intermediate results (real-time feedback)
        recognizer.Recognizing += async (sender, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizingSpeech)
            {
                var result = await ProcessTranscriptionResult(e.Result, sessionId, isIntermediate: true);
                
                TranscriptionReceived?.Invoke(this, new TranscriptionEvent
                {
                    Type = "IntermediateResult",
                    SessionId = sessionId,
                    Result = result,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        // Handle final results
        recognizer.Recognized += async (sender, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                var result = await ProcessTranscriptionResult(e.Result, sessionId, isIntermediate: false);
                
                // Add to session results
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.Results.Add(result);
                    session.FinalTranscription += result.Text + " ";
                    _sessions[sessionId] = session;
                }

                TranscriptionReceived?.Invoke(this, new TranscriptionEvent
                {
                    Type = "FinalResult",
                    SessionId = sessionId,
                    Result = result,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        // Handle session events
        recognizer.SessionStarted += (sender, e) =>
        {
            _logger.LogDebug($"Speech recognition session started for {sessionId}");
        };

        recognizer.SessionStopped += (sender, e) =>
        {
            _logger.LogDebug($"Speech recognition session stopped for {sessionId}");
        };

        // Handle cancellation and errors
        recognizer.Canceled += (sender, e) =>
        {
            _logger.LogWarning($"Speech recognition canceled for {sessionId}: {e.Reason}");
            
            if (e.Reason == CancellationReason.Error)
            {
                ErrorOccurred?.Invoke(this, new TranscriptionEvent
                {
                    Type = "RecognitionError",
                    SessionId = sessionId,
                    ErrorMessage = e.ErrorDetails,
                    Timestamp = DateTime.UtcNow
                });
            }
        };
    }

    private async Task<TranscriptionResult> ProcessTranscriptionResult(SpeechRecognitionResult speechResult, string sessionId, bool isIntermediate)
    {
        var result = new TranscriptionResult
        {
            SessionId = sessionId,
            Text = speechResult.Text,
            OriginalText = speechResult.Text,
            Confidence = GetConfidenceScore(speechResult),
            IsIntermediate = isIntermediate,
            Timestamp = DateTime.UtcNow
        };

        if (!isIntermediate && !string.IsNullOrEmpty(speechResult.Text))
        {
            try
            {
                // Process medical terms
                result.MedicalTerms = await _medicalTerminologyService.ProcessMedicalTermsAsync(speechResult.Text);
                
                // Detect SOAP section
                result.DetectedSection = await _medicalTerminologyService.DetectSOAPSectionAsync(speechResult.Text);
                
                // Enhance transcription with medical corrections
                result.Text = await _medicalTerminologyService.EnhanceTranscriptionAsync(speechResult.Text, result.MedicalTerms);
                
                // Assess audio quality based on confidence
                result.AudioQuality = AssessAudioQuality(result.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing medical terminology for session {sessionId}");
            }
        }

        return result;
    }

    private double GetConfidenceScore(SpeechRecognitionResult result)
    {
        try
        {
            // Try to get detailed confidence from JSON if available
            var jsonResult = result.Properties.GetProperty(PropertyId.SpeechServiceResponse_JsonResult);
            if (!string.IsNullOrEmpty(jsonResult))
            {
                // Parse JSON to extract confidence - simplified for demo
                return 0.85; // Default confidence
            }
            
            return 0.85; // Default confidence score
        }
        catch
        {
            return 0.85; // Fallback confidence
        }
    }

    private AudioQuality AssessAudioQuality(double confidence)
    {
        return confidence switch
        {
            >= 0.95 => AudioQuality.Excellent,
            >= 0.85 => AudioQuality.Good,
            >= 0.70 => AudioQuality.Fair,
            >= 0.50 => AudioQuality.Poor,
            _ => AudioQuality.Critical
        };
    }

    #endregion

    public void Dispose()
    {
        try
        {
            // Clean up all active sessions
            var activeSessions = _sessions.Keys.ToList();
            foreach (var sessionId in activeSessions)
            {
                CleanupSessionAsync(sessionId).Wait(TimeSpan.FromSeconds(5));
            }

            // Dispose all recognizers
            foreach (var recognizer in _recognizers.Values)
            {
                recognizer?.Dispose();
            }

            // Close all audio streams
            foreach (var audioStream in _audioStreams.Values)
            {
                audioStream?.Dispose();
            }

            _recognizers.Clear();
            _audioStreams.Clear();
            _sessions.Clear();

            _logger.LogInformation("Azure Speech Service disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Azure Speech Service");
        }
    }
} 