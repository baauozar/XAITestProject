using XAITestProject.Api.Models;
using XAITestProject.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace XAITestProject.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ScoringController : ControllerBase
{
    private readonly IScoringService _svc;
    private readonly ILanguageDetector _lang;
    private readonly IFileTextExtractor _files;

    public ScoringController(IScoringService svc, ILanguageDetector lang, IFileTextExtractor files)
    {
        _svc = svc; _lang = lang; _files = files;
    }

    // GET api/Scoring/health
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    // ---------- DETECT-LANGUAGE ----------
    // POST api/Scoring/detect-language   (body: {cv_text, job_text})
    [HttpPost("detect-language")]
    public IActionResult DetectLanguage([FromBody] ScoreRequest req)
    {
        var lc = _lang.Detect(req.cv_text ?? string.Empty);
        var lj = _lang.Detect(req.job_text ?? string.Empty);
        return Ok(new { cv = lc.ToString(), job = lj.ToString(), uiCulture = "tr-TR" });
    }

    // GET api/Scoring/detect-language?cv_text=...&job_text=...
    [HttpGet("detect-language")]
    public IActionResult DetectLanguageGet([FromQuery] string? cv_text, [FromQuery] string? job_text)
    {
        var lc = _lang.Detect(cv_text ?? string.Empty);
        var lj = _lang.Detect(job_text ?? string.Empty);
        return Ok(new { cv = lc.ToString(), job = lj.ToString(), uiCulture = "tr-TR" });
    }

    // ---------- SCORE ----------
    // POST api/Scoring/score   (body: {cv_text, job_text})
    [HttpPost("score")]
    public async Task<ActionResult<ScoreResponse>> Score([FromBody] ScoreRequest req, CancellationToken ct)
        => Ok(await _svc.ScoreAsync(req, ct));

    // GET api/Scoring/score?cv_text=...&job_text=...
    [HttpGet("score")]
    public async Task<ActionResult<ScoreResponse>> ScoreGet([FromQuery] string? cv_text, [FromQuery] string? job_text, CancellationToken ct)
    {
        var req = new ScoreRequest(cv_text ?? string.Empty, job_text ?? string.Empty);
        return Ok(await _svc.ScoreAsync(req, ct));
    }

    // ---------- SCORE-FILE (multipart) ----------
    // POST api/Scoring/score-file
    [HttpPost("score-file")]
    [RequestSizeLimit(25_000_000)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ScoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ScoreResponse>> ScoreFile([FromForm] ScoreFileForm form, CancellationToken ct)
    {
        if (form.cv_file is null || form.cv_file.Length == 0)
            return BadRequest("cv_file is required");

        if (!IsSupported(form.cv_file.FileName))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, "cv_file must be .txt, .pdf, or .docx");

        // CV metni
        var cvText = await _files.ExtractAsync(form.cv_file, ct);

        // İlan metni: dosya öncelikli
        string jobText = form.job_text ?? string.Empty;

        if (form.job_file is not null && form.job_file.Length > 0)
        {
            if (!IsSupported(form.job_file.FileName))
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, "job_file must be .txt, .pdf, or .docx");

            jobText = await _files.ExtractAsync(form.job_file, ct);
        }

        if (string.IsNullOrWhiteSpace(jobText))
            return BadRequest("Provide job_file (.txt/.pdf/.docx) or job_text.");

        var req = new ScoreRequest(cvText, jobText);
        var resp = await _svc.ScoreAsync(req, ct);
        return Ok(resp);
    }

    private static bool IsSupported(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".pdf" or ".docx";
    }

}

public sealed class ScoreFileForm
{
    public IFormFile? cv_file { get; set; }
    public IFormFile? job_file { get; set; }
    public string? job_text { get; set; }
}
