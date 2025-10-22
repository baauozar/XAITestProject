using Microsoft.Extensions.Options;
using System.Linq;
using System.Text.RegularExpressions;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;
using XAITestProject.Models;

namespace CvScoring.Api.Services;
public sealed class RuleEngine : IRuleEngine
{
    private readonly RuleOptions _options;
    public RuleEngine(IOptions<RuleOptions> options)
    {
        _options = options.Value;
    }
    public (int adj, List<string> reasons) Adjust(string cv, string job, Lang langCv, Lang langJob, JobRequirements reqs)
    {
        int total = 0;
        var reasons = new List<string>();

        int yrs = ExtractYears(cv);
        if (yrs >= 12) { total += _options.Exp12; reasons.Add($"Tecrübe: 12+ yıl +{_options.Exp12} / Experience: 12+ years +{_options.Exp12}"); }
        else if (yrs >= 8) { total += _options.Exp8; reasons.Add($"Tecrübe: 8+ yıl +{_options.Exp8} / Experience: 8+ years +{_options.Exp8}"); }
        else if (yrs >= 5) { total += _options.Exp5; reasons.Add($"Tecrübe: 5+ yıl +{_options.Exp5} / Experience: 5+ years +{_options.Exp5}"); }

        if (reqs.MinYears > 0 && yrs < reqs.MinYears) { total += _options.SeniorUnder; reasons.Add($"Min yıl şartı sağlanmadı {_options.SeniorUnder} / Min years not met {_options.SeniorUnder}"); }

        var cvReq = ContainsAny(cv, reqs.RequiredSkills);
        var cvPref = ContainsAny(cv, reqs.PreferredSkills);

        var matchedReq = reqs.RequiredSkills.Intersect(cvReq, StringComparer.OrdinalIgnoreCase).ToList();
        var missingReq = reqs.RequiredSkills.Except(cvReq, StringComparer.OrdinalIgnoreCase).ToList();
        var matchedPref = reqs.PreferredSkills.Intersect(cvPref, StringComparer.OrdinalIgnoreCase).ToList();

        int skillBonus = matchedReq.Count * _options.PerReq + matchedPref.Count * _options.PerPref;
        if (skillBonus > 0)
        {
            skillBonus = Math.Min(skillBonus, _options.SkillBonusCap);
            total += skillBonus;
            var parts = new List<string>();
            if (matchedReq.Count > 0) parts.Add(string.Join(", ", matchedReq));
            if (matchedPref.Count > 0) parts.Add(string.Join(", ", matchedPref));
            reasons.Add($"Beceri eşleşmesi: {string.Join(", ", parts)} +{skillBonus} / Skill match +{skillBonus}");
        }

        if (missingReq.Count > 0)
        {
            int penalty = Math.Max(_options.MissCap, _options.ReqPenalty * missingReq.Count);
            total += penalty;
            reasons.Add($"Eksik zorunlu: {string.Join(", ", missingReq)} {penalty} / Missing required {penalty}");
        }

        int eduPts = EducationPoints(cv, out var eduMsg);
        if (eduPts != 0 && eduMsg is not null) { total += eduPts; reasons.Add(eduMsg!); }

        var enOk = MentionsEnglish(cv);
        var trOk = MentionsTurkish(cv);

        var cvCerts = ContainsAny(cv, reqs.Certifications);
        if (cvCerts.Count > 0)
        {
            int cbonus = Math.Min(5, cvCerts.Count);
            total += cbonus;
            reasons.Add($"Sertifikalar: {string.Join(", ", cvCerts)} +{cbonus} / Certifications +{cbonus}");
        }

        if (RecentActivity(cv)) { total += _options.Recent; reasons.Add($"Son 2 yıl aktivite +{_options.Recent} / Recent activity +{_options.Recent}"); }

        if (reqs.Languages.Contains("english") && enOk) { total += _options.LangEn; reasons.Add($"İngilizce yeterlilik +{_options.LangEn} / English +{_options.LangEn}"); }
        if (reqs.Languages.Contains("turkish") && trOk) { total += _options.LangTr; reasons.Add($"Türkçe yeterlilik +{_options.LangTr} / Turkish +{_options.LangTr}"); }

        var allCvSkills = new HashSet<string>(cvReq.Concat(cvPref), StringComparer.OrdinalIgnoreCase);
        if (cv.Length < 250 || allCvSkills.Count < 2) { total += _options.Thin; reasons.Add($"Zayıf içerik {_options.Thin} / Thin CV {_options.Thin} (len={cv.Length}, skills={allCvSkills.Count})"); }

        // Sınırlama, appsettings.json'dan okunan değerleri kullanır.
        total = Math.Clamp(total, _options.MinAdjustment, _options.MaxAdjustment);
        return (total, reasons);
    }
    private static List<string> ContainsAny(string text, IEnumerable<string> keys)
    {
        var t = text.ToLowerInvariant();
        return keys.Where(k => t.Contains(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int ExtractYears(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int years = 0;
        foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"(\d{1,2})\s*\+?\s*(yıl|yr|yrs|year|years)"))
            if (int.TryParse(m.Groups[1].Value, out var v)) years = Math.Max(years, v);
        return years;
    }

    private int EducationPoints(string cv, out string? msg)
    {
        var t = cv.ToLowerInvariant();
        if (t.Contains("phd") || t.Contains("doktora")) { msg = $"Eğitim: Doktora +{_options.EduPhd} / Education: PhD +{_options.EduPhd}"; return _options.EduPhd; }
        if (t.Contains("yüksek lisans") || t.Contains("yuksek lisans") || t.Contains("master") || t.Contains("msc")) { msg = $"Eğitim: Yüksek lisans +{_options.EduMsc} / Education: Master +{_options.EduMsc}"; return _options.EduMsc; }
        if (t.Contains("lisans") || t.Contains("bsc") || t.Contains("bachelor")) { msg = $"Eğitim: Lisans +{_options.EduBsc} / Education: Bachelor +{_options.EduBsc}"; return _options.EduBsc; }
        msg = null; return 0;
    }

    private static bool MentionsEnglish(string cv)
        => cv.Contains("english", StringComparison.OrdinalIgnoreCase) || cv.Contains("ielts", StringComparison.OrdinalIgnoreCase) || cv.Contains("toefl", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(cv, @"\b(c1|c2)\b", RegexOptions.IgnoreCase);

    private static bool MentionsTurkish(string cv)
        => cv.Contains("türkçe", StringComparison.OrdinalIgnoreCase) || cv.Contains("turkce", StringComparison.OrdinalIgnoreCase) || cv.Contains("ana dil", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(cv, @"\b(c1|c2)\b", RegexOptions.IgnoreCase);

    private static bool RecentActivity(string cv)
    {
        int y = DateTime.UtcNow.Year;
        return cv.Contains(y.ToString()) || cv.Contains((y - 1).ToString()) || cv.Contains((y - 2).ToString());
    }
}
