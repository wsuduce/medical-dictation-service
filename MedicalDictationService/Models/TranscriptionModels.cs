using System.ComponentModel.DataAnnotations;

namespace MedicalDictationService.Models;

/// <summary>
/// Represents different sections of a SOAP note
/// </summary>
public enum SOAPSection
{
    None = 0,
    General = 1,
    Subjective = 2,
    Objective = 3,
    Assessment = 4,
    Plan = 5
}

/// <summary>
/// Represents audio quality levels for transcription
/// </summary>
public enum AudioQuality
{
    Unknown = 0,
    Critical = 1,
    Poor = 2,
    Fair = 3,
    Good = 4,
    Excellent = 5
}

/// <summary>
/// Represents the result of a transcription operation
/// </summary>
public class TranscriptionResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public string TranscribedText { get; set; } = string.Empty;
    public string? OriginalText { get; set; }
    public double Confidence { get; set; }
    public SOAPSection DetectedSection { get; set; } = SOAPSection.General;
    public SOAPSection DetectedSOAPSection { get; set; } = SOAPSection.None;
    public List<MedicalTerm> MedicalTerms { get; set; } = new();
    public AudioQuality AudioQuality { get; set; } = AudioQuality.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsIntermediate { get; set; }
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a medical term identified in transcription
/// </summary>
public class MedicalTerm
{
    public string Term { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Correction { get; set; }
    public double Confidence { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
}

/// <summary>
/// Represents a transcription session
/// </summary>
public class TranscriptionSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public TranscriptionStatus Status { get; set; } = TranscriptionStatus.Starting;
    public bool IsActive { get; set; } = false;
    public List<TranscriptionResult> Results { get; set; } = new();
    public string FinalTranscription { get; set; } = string.Empty;
    public AudioQuality OverallAudioQuality { get; set; } = AudioQuality.Unknown;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents transcription session status
/// </summary>
public enum TranscriptionStatus
{
    Starting = 0,
    Active = 1,
    Paused = 2,
    Stopped = 3,
    Completed = 4,
    Error = 5
}

/// <summary>
/// Configuration for audio processing
/// </summary>
public class AudioConfig
{
    public string Language { get; set; } = "en-US";
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public string AudioFormat { get; set; } = "wav";
    public bool EnableMedicalTerminology { get; set; } = true;
    public bool EnableProfanityFilter { get; set; } = true;
    public TimeSpan MaxSilenceDuration { get; set; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Event data for real-time transcription updates
/// </summary>
public class TranscriptionEvent
{
    public string Type { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public TranscriptionResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public AudioQuality? AudioQuality { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
} 