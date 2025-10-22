using System.Text.RegularExpressions;
using XAITestProject.Api.Clients;
using XAITestProject.Api.Models;
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
    private readonly IExplanationFormatter _fmt;     // ← eklendi

    public ScoringService(
        NlpClient nlp,
        ITfIdf tfidf,
        ISimilarity sim,
        IRuleEngine rules,
        ILanguageDetector lang,
        IRequirementExtractor reqExt,
        ITextProcessor text,
        IExplanationFormatter fmt)                  // ← eklendi
    {
        _nlp = nlp; _tfidf = tfidf; _sim = sim; _rules = rules;
        _lang = lang; _reqExt = reqExt; _text = text; _fmt = fmt;  // ← eklendi
    }

    public async Task<ScoreResponse> ScoreAsync(ScoreRequest req, CancellationToken ct = default)
    {
        var cvText = req.cv_text ?? string.Empty;
        var jobText = req.job_text ?? string.Empty;

        // 1) Dil
        var lc = _lang.Detect(cvText);
        var lj = _lang.Detect(jobText);

        // 2) Temel skor (yerel TF-IDF + kosinüs)
        var tokensCv = _text.Tokenize(cvText, lc);
        var tokensJob = _text.Tokenize(jobText, lj);
        var (wCv, wJob) = _tfidf.Vectorize(tokensCv, tokensJob);
        var cos = _sim.Cosine(wCv, wJob);
        var baseScore = Math.Round(cos * 100.0, 2);

        var reasons = new List<string>();
        var overlaps = wCv.Keys.Intersect(wJob.Keys, StringComparer.OrdinalIgnoreCase)
                               .OrderByDescending(k => (wCv[k]) * (wJob[k]))
                               .Take(10)
                               .ToList();
        if (overlaps.Count > 0)
            reasons.Add("Ortak terimler / Overlaps: " + string.Join(", ", overlaps));

        // 3) İş ilanı gereksinimleri ve CV eşleşmesi
        var jr = _reqExt.Extract(jobText); // Required/Preferred/MinYears/Languages/Certs
        var cvLower = cvText.ToLowerInvariant();
        var matchedReq = jr.RequiredSkills.Where(s => cvLower.Contains(s, StringComparison.OrdinalIgnoreCase)).ToArray();
        var missingReq = jr.RequiredSkills.Except(matchedReq, StringComparer.OrdinalIgnoreCase).ToArray();

        // 4) Basit kural katkısı
        int years = ExtractYearsFromText(cvText);
        int adjustment = 0;

        if (years >= 12) { adjustment += 10; reasons.Add("Tecrübe: 12+ yıl +10 / Experience: 12+ years +10"); }
        else if (years >= 8) { adjustment += 7; reasons.Add("Tecrübe: 8+ yıl +7 / Experience: 8+ years +7"); }
        else if (years >= 5) { adjustment += 5; reasons.Add("Tecrübe: 5+ yıl +5 / Experience: 5+ years +5"); }

        if (matchedReq.Length > 0)
        {
            int bonus = Math.Min(10, 2 * matchedReq.Length);
            adjustment += bonus;
            reasons.Add($"Zorunlu eşleşmeler: {string.Join(", ", matchedReq)} +{bonus}");
        }
        if (missingReq.Length > 0)
        {
            int penalty = Math.Max(-15, -3 * missingReq.Length);
            adjustment += penalty;
            reasons.Add($"Eksik zorunlu beceriler: {string.Join(", ", missingReq)} {penalty}");
        }

        reasons.Add($"Temel skor: {baseScore}");
        reasons.Add($"Kural ayarlaması: {adjustment:+#;-#;0}");

        // 5) Nihai skor
        var finalScore = Math.Clamp(baseScore + adjustment, 0, 100);

        // 6) Doğal dil açıklama
        var expData = new ExplanationData(
            Score: finalScore,
            BaseScore: baseScore,
            Adjustment: adjustment,
            LangCv: lc.ToString(),
            LangJob: lj.ToString(),
            Years: years,
            MinYears: jr.MinYears,
            RequiredMatched: matchedReq,
            RequiredMissing: missingReq,
            Certifications: jr.Certifications,
            RequestedLanguages: jr.Languages
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
            explanationTextEn: expEn
        );
    }

    private static int ExtractYearsFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // 7 years, 7 yrs, 7 yr, 7 yıl, 7 yil, 7+ years ...
        var m = Regex.Match(text, @"(?i)\b(\d{1,2})\s*\+?\s*(?:years?|yrs?|yıl|yil)\b");
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
    }
}
