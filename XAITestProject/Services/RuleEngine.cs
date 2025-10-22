using System.Linq;
using System.Text.RegularExpressions;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;

namespace CvScoring.Api.Services;
public sealed class RuleEngine : IRuleEngine
{
    private const int EXP5 = 5, EXP8 = 7, EXP12 = 10;
    private const int PER_REQ = 3, PER_PREF = 1, REQ_PENALTY = -3;
    private const int SKILL_BONUS_CAP = 12, MISS_CAP = -18;
    private const int EDU_BSC = 2, EDU_MSC = 4, EDU_PHD = 6;
    private const int LANG_EN = 2, LANG_TR = 2;
    private const int SENIOR_UNDER = -4;
    private const int THIN = -5, RECENT = 2;

    public (int adj, List<string> reasons) Adjust(string cv, string job, Lang langCv, Lang langJob, JobRequirements reqs)
    {
        int total = 0;
        var reasons = new List<string>();

        int yrs = ExtractYears(cv);
        if (yrs >= 12) { total += EXP12; reasons.Add($"Tecrübe: 12+ yıl +{EXP12} / Experience: 12+ years +{EXP12}"); }
        else if (yrs >= 8) { total += EXP8; reasons.Add($"Tecrübe: 8+ yıl +{EXP8} / Experience: 8+ years +{EXP8}"); }
        else if (yrs >= 5) { total += EXP5; reasons.Add($"Tecrübe: 5+ yıl +{EXP5} / Experience: 5+ years +{EXP5}"); }

        if (reqs.MinYears > 0 && yrs < reqs.MinYears) { total += SENIOR_UNDER; reasons.Add($"Min yıl şartı sağlanmadı {SENIOR_UNDER} / Min years not met {SENIOR_UNDER}"); }

        var cvReq = ContainsAny(cv, reqs.RequiredSkills);
        var cvPref = ContainsAny(cv, reqs.PreferredSkills);

        var matchedReq = reqs.RequiredSkills.Intersect(cvReq, StringComparer.OrdinalIgnoreCase).ToList();
        var missingReq = reqs.RequiredSkills.Except(cvReq, StringComparer.OrdinalIgnoreCase).ToList();
        var matchedPref = reqs.PreferredSkills.Intersect(cvPref, StringComparer.OrdinalIgnoreCase).ToList();

        int skillBonus = matchedReq.Count * PER_REQ + matchedPref.Count * PER_PREF;
        if (skillBonus > 0)
        {
            skillBonus = Math.Min(skillBonus, SKILL_BONUS_CAP);
            total += skillBonus;
            var parts = new List<string>();
            if (matchedReq.Count > 0) parts.Add(string.Join(", ", matchedReq));
            if (matchedPref.Count > 0) parts.Add(string.Join(", ", matchedPref));
            reasons.Add($"Beceri eşleşmesi: {string.Join(", ", parts)} +{skillBonus} / Skill match +{skillBonus}");
        }

        if (missingReq.Count > 0)
        {
            int penalty = Math.Max(MISS_CAP, REQ_PENALTY * missingReq.Count);
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

        if (RecentActivity(cv)) { total += RECENT; reasons.Add($"Son 2 yıl aktivite +{RECENT} / Recent activity +{RECENT}"); }

        if (reqs.Languages.Contains("english") && enOk) { total += LANG_EN; reasons.Add($"İngilizce yeterlilik +{LANG_EN} / English +{LANG_EN}"); }
        if (reqs.Languages.Contains("turkish") && trOk) { total += LANG_TR; reasons.Add($"Türkçe yeterlilik +{LANG_TR} / Turkish +{LANG_TR}"); }

        var allCvSkills = new HashSet<string>(cvReq.Concat(cvPref), StringComparer.OrdinalIgnoreCase);
        if (cv.Length < 250 || allCvSkills.Count < 2) { total += THIN; reasons.Add($"Zayıf içerik {THIN} / Thin CV {THIN} (len={cv.Length}, skills={allCvSkills.Count})"); }

        total = Math.Clamp(total, -25, 25);
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

    private static int EducationPoints(string cv, out string? msg)
    {
        var t = cv.ToLowerInvariant();
        if (t.Contains("phd") || t.Contains("doktora")) { msg = $"Eğitim: Doktora +{EDU_PHD} / Education: PhD +{EDU_PHD}"; return EDU_PHD; }
        if (t.Contains("yüksek lisans") || t.Contains("yuksek lisans") || t.Contains("master") || t.Contains("msc")) { msg = $"Eğitim: Yüksek lisans +{EDU_MSC} / Education: Master +{EDU_MSC}"; return EDU_MSC; }
        if (t.Contains("lisans") || t.Contains("bsc") || t.Contains("bachelor")) { msg = $"Eğitim: Lisans +{EDU_BSC} / Education: Bachelor +{EDU_BSC}"; return EDU_BSC; }
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
