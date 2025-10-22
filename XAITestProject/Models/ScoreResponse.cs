namespace XAITestProject.Api.Models;

public record ScoreResponse(
    double score,
    double baseScore,
    int adjustment,
    string langCv,
    string langJob,
    string uiCulture,
    List<string> explanation,
    string? explanationText = null,   // ← eklendi
    string? explanationTextEn = null  // ← opsiyonel İngilizce
);
