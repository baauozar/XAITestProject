using XAITestProject.Api.Services.Interfaces;

namespace XAITestProject.Api.Services;
public sealed class TfIdf : ITfIdf
{
    public (Dictionary<string,double> a, Dictionary<string,double> b) Vectorize(List<string> a, List<string> b)
    {
        var df = new Dictionary<string,int>(StringComparer.Ordinal);
        foreach (var t in new HashSet<string>(a)) df[t] = df.GetValueOrDefault(t) + 1;
        foreach (var t in new HashSet<string>(b)) df[t] = df.GetValueOrDefault(t) + 1;

        static Dictionary<string,double> Tf(List<string> toks)
        {
            var tf = new Dictionary<string,double>(StringComparer.Ordinal);
            foreach (var t in toks) tf[t] = tf.GetValueOrDefault(t) + 1;
            var max = tf.Values.DefaultIfEmpty(1).Max();
            foreach (var k in tf.Keys.ToList()) tf[k] = tf[k] / max;
            return tf;
        }

        double Idf(string term)
        {
            const int N = 2;
            var d = df.GetValueOrDefault(term);
            return Math.Log((N + 1.0) / (d + 1.0)) + 1.0;
        }

        var tfA = Tf(a);
        var tfB = Tf(b);

        var wA = tfA.ToDictionary(k => k.Key, v => v.Value * Idf(v.Key), StringComparer.Ordinal);
        var wB = tfB.ToDictionary(k => k.Key, v => v.Value * Idf(v.Key), StringComparer.Ordinal);
        return (wA, wB);
    }
}
