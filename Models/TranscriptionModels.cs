namespace MedicalDictationService.Models;

/// <summary>
/// SOAP note section types for medical documentation
/// </summary>
public enum SOAPSection
{
    Subjective,     // Patient's description of symptoms
    Objective,      // Observable/measurable findings  
    Assessment,     // Provider's clinical judgment
    Plan,          // Treatment and follow-up plan
    General        // General notes or uncategorized content
}

/// <summary>
/// Audio quality indicators for transcription feedback
/// </summary>
public enum AudioQuality
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical
}

/// <summary>
/// Real-time transcription result with medical enhancement
/// </summary>
public class TranscriptionResult
{
    public string SessionId { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string EnhancedText { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public TimeSpan Duration { get; set; }
    public SOAPSection DetectedSection { get; set; } = SOAPSection.General;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AudioQuality AudioQuality { get; set; } = AudioQuality.Good;
    public List<MedicalTerm> MedicalTerms { get; set; } = new();
    public bool IsInterim { get; set; } = false;
}

/// <summary>
/// Enhanced word with medical terminology recognition
/// </summary>
public class EnhancedWord
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool IsMedicalTerm { get; set; }
    public string? MedicalCategory { get; set; }
    public string? AlternativeSuggestion { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

/// <summary>
/// Medical terminology classification
/// </summary>
public class MedicalTerm
{
    public string Term { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Anatomy, Medication, Procedure, etc.
    public double Confidence { get; set; }
    public string? ICD10Code { get; set; }
    public string? Description { get; set; }
    public List<string> Synonyms { get; set; } = new();
}

/// <summary>
/// Transcription session management
/// </summary>
public class TranscriptionSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "Active"; // Active, Paused, Completed, Error
    public List<TranscriptionResult> Results { get; set; } = new();
    public AudioQuality OverallAudioQuality { get; set; } = AudioQuality.Good;
    public string? TemplateId { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Audio processing configuration
/// </summary>
public class AudioConfig
{
    public int SampleRate { get; set; } = 16000; // 16kHz for Azure Speech Services
    public int BitDepth { get; set; } = 16;
    public int Channels { get; set; } = 1; // Mono
    public string Format { get; set; } = "PCM";
    public bool NoiseReduction { get; set; } = true;
    public double VolumeThreshold { get; set; } = 0.1;
    public TimeSpan MaxSilenceDuration { get; set; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Medical vocabulary categories for enhanced recognition
/// </summary>
public static class MedicalCategories
{
    public const string Anatomy = "Anatomy";
    public const string Medication = "Medication";
    public const string Procedure = "Procedure";
    public const string Symptom = "Symptom";
    public const string Diagnosis = "Diagnosis";
    public const string Laboratory = "Laboratory";
    public const string Imaging = "Imaging";
    public const string Dosage = "Dosage";
    public const string Route = "Route";
    public const string Frequency = "Frequency";
}

/// <summary>
/// Real-time transcription event for SignalR
/// </summary>
public class TranscriptionEvent
{
    public string SessionId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // "interim", "final", "session_start", "session_end", "error"
    public TranscriptionResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
} 