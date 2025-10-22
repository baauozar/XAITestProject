using System.Globalization;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;
using XAITestProject.Models;
using System.Linq;

namespace XAITestProject.Api.Services;

public sealed class ExplanationFormatter : IExplanationFormatter
{
    public string BuildTr(ExplanationData b, double confidence, string confidenceReason)
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        string F(double v) => v.ToString("0.##", tr);
        var parts = new List<string>();

        // 1. Initial Summary and Score
        parts.Add($"**Değerlendirme Özeti:** Toplam uyum skorunuz **{F(b.Score)}/100** olarak hesaplanmıştır. Temel benzerlik (NLP) skoru {F(b.BaseScore)}, kural tabanlı ayarlama ise {b.Adjustment:+#;-#;0} puan katkı sağlamıştır. CV dili **{b.LangCv}** ve İlan dili **{b.LangJob}** olarak tespit edilmiştir.");

        // 2. Experience Analysis
        var expMsg = $"**Tecrübe Analizi:** CV'de **{b.Years} yıl** tecrübe beyan edilmiştir.";
        if (b.MinYears is int min && min > 0)
            expMsg += $" İlanın minimum tecrübe şartı **{min} yıl**{(b.Years >= min ? " olup, bu şart **sağlanmıştır** ✅." : " olup, bu şart **sağlanamamıştır** ❌.")}";
        parts.Add(expMsg);

        // 3. Skills Analysis
        var skillMsg = "**Beceri Profili:**";
        if (b.RequiredMatched.Any())
            skillMsg += $" Zorunlu becerilerin **{JoinTr(b.RequiredMatched)}** kısmı CV'de **başarıyla eşleşmiştir** ⭐.";
        if (b.RequiredMissing.Any())
            skillMsg += $" **Dikkat!** Zorunlu **{JoinTr(b.RequiredMissing)}** becerileri CV'nizde **eksiktir** ⚠️. Bu, skoru negatif etkileyen kritik bir faktördür.";
        if (b.Certifications.Any())
            skillMsg += $" CV'nizde **{JoinTr(b.Certifications)}** sertifikaları tespit edilmiştir 🏆.";
        if (b.RequestedLanguages.Any())
            skillMsg += $" Dil yeterlilikleri: {JoinTr(b.RequestedLanguages)}.";
        parts.Add(skillMsg);

        // 4. Confidence & Reliability Section (The XAI "Perfect" Feature)
        parts.Add($"**Güvenilirlik Seviyesi:** Bu skorun **güvenilirlik derecesi %{confidence:0.##}**'dir. Sistem, değerlendirmenin güvenilirliğini belirlerken şu faktörleri dikkate almıştır: \n- *Detaylar*: {confidenceReason.Replace("Base:", "Temel:").Replace("Factors:", "Faktörler:")}.");

        // 5. Final Assessment
        parts.Add($"**Sonuç:** {LabelTr(b.Score, b.RequiredMissing)}");
        return string.Join("\n\n", parts);
    }

    public string BuildEn(ExplanationData b, double confidence, string confidenceReason)
    {
        var en = CultureInfo.GetCultureInfo("en-US");
        string F(double v) => v.ToString("0.##", en);
        var parts = new List<string>();

        // 1. Initial Summary and Score
        parts.Add($"**Assessment Summary:** Your total compatibility score is calculated as **{F(b.Score)}/100**. The base similarity (NLP) score is {F(b.BaseScore)}, and the rule-based adjustment contributed {b.Adjustment:+#;-#;0} points. The CV language is detected as **{b.LangCv}** and the Job description language as **{b.LangJob}**.");

        // 2. Experience Analysis
        var expMsg = $"**Experience Analysis:** The CV declares **{b.Years} years** of experience.";
        if (b.MinYears is int min && min > 0)
            expMsg += $" The job's minimum experience requirement is **{min} years**{(b.Years >= min ? ", and this condition is **met** ✅." : ", and this condition is **not met** ❌.")}";
        parts.Add(expMsg);

        // 3. Skills Analysis
        var skillMsg = "**Skill Profile:**";
        if (b.RequiredMatched.Any())
            skillMsg += $" The required skills **{JoinEn(b.RequiredMatched)}** are **successfully matched** in the CV ⭐.";
        if (b.RequiredMissing.Any())
            skillMsg += $" **Attention!** The required skills **{JoinEn(b.RequiredMissing)}** are **missing** from your CV ⚠️. This is a critical factor negatively impacting the score.";
        if (b.Certifications.Any())
            skillMsg += $" Certifications: **{JoinEn(b.Certifications)}** are detected in the CV 🏆.";
        if (b.RequestedLanguages.Any())
            skillMsg += $" Language proficiencies: {JoinEn(b.RequestedLanguages)}.";
        parts.Add(skillMsg);

        // 4. Confidence & Reliability Section (The XAI "Perfect" Feature)
        parts.Add($"**Confidence & Reliability Level:** The **confidence score for this assessment is {confidence:0.##}%**. The system considered the following factors to determine the assessment reliability: \n- *Details*: {confidenceReason}.");

        // 5. Final Assessment
        parts.Add($"**Conclusion:** {LabelEn(b.Score, b.RequiredMissing)}");
        return string.Join("\n\n", parts);
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
        if (s >= 90) return "Mükemmel Uyum! Aday, gereksinimlerin neredeyse tamamını karşılamaktadır. ✨";
        if (s >= 75) return m ? "Çok Güçlü Aday. Temel beceriler mevcut ancak bazı küçük zorunlu eksikler not edilmiştir." : "Güçlü Aday. Rol için ideal bir eşleşmedir. 💪";
        if (s >= 55) return m ? "Orta Uyum. Adayın potansiyeli yüksek, ancak önemli zorunlu beceri boşlukları bulunmaktadır. Eksikleri kapatmak kritik." : "Orta-İyi Uyum. Aday temel gereksinimleri karşılıyor, ancak daha iyi bir eşleşme aranabilir.";
        if (s >= 40) return "Orta-Düşük Uyum. Başvuruda bazı kritik zorunlu beceriler eksik. Ek eğitim veya tecrübe gereklidir.";
        return "Düşük Uyum. Pozisyon gereksinimleri ile CV arasında belirgin bir uyumsuzluk var. 🔴";
    }
    static string LabelEn(double s, IEnumerable<string> miss)
    {
        bool m = miss.Any();
        if (s >= 90) return "Excellent Fit! The candidate meets almost all requirements. ✨";
        if (s >= 75) return m ? "Very Strong Candidate. Core skills are present but minor required gaps are noted." : "Strong Candidate. An ideal match for the role. 💪";
        if (s >= 55) return m ? "Moderate Fit. The candidate has high potential but significant required skill gaps exist. Closing these gaps is critical." : "Moderate-Good Fit. The candidate meets basic requirements, but a better match may be sought.";
        if (s >= 40) return "Mid-Low Fit. The application is missing some critical required skills. Further training or experience is needed.";
        return "Low Fit. There is a significant mismatch between the position requirements and the CV. 🔴";
    }
}