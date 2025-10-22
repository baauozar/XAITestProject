namespace XAITestProject.Api.Services.Interfaces;
using XAITestProject.Api.Models;
using XAITestProject.Models;

public interface IExplanationFormatter
{
    string BuildTr(ExplanationData b);
    string BuildEn(ExplanationData b);
}
