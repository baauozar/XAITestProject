namespace XAITestProject.Api.Services.Interfaces;
using XAITestProject.Api.Models;
using XAITestProject.Models;

public interface IExplanationFormatter
{
    string BuildTr(ExplanationData b, double confidence, string confidenceReason);
    string BuildEn(ExplanationData b, double confidence, string confidenceReason);
}
