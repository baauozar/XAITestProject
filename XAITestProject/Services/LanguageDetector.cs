using System.Text.RegularExpressions;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;

namespace CvScoring.Api.Services;
public sealed class LanguageDetector : ILanguageDetector
{
    private static readonly HashSet<string> TrHints = new(StringComparer.OrdinalIgnoreCase)
    { "ve","bir","ile","olarak","deneyim","yıl","proje","türkçe","ingilizce" };
    private static readonly HashSet<string> EnHints = new(StringComparer.OrdinalIgnoreCase)
    { "and","with","experience","years","project","english","turkish" };

    public Lang Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Lang.Unknown;
        var hasTr = Regex.IsMatch(text, "[çğıöşüÇĞİÖŞÜ]");
        var hasEn = Regex.IsMatch(text, "[A-Za-z]");

        int tr = hasTr ? 2 : 0;
        int en = hasEn ? 1 : 0;

        foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"[a-zA-Z0-9öçşığü]+"))
        {
            var t = m.Value;
            if (TrHints.Contains(t)) tr++;
            if (EnHints.Contains(t)) en++;
        }

        if (tr > en) return Lang.Turkish;
        if (en > tr) return Lang.English;
        return hasTr ? Lang.Turkish : Lang.English;
    }
}
