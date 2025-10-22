using System.Text.RegularExpressions;
using XAITestProject.Api.Clients;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services;
using XAITestProject.Api.Services.Interfaces;
using XAITestProject.Models;

namespace CvScoring.Api.Services;

public sealed class ScoringService : IScoringService
{
    private readonly NlpClient _nlp;
    private readonly ITfIdf _tfidf;
    private readonly ISimilarity _sim;
    private readonly IRuleEngine _rules;
    private readonly ILanguageDetector _lang;
    private readonly IRequirementExtractor _reqExt;
    private readonly ITextProcessor _text;
    private readonly IExplanationFormatter _fmt;
    private readonly IXai _xai;

    public ScoringService(
        NlpClient nlp,
        ITfIdf tfidf,
        ISimilarity sim,
        IRuleEngine rules,
        ILanguageDetector lang,
        IRequirementExtractor reqExt,
        ITextProcessor text,
        IExplanationFormatter fmt,
        IXai xai)
    {
        _nlp = nlp;
        _tfidf = tfidf;
        _sim = sim;
        _rules = rules;
        _lang = lang;
        _reqExt = reqExt;
        _text = text;
        _fmt = fmt;
        _xai = xai;
    }

    public async Task<ScoreResponse> ScoreAsync(ScoreRequest req, CancellationToken ct = default)
    {
        var cvText = req.cv_text ?? string.Empty;
        var jobText = req.job_text ?? string.Empty;

        var reasons = new List<string>();
        bool usedFlask = false;

        // 1) Language detection
        var lc = _lang.Detect(cvText);
        var lj = _lang.Detect(jobText);

        // 2) Try Flask first, fallback to local
        double baseScore;
        int adjustment = 0;

        var flaskResult = await _nlp.TryScoreAsync(cvText, jobText, ct);

        if (flaskResult is not null)
        {
            // Use Flask scoring
            baseScore = flaskResult.baseValue;
            adjustment = flaskResult.adjustment;
            usedFlask = true;

            if (flaskResult.explanation is not null && flaskResult.explanation.Count > 0)
            {
                reasons.AddRange(flaskResult.explanation);
            }

            reasons.Add("✓ Using advanced SBERT model / SBERT modeli kullanıldı");
        }
        else
        {
            // Fallback to local TF-IDF scoring
            reasons.Add("⚠ Using local TF-IDF (Flask unavailable) / Yerel TF-IDF kullanıldı");

            var tokensCv = _text.Tokenize(cvText, lc);
            var tokensJob = _text.Tokenize(jobText, lj);
            var (wCv, wJob) = _tfidf.Vectorize(tokensCv, tokensJob);
            var cos = _sim.Cosine(wCv, wJob);
            baseScore = Math.Round(cos * 100.0, 2);

            // Show top overlapping terms using XAI service
            var overlaps = _xai.TopOverlapTerms(wCv, wJob, 10);
            if (overlaps.Count > 0)
            {
                reasons.Add("Ortak terimler / Overlaps: " + string.Join(", ", overlaps));
            }
        }

        // 3) Extract job requirements
        var jr = _reqExt.Extract(jobText);

        // 4) Use RuleEngine instead of manual calculations
        var (ruleAdjustment, ruleReasons) = _rules.Adjust(cvText, jobText, lc, lj, jr);

        adjustment += ruleAdjustment;
        reasons.AddRange(ruleReasons);

        // Add summary
        reasons.Add($"Temel skor / Base score: {baseScore:0.##}");
        reasons.Add($"Kural ayarlaması / Rule adjustment: {adjustment:+#;-#;0}");

        // 5) Calculate final score
        var finalScore = Math.Clamp(baseScore + adjustment, 0, 100);

        // 6) Extract CV-specific data for natural language explanation
        int years = ExtractYearsFromText(cvText);

        var cvLower = cvText.ToLowerInvariant();
        var matchedReq = jr.RequiredSkills
            .Where(s => cvLower.Contains(s, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var missingReq = jr.RequiredSkills
            .Except(matchedReq, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Extract CV certifications (not job certifications)
        var cvCerts = jr.Certifications
            .Where(c => cvLower.Contains(c, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Extract CV languages (not just requested languages)
        var cvLangs = new List<string>();
        if (cvLower.Contains("english") || cvLower.Contains("ielts") || cvLower.Contains("toefl"))
            cvLangs.Add("english");
        if (cvLower.Contains("türkçe") || cvLower.Contains("turkce") || cvLower.Contains("turkish"))
            cvLangs.Add("turkish");

        // 7) Calculate confidence score
        var (confidence, confidenceReason) = CalculateConfidence(
            cvText,
            jobText,
            usedFlask,
            lc,
            lj,
            matchedReq.Length,
            jr.RequiredSkills.Count,
            years,
            jr.MinYears
        );

        // 8) Build natural language explanation
        var expData = new ExplanationData(
            Score: finalScore,
            BaseScore: baseScore,
            Adjustment: adjustment,
            LangCv: lc.ToString(),
            LangJob: lj.ToString(),
            Years: years,
            MinYears: jr.MinYears > 0 ? jr.MinYears : null,
            RequiredMatched: matchedReq,
            RequiredMissing: missingReq,
            Certifications: cvCerts,
            RequestedLanguages: cvLangs
        );

        var expTr = _fmt.BuildTr(expData, confidence, confidenceReason);
        var expEn = _fmt.BuildEn(expData, confidence, confidenceReason);

        return new ScoreResponse(
            score: Math.Round(finalScore, 2),
            baseScore: Math.Round(baseScore, 2),
            adjustment: adjustment,
            langCv: lc.ToString(),
            langJob: lj.ToString(),
            uiCulture: "tr-TR",
            explanation: reasons,
            explanationText: expTr,
            explanationTextEn: expEn,
            confidence: Math.Round(confidence, 2),
            confidenceReason: confidenceReason
        );
    }

    private static (double confidence, string reason) CalculateConfidence(
            string cvText,
            string jobText,
            bool usedFlask,
            Lang cvLang,
            Lang jobLang,
            int matchedReqCount,
            int totalReqCount,
            int cvYears,
            int minYears)
    {
        double baseConfidence = 80.0; // Slightly lower base for more factor impact
        double confidence = baseConfidence;
        var factors = new List<string>();

        // Factor 1: Model quality (SBERT is more reliable)
        if (usedFlask)
        {
            confidence += 10.0;
            factors.Add("SBERT Semantic Model (+10.0)");
        }
        else
        {
            confidence -= 15.0;
            factors.Add("TF-IDF Fallback (-15.0)");
        }

        // Factor 2: CV length - More granularity
        if (cvText.Length < 150)
        {
            confidence -= 25.0;
            factors.Add("CV too brief (<150 chars) (-25.0)");
        }
        else if (cvText.Length < 400)
        {
            confidence -= 10.0;
            factors.Add("Short CV (150-400 chars) (-10.0)");
        }
        else if (cvText.Length > 2500)
        {
            confidence += 7.0;
            factors.Add("Very detailed CV (>2500 chars) (+7.0)");
        }
        else
        {
            confidence += 3.0;
            factors.Add("Adequate CV length (400-2500 chars) (+3.0)");
        }

        // Factor 3: Job description length - More granularity
        if (jobText.Length < 150)
        {
            confidence -= 20.0;
            factors.Add("Vague Job Description (<150 chars) (-20.0)");
        }
        else if (jobText.Length > 1500)
        {
            confidence += 5.0;
            factors.Add("Detailed Job Description (>1500 chars) (+5.0)");
        }
        else
        {
            confidence += 2.0;
            factors.Add("Adequate Job Description length (+2.0)");
        }

        // Factor 4: Language match - Increased penalty for mismatch
        if (cvLang != jobLang && cvLang != Lang.Unknown && jobLang != Lang.Unknown)
        {
            confidence -= 15.0;
            factors.Add("Language Mismatch (CV/Job) (-15.0)");
        }

        // Factor 5: Required skills coverage - Weighted impact
        if (totalReqCount > 0)
        {
            double coverage = (double)matchedReqCount / totalReqCount;
            // Impact ranges from -10 to +10 based on coverage (2*coverage-1) * 10
            double skillImpact = Math.Round(10 * (2 * coverage - 1));
            confidence += skillImpact;
            factors.Add($"Skill Coverage ({coverage:P0}, {matchedReqCount}/{totalReqCount}) ({(skillImpact > 0 ? "+" : "")}{skillImpact:0.#})");
        }
        else
        {
            confidence -= 8.0;
            factors.Add("No clear requirements extracted (-8.0)");
        }

        // Factor 6: Experience clarity & alignment
        if (cvYears == 0)
        {
            confidence -= 5.0;
            factors.Add("Unclear Experience in CV (-5.0)");
        }
        else if (minYears > 0)
        {
            int diff = cvYears - minYears;
            if (diff < -2) { confidence -= 5.0; factors.Add($"Significant Experience Gap (Min {minYears}, CV {cvYears}) (-5.0)"); }
            else if (diff >= 5) { confidence += 3.0; factors.Add("Experience significantly exceeds minimum (+3.0)"); }
        }

        // Final score and reason text
        confidence = Math.Clamp(confidence, 0, 100);
        // This reason string now contains all dynamic factor contributions for explanation
        string reasonText = $"Base: {baseConfidence:0.##}% | Factors: {string.Join(" | ", factors)} | Final: {confidence:0.##}%";

        return (confidence, reasonText);
    }

    private static int ExtractYearsFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;

        // Match patterns like: 7 years, 7 yrs, 7 yr, 7 yıl, 7 yil, 7+ years
        var m = Regex.Match(text, @"(?i)\b(\d{1,2})\s*\+?\s*(?:years?|yrs?|yıl|yil)\b");

        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
    }
}