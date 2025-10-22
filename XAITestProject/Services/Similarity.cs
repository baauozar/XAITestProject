using XAITestProject.Api.Services.Interfaces;
namespace XAITestProject.Api.Services;
public sealed class Similarity : ISimilarity
{
    public double Cosine(Dictionary<string,double> a, Dictionary<string,double> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0.0;
        double dot = 0;
        foreach (var kv in a) if (b.TryGetValue(kv.Key, out var wb)) dot += kv.Value * wb;
        double na = Math.Sqrt(a.Values.Sum(v => v*v));
        double nb = Math.Sqrt(b.Values.Sum(v => v*v));
        if (na == 0 || nb == 0) return 0.0;
        return dot / (na * nb);
    }
}
