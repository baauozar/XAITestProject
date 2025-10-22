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

        var expTr = _fmt.BuildTr(expData);
        var expEn = _fmt.BuildEn(expData);

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
        double confidence = 85.0; // Base confidence
        var reasons = new List<string>();

        // Factor 1: Model quality (Flask SBERT is more reliable)
        if (usedFlask)
        {
            confidence += 10.0;
            reasons.Add("SBERT semantic model (+10)");
        }
        else
        {
            confidence -= 15.0;
            reasons.Add("TF-IDF fallback (-15)");
        }

        // Factor 2: CV length (more data = higher confidence)
        if (cvText.Length < 200)
        {
            confidence -= 20.0;
            reasons.Add("very short CV (-20)");
        }
        else if (cvText.Length < 500)
        {
            confidence -= 10.0;
            reasons.Add("short CV (-10)");
        }
        else if (cvText.Length > 2000)
        {
            confidence += 5.0;
            reasons.Add("detailed CV (+5)");
        }

        // Factor 3: Job description length
        if (jobText.Length < 200)
        {
            confidence -= 15.0;
            reasons.Add("vague job description (-15)");
        }
        else if (jobText.Length > 1000)
        {
            confidence += 5.0;
            reasons.Add("detailed job description (+5)");
        }

        // Factor 4: Language mismatch
        if (cvLang != jobLang && cvLang != Lang.Unknown && jobLang != Lang.Unknown)
        {
            confidence -= 10.0;
            reasons.Add("language mismatch (-10)");
        }

        // Factor 5: Required skills coverage
        if (totalReqCount > 0)
        {
            double coverage = (double)matchedReqCount / totalReqCount;
            if (coverage >= 0.8)
            {
                confidence += 5.0;
                reasons.Add("strong skill match (+5)");
            }
            else if (coverage < 0.3)
            {
                confidence -= 10.0;
                reasons.Add("weak skill match (-10)");
            }
        }
        else
        {
            // No extractable requirements
            confidence -= 5.0;
            reasons.Add("no clear requirements (-5)");
        }

        // Factor 6: Experience clarity
        if (cvYears == 0)
        {
            confidence -= 5.0;
            reasons.Add("unclear experience (-5)");
        }
        else if (minYears > 0 && Math.Abs(cvYears - minYears) > 5)
        {
            confidence -= 5.0;
            reasons.Add("experience mismatch (-5)");
        }

        // Clamp between 0-100
        confidence = Math.Clamp(confidence, 0, 100);

        // Build reason string
        string reasonText = $"Confidence: {confidence:0.##}% - Factors: {string.Join(", ", reasons)}";

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