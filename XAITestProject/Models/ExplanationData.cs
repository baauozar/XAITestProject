namespace XAITestProject.Models
{
    public sealed record ExplanationData(
     double Score,
     double BaseScore,
     int Adjustment,
     string LangCv,
     string LangJob,
     int Years,
     int? MinYears,
     IEnumerable<string> RequiredMatched,
     IEnumerable<string> RequiredMissing,
     IEnumerable<string> Certifications,
     IEnumerable<string> RequestedLanguages
 );
}
