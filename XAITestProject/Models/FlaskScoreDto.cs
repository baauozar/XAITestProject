using System.Text.Json.Serialization;
namespace XAITestProject.Api.Models;
public sealed class FlaskScoreDto
{
    public double score { get; set; }
    [JsonPropertyName("base")] public double baseValue { get; set; }
    public int adjustment { get; set; }
    public List<string>? explanation { get; set; }
}
