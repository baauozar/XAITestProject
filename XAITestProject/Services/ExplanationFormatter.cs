using System.Globalization;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;
using XAITestProject.Models;

namespace XAITestProject.Api.Services;

public sealed class ExplanationFormatter : IExplanationFormatter
{
    public string BuildTr(ExplanationData b)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        string F(double v) => v.ToString("0.##", tr);

        var parts = new List<string>
        {
            $"Toplam skor {F(b.Score)}. Temel benzerlik {F(b.BaseScore)}. Kural etkisi {b.Adjustment:+#;-#;0}.",
            $"CV dili {b.LangCv}. İlan dili {b.LangJob}."
        };

        if (b.MinYears is int min)
            parts.Add($"Deneyim {b.Years} yıl. İlan en az {min} yıl ister{(b.Years >= min ? " ve şart sağlanır." : " ve şart sağlanmaz.")}");
        else
            parts.Add($"Deneyim {b.Years} yıl.");

        if (b.RequiredMatched.Any())
            parts.Add($"Zorunlu eşleşmeler: {JoinTr(b.RequiredMatched)}.");
        if (b.RequiredMissing.Any())
            parts.Add($"Eksik zorunlu beceriler: {JoinTr(b.RequiredMissing)}.");
        if (b.Certifications.Any())
            parts.Add($"Sertifikalar: {JoinTr(b.Certifications)}.");
        if (b.RequestedLanguages.Any())
            parts.Add($"İstenen diller: {JoinTr(b.RequestedLanguages)}.");

        parts.Add($"Değerlendirme: {LabelTr(b.Score, b.RequiredMissing)}.");
        return string.Join(" ", parts);
    }

    public string BuildEn(ExplanationData b)
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        string F(double v) => v.ToString("0.##", en);

        var parts = new List<string>
        {
            $"Overall score {F(b.Score)}. Base similarity {F(b.BaseScore)}. Rule impact {b.Adjustment:+#;-#;0}.",
            $"CV language {b.LangCv}. Job language {b.LangJob}."
        };

        if (b.MinYears is int min)
            parts.Add($"Experience {b.Years} years. Job requires at least {min} years{(b.Years >= min ? " and the condition is met." : " and the condition is not met.")}");
        else
            parts.Add($"Experience {b.Years} years.");

        if (b.RequiredMatched.Any())
            parts.Add($"Matched required skills: {JoinEn(b.RequiredMatched)}.");
        if (b.RequiredMissing.Any())
            parts.Add($"Missing required skills: {JoinEn(b.RequiredMissing)}.");
        if (b.Certifications.Any())
            parts.Add($"Certifications: {JoinEn(b.Certifications)}.");
        if (b.RequestedLanguages.Any())
            parts.Add($"Requested languages: {JoinEn(b.RequestedLanguages)}.");

        parts.Add($"Assessment: {LabelEn(b.Score, b.RequiredMissing)}.");
        return string.Join(" ", parts);
    }

    static string JoinTr(IEnumerable<string> xs)
    {
        var a = xs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return a.Length <= 1 ? string.Join("", a) : string.Join(", ", a[..^1]) + " ve " + a[^1];
    }
    static string JoinEn(IEnumerable<string> xs)
    {
        var a = xs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return a.Length <= 1 ? string.Join("", a) : string.Join(", ", a[..^1]) + " and " + a[^1];
    }

    static string LabelTr(double s, IEnumerable<string> miss)
    {
        bool m = miss.Any();
        if (s >= 80) return m ? "yüksek ama kritik eksikler var." : "yüksek ve şartları karşılıyor.";
        if (s >= 60) return m ? "orta-yüksek, bazı zorunlular eksik." : "orta-yüksek, rol için uygun.";
        if (s >= 40) return "orta, ek geliştirme gerekir.";
        return "düşük, rol gereksinimleriyle uyum sınırlı.";
    }
    static string LabelEn(double s, IEnumerable<string> miss)
    {
        bool m = miss.Any();
        if (s >= 80) return m ? "high but with critical gaps." : "high and meets the requirements.";
        if (s >= 60) return m ? "upper-mid with some required gaps." : "upper-mid and suitable.";
        if (s >= 40) return "mid, needs improvement.";
        return "low, limited fit to the role.";
    }
}
