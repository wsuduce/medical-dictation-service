using MedicalDictationService.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace MedicalDictationService.Services;

/// <summary>
/// Azure Speech Services integration interface for medical dictation
/// </summary>
public interface IAzureSpeechService
{
    /// <summary>
    /// Start streaming speech recognition for a session
    /// </summary>
    Task<bool> StartStreamingRecognitionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop streaming recognition for a session
    /// </summary>
    Task<bool> StopStreamingRecognitionAsync(string sessionId);

    /// <summary>
    /// Process audio chunk for real-time transcription
    /// </summary>
    Task<TranscriptionResult?> ProcessAudioChunkAsync(string sessionId, byte[] audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the speech service is healthy and responsive
    /// </summary>
    Task<bool> IsServiceHealthyAsync();

    /// <summary>
    /// Get current service metrics and performance data
    /// </summary>
    Task<Dictionary<string, object>> GetServiceMetricsAsync();

    /// <summary>
    /// Configure speech recognition for medical terminology
    /// </summary>
    Task ConfigureMedicalRecognitionAsync(SpeechConfig speechConfig);

    /// <summary>
    /// Event triggered when interim transcription results are available
    /// </summary>
    event EventHandler<TranscriptionResult> InterimResultReceived;

    /// <summary>
    /// Event triggered when final transcription results are available
    /// </summary>
    event EventHandler<TranscriptionResult> FinalResultReceived;

    /// <summary>
    /// Event triggered when transcription session encounters an error
    /// </summary>
    event EventHandler<string> TranscriptionError;

    /// <summary>
    /// Event triggered when audio quality changes
    /// </summary>
    event EventHandler<AudioQuality> AudioQualityChanged;
}

/// <summary>
/// Speech service configuration options
/// </summary>
public class SpeechServiceOptions
{
    public string SubscriptionKey { get; set; } = string.Empty;
    public string Region { get; set; } = "eastus";
    public string Language { get; set; } = "en-US";
    public bool EnableMedicalTerminology { get; set; } = true;
    public bool EnableNoiseReduction { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.7;
    public TimeSpan RecognitionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public List<string> CustomVocabulary { get; set; } = new();
} 