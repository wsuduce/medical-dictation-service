using MedicalDictationService.Models;

namespace MedicalDictationService.Services;

/// <summary>
/// Medical terminology processing service for enhancing transcriptions
/// </summary>
public interface IMedicalTerminologyService
{
    /// <summary>
    /// Enhance transcription with medical terminology recognition
    /// </summary>
    Task<TranscriptionResult> EnhanceTranscriptionAsync(TranscriptionResult transcription);

    /// <summary>
    /// Detect SOAP section from transcribed text
    /// </summary>
    Task<SOAPSection> DetectSOAPSectionAsync(string text);

    /// <summary>
    /// Identify medical terms in text
    /// </summary>
    Task<List<MedicalTerm>> IdentifyMedicalTermsAsync(string text);

    /// <summary>
    /// Get suggestions for potentially misspelled medical terms
    /// </summary>
    Task<List<string>> GetMedicalTermSuggestionsAsync(string term);

    /// <summary>
    /// Validate and correct common medical terminology errors
    /// </summary>
    Task<string> CorrectMedicalTerminologyAsync(string text);

    /// <summary>
    /// Get medical vocabulary for a specific category
    /// </summary>
    Task<List<string>> GetMedicalVocabularyAsync(string category);
} 