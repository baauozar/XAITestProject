namespace XAITestProject.Models;

public sealed class RuleOptions
{
    // Deneyim Puanları
    public int Exp5 { get; set; } = 5;
    public int Exp8 { get; set; } = 7;
    public int Exp12 { get; set; } = 10;

    // Beceri Puanları
    public int PerReq { get; set; } = 3;
    public int PerPref { get; set; } = 1;
    public int ReqPenalty { get; set; } = -3;

    // Beceri Sınırları
    public int SkillBonusCap { get; set; } = 12;
    public int MissCap { get; set; } = -18;

    // Eğitim Puanları
    public int EduBsc { get; set; } = 2;
    public int EduMsc { get; set; } = 4;
    public int EduPhd { get; set; } = 6;

    // Dil Puanları
    public int LangEn { get; set; } = 2;
    public int LangTr { get; set; } = 2;

    // Diğer Kural Puanları
    public int SeniorUnder { get; set; } = -4;
    public int Thin { get; set; } = -5;
    public int Recent { get; set; } = 2;

    // Nihai Sınırlama (Clamping)
    public int MaxAdjustment { get; set; } = 25;
    public int MinAdjustment { get; set; } = -25;
}