using CvScoring.Api.Services;
using XAITestProject.Api.Clients;
using XAITestProject.Api.Services;
using XAITestProject.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<FlaskOptions>(builder.Configuration.GetSection("Flask"));
builder.Services.AddHttpClient<NlpClient>();

builder.Services.AddSingleton<ILanguageDetector, LanguageDetector>();
builder.Services.AddSingleton<ITextProcessor, TextProcessor>();
builder.Services.AddSingleton<ITfIdf, TfIdf>();
builder.Services.AddSingleton<ISimilarity, Similarity>();
builder.Services.AddSingleton<IRuleEngine, RuleEngine>();
builder.Services.AddSingleton<IXai, Xai>();
builder.Services.AddSingleton<IRequirementExtractor, RequirementExtractor>();
builder.Services.AddSingleton<IFileTextExtractor, FileTextExtractor>();
builder.Services.AddSingleton<IExplanationFormatter, ExplanationFormatter>();
builder.Services.AddSingleton<IScoringService, ScoringService>();
builder.Services.AddSingleton<IXai, Xai>();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();
app.Run();
