using MedicalDictationService.Models;

namespace MedicalDictationService.Services;

/// <summary>
/// Interface for medical terminology processing and enhancement
/// </summary>
public interface IMedicalTerminologyService
{
    /// <summary>
    /// Processes text to identify and enhance medical terms
    /// </summary>
    Task<List<MedicalTerm>> ProcessMedicalTermsAsync(string text);

    /// <summary>
    /// Detects the SOAP section based on text content
    /// </summary>
    Task<SOAPSection> DetectSOAPSectionAsync(string text);

    /// <summary>
    /// Enhances transcription with medical terminology corrections
    /// </summary>
    Task<string> EnhanceTranscriptionAsync(string originalText, List<MedicalTerm> medicalTerms);

    /// <summary>
    /// Gets medical term suggestions for a given partial term
    /// </summary>
    Task<List<string>> GetMedicalTermSuggestionsAsync(string partialTerm);

    /// <summary>
    /// Validates if a term is a recognized medical term
    /// </summary>
    Task<bool> IsValidMedicalTermAsync(string term);

    /// <summary>
    /// Gets the medical category for a given term
    /// </summary>
    Task<string> GetMedicalCategoryAsync(string term);
} 