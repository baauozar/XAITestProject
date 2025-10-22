using XAITestProject.Api.Services.Interfaces;
namespace XAITestProject.Api.Services;
public sealed class Xai : IXai
{
    public List<string> TopOverlapTerms(Dictionary<string,double> a, Dictionary<string,double> b, int topK = 10)
    {
        var list = new List<(string term, double w)>();
        foreach (var kv in a) if (b.TryGetValue(kv.Key, out var wb)) list.Add((kv.Key, kv.Value * wb));
        return list.OrderByDescending(x => x.w).Take(topK).Select(x => x.term).ToList();
    }
}
