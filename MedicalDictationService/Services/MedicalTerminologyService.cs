using MedicalDictationService.Models;
using System.Text.RegularExpressions;

namespace MedicalDictationService.Services;

/// <summary>
/// Implementation of medical terminology processing service
/// </summary>
public class MedicalTerminologyService : IMedicalTerminologyService
{
    private readonly ILogger<MedicalTerminologyService> _logger;
    
    // Medical term dictionaries organized by category
    private readonly Dictionary<string, HashSet<string>> _medicalTerms;
    private readonly Dictionary<string, string> _commonCorrections;
    private readonly Dictionary<SOAPSection, List<string>> _soapKeywords;

    public MedicalTerminologyService(ILogger<MedicalTerminologyService> logger)
    {
        _logger = logger;
        _medicalTerms = InitializeMedicalTerms();
        _commonCorrections = InitializeCommonCorrections();
        _soapKeywords = InitializeSOAPKeywords();
    }

    public async Task<List<MedicalTerm>> ProcessMedicalTermsAsync(string text)
    {
        var medicalTerms = new List<MedicalTerm>();
        
        try
        {
            // Process each category of medical terms
            foreach (var category in _medicalTerms.Keys)
            {
                var categoryTerms = await FindTermsInCategory(text, category);
                medicalTerms.AddRange(categoryTerms);
            }

            // Apply common corrections
            foreach (var term in medicalTerms)
            {
                if (_commonCorrections.TryGetValue(term.Term.ToLower(), out var correction))
                {
                    term.Correction = correction;
                }
            }

            _logger.LogInformation($"Found {medicalTerms.Count} medical terms in text");
            return medicalTerms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing medical terms");
            return medicalTerms;
        }
    }

    public async Task<SOAPSection> DetectSOAPSectionAsync(string text)
    {
        try
        {
            var textLower = text.ToLower();
            var sectionScores = new Dictionary<SOAPSection, int>();

            // Initialize scores
            foreach (SOAPSection section in Enum.GetValues<SOAPSection>())
            {
                sectionScores[section] = 0;
            }

            // Score based on keywords
            foreach (var soapSection in _soapKeywords)
            {
                foreach (var keyword in soapSection.Value)
                {
                    var matches = Regex.Matches(textLower, $@"\b{keyword.ToLower()}\b");
                    sectionScores[soapSection.Key] += matches.Count * 2; // Weight keyword matches higher
                }
            }

            // Additional context-based scoring
            sectionScores[SOAPSection.Subjective] += CountSubjectiveIndicators(textLower);
            sectionScores[SOAPSection.Objective] += CountObjectiveIndicators(textLower);
            sectionScores[SOAPSection.Assessment] += CountAssessmentIndicators(textLower);
            sectionScores[SOAPSection.Plan] += CountPlanIndicators(textLower);

            // Return section with highest score, defaulting to General
            var maxScore = sectionScores.Values.Max();
            if (maxScore == 0) return SOAPSection.General;

            var detectedSection = sectionScores.First(x => x.Value == maxScore).Key;
            _logger.LogDebug($"Detected SOAP section: {detectedSection} (score: {maxScore})");
            
            return detectedSection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting SOAP section");
            return SOAPSection.General;
        }
    }

    public async Task<string> EnhanceTranscriptionAsync(string originalText, List<MedicalTerm> medicalTerms)
    {
        try
        {
            var enhancedText = originalText;

            // Apply corrections in reverse order to maintain position accuracy
            var termsWithCorrections = medicalTerms
                .Where(t => !string.IsNullOrEmpty(t.Correction))
                .OrderByDescending(t => t.StartPosition);

            foreach (var term in termsWithCorrections)
            {
                if (term.StartPosition >= 0 && term.EndPosition <= enhancedText.Length)
                {
                    enhancedText = enhancedText.Substring(0, term.StartPosition) + 
                                  term.Correction + 
                                  enhancedText.Substring(term.EndPosition);
                }
            }

            return enhancedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing transcription");
            return originalText;
        }
    }

    public async Task<List<string>> GetMedicalTermSuggestionsAsync(string partialTerm)
    {
        var suggestions = new List<string>();
        var partialLower = partialTerm.ToLower();

        foreach (var category in _medicalTerms.Values)
        {
            var matches = category
                .Where(term => term.ToLower().StartsWith(partialLower))
                .Take(10)
                .ToList();
            suggestions.AddRange(matches);
        }

        return suggestions.Take(20).ToList();
    }

    public async Task<bool> IsValidMedicalTermAsync(string term)
    {
        var termLower = term.ToLower();
        return _medicalTerms.Values.Any(category => 
            category.Any(t => t.ToLower() == termLower));
    }

    public async Task<string> GetMedicalCategoryAsync(string term)
    {
        var termLower = term.ToLower();
        
        foreach (var category in _medicalTerms)
        {
            if (category.Value.Any(t => t.ToLower() == termLower))
            {
                return category.Key;
            }
        }

        return "General";
    }

    #region Private Helper Methods

    private async Task<List<MedicalTerm>> FindTermsInCategory(string text, string category)
    {
        var terms = new List<MedicalTerm>();
        var categoryTerms = _medicalTerms[category];

        foreach (var medicalTerm in categoryTerms)
        {
            var matches = Regex.Matches(text, $@"\b{Regex.Escape(medicalTerm)}\b", RegexOptions.IgnoreCase);
            
            foreach (Match match in matches)
            {
                terms.Add(new MedicalTerm
                {
                    Term = match.Value,
                    Category = category,
                    Confidence = 0.9, // Base confidence, could be enhanced with ML
                    StartPosition = match.Index,
                    EndPosition = match.Index + match.Length
                });
            }
        }

        return terms;
    }

    private int CountSubjectiveIndicators(string text)
    {
        var indicators = new[] { "patient reports", "complains of", "states", "feels", "describes", "mentions" };
        return indicators.Sum(indicator => Regex.Matches(text, indicator).Count);
    }

    private int CountObjectiveIndicators(string text)
    {
        var indicators = new[] { "blood pressure", "temperature", "pulse", "examination reveals", "observed", "vital signs" };
        return indicators.Sum(indicator => Regex.Matches(text, indicator).Count);
    }

    private int CountAssessmentIndicators(string text)
    {
        var indicators = new[] { "diagnosis", "impression", "likely", "probable", "differential", "rule out" };
        return indicators.Sum(indicator => Regex.Matches(text, indicator).Count);
    }

    private int CountPlanIndicators(string text)
    {
        var indicators = new[] { "prescribe", "recommend", "follow up", "treatment", "therapy", "schedule", "order" };
        return indicators.Sum(indicator => Regex.Matches(text, indicator).Count);
    }

    #endregion

    #region Initialization Methods

    private Dictionary<string, HashSet<string>> InitializeMedicalTerms()
    {
        return new Dictionary<string, HashSet<string>>
        {
            ["Anatomy"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "heart", "lungs", "liver", "kidney", "brain", "stomach", "intestine", "spine", "chest", "abdomen"
            },
            ["Symptoms"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pain", "fever", "headache", "nausea", "vomiting", "diarrhea", "constipation", "fatigue", "dizziness", "shortness of breath"
            },
            ["Medications"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "aspirin", "ibuprofen", "acetaminophen", "lisinopril", "metformin", "atorvastatin", "amlodipine", "omeprazole", "levothyroxine", "albuterol"
            },
            ["Procedures"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "x-ray", "CT scan", "MRI", "ultrasound", "biopsy", "surgery", "endoscopy", "blood test", "EKG", "echocardiogram"
            },
            ["Conditions"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "diabetes", "hypertension", "asthma", "pneumonia", "bronchitis", "arthritis", "depression", "anxiety", "COPD", "heart disease"
            }
        };
    }

    private Dictionary<string, string> InitializeCommonCorrections()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hi tension"] = "hypertension",
            ["die beates"] = "diabetes", 
            ["as ma"] = "asthma",
            ["new monia"] = "pneumonia",
            ["arth ritis"] = "arthritis",
            ["head ache"] = "headache",
            ["stomach ache"] = "stomachache"
        };
    }

    private Dictionary<SOAPSection, List<string>> InitializeSOAPKeywords()
    {
        return new Dictionary<SOAPSection, List<string>>
        {
            [SOAPSection.Subjective] = new List<string>
            {
                "chief complaint", "history of present illness", "patient reports", "complains of", 
                "states", "feels", "describes", "subjective", "HPI", "CC"
            },
            [SOAPSection.Objective] = new List<string>
            {
                "vital signs", "physical examination", "blood pressure", "temperature", "pulse", 
                "objective", "exam", "examination reveals", "observed", "findings"
            },
            [SOAPSection.Assessment] = new List<string>
            {
                "assessment", "diagnosis", "impression", "likely", "probable", "differential", 
                "rule out", "clinical impression", "working diagnosis"
            },
            [SOAPSection.Plan] = new List<string>
            {
                "plan", "treatment", "prescribe", "recommend", "follow up", "therapy", 
                "schedule", "order", "continue", "discontinue", "monitoring"
            }
        };
    }

    #endregion
} 