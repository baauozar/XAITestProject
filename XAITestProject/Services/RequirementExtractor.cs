using System.Linq;
using System.Text.RegularExpressions;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;

namespace XAITestProject.Api.Services;

public sealed class RequirementExtractor : IRequirementExtractor
{
    private static readonly string[] SkillsLexicon =
    {
        "c#", ".net", "asp.net", "ef core", "sql", "postgres", "mysql",
        "azure", "aws", "gcp", "docker", "kubernetes", "microservice",
        "python", "flask", "django", "pandas", "scikit", "pytorch",
        "react", "angular", "terraform", "helm", "prometheus", "grafana", "ci/cd",
        "airflow", "spark", "sagemaker", "feature store"
    };

    private static readonly string[] CertsLexicon =
    { "aws", "azure", "gcp", "cka", "ckad", "pmp", "scrum master", "ielts", "toefl" };

    public JobRequirements Extract(string jobText)
    {
        var t = (jobText ?? string.Empty).ToLowerInvariant();

        // MinYears init-only olduğu için başlatıcıda set ediyoruz
        var req = new JobRequirements { MinYears = ExtractMinYears(t) };

        // Bölge yakalama
        var requiredBlocks = CaptureZones(t, new[] { "zorunlu", "gerekli", "must have", "required", "mandatory" });
        var preferredBlocks = CaptureZones(t, new[] { "tercihen", "nice to have", "preferred", "plus", "artı" });

        // Beceri terimleri
        var requiredTerms = new HashSet<string>(requiredBlocks.SelectMany(SplitSkills), StringComparer.OrdinalIgnoreCase);
        var preferredTerms = new HashSet<string>(preferredBlocks.SelectMany(SplitSkills), StringComparer.OrdinalIgnoreCase);

        // Dil gereksinimi
        if (Regex.IsMatch(t, @"\b(english|required english|ingilizce)\b", RegexOptions.IgnoreCase))
            req.Languages.Add("english");
        if (Regex.IsMatch(t, @"\b(turkish|türkçe|turkce)\b", RegexOptions.IgnoreCase))
            req.Languages.Add("turkish");

        // Sertifikalar
        foreach (var c in CertsLexicon)
            if (t.Contains(c))
                req.Certifications.Add(c);

        // Sözlükten zorunlu/tercih sınıflaması
        foreach (var s in SkillsLexicon)
        {
            bool inRequired = requiredTerms.Contains(s) || requiredBlocks.Any(b => b.Contains(s, StringComparison.OrdinalIgnoreCase));
            bool inPreferred = !inRequired && (preferredTerms.Contains(s) || t.Contains(s));

            if (inRequired)
                req.RequiredSkills.Add(s);
            else if (inPreferred)
                req.PreferredSkills.Add(s);
        }

        return req;
    }

    // İşaretçileri tek pattern içinde birleştir
    private static List<string> CaptureZones(string text, IEnumerable<string> markers)
    {
        var alt = string.Join("|", markers.Where(m => !string.IsNullOrWhiteSpace(m))
                                          .Select(Regex.Escape));
        if (string.IsNullOrEmpty(alt)) return new List<string>();

        // ^ marker ... içeriği, boş satır ya da metin sonuna kadar al

        var pattern = $@"(?i)({alt})\s*[:\-]?\s*(.+?)(?=(?:{alt})|$)";
        var rx = new Regex(pattern, RegexOptions.Compiled);

        var zones = new List<string>();
        foreach (Match m in rx.Matches(text))
        {
            var block = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(block)) zones.Add(block);
        }
        return zones;
    }

    // Virgül, noktalı virgül, satır sonuna göre parçala; uçtaki noktalama kaldır
    private static IEnumerable<string> SplitSkills(string block)
    {
        foreach (var raw in Regex.Split(block, @"[,\;\r\n]+"))
        {
            var s = raw.Trim().ToLowerInvariant();
            if (s.Length < 2) continue;
            s = Regex.Replace(s, @"^[^\w\+\#\.]+|[^\w\+\#\.]+$", "");
            if (!string.IsNullOrWhiteSpace(s)) yield return s;
        }
    }

    private static int ExtractMinYears(string text)
    {
        var m = Regex.Match(text, @"(?i)\b(?:min(?:imum)?|en\s*az)?\s*(\d{1,2})\s*\+?\s*(?:yıl|yil|year|years|yr|yrs)\b");
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : 0;
    }
}
