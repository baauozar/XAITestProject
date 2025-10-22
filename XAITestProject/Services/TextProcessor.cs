using System.Text.RegularExpressions;
using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;

namespace XAITestProject.Api.Services;
public sealed class TextProcessor : ITextProcessor
{
    private static readonly HashSet<string> StopTr = new(StringComparer.OrdinalIgnoreCase)
    {
        "ve","ile","da","de","mi","mı","mu","mü","bir","bu","şu","o","çok","az","en",
        "için","gibi","olan","olarak","ya","veya","ama","fakat","ise","ki",
        "ben","sen","o","biz","siz","onlar","hem","her","yıl","ay","gün"
    };
    private static readonly HashSet<string> StopEn = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","with","of","to","in","on","for","is","are","was","were",
        "i","you","he","she","it","we","they","this","that","these","those","as","by","from","at"
    };

    public List<string> Tokenize(string text, Lang lang)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();
        var lower = text.ToLowerInvariant();
        var tokens = Regex.Matches(lower, @"[a-z0-9öçşığü]+").Select(m => m.Value).ToList();
        var stop = lang == Lang.Turkish ? StopTr : StopEn;
        return tokens.Where(t => t.Length > 1 && !stop.Contains(t)).ToList();
    }
}
