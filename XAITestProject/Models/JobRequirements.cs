namespace XAITestProject.Api.Models;
public sealed class JobRequirements
{
    public HashSet<string> RequiredSkills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> PreferredSkills { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Certifications { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Languages { get; init; } = new(StringComparer.OrdinalIgnoreCase); // english, turkish
    public int MinYears { get; init; } = 0;
}
