using System.Net.Http.Headers;
using System.Net.Http.Json;
using XAITestProject.Api.Models;
using Microsoft.Extensions.Options;

namespace XAITestProject.Api.Clients;

public sealed class NlpClient
{
    private readonly HttpClient _http;

    public NlpClient(HttpClient http, IOptions<FlaskOptions> opt)
    {
        _http = http;
        var baseUrl = opt.Value.BaseUrl ?? "http://127.0.0.1:8001/";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<FlaskScoreDto?> TryScoreAsync(string cv, string job, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("score", new { cv_text = cv, job_text = job }, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<FlaskScoreDto>(cancellationToken: ct);
        }
        catch { return null; }
    }

    public async Task<string?> ExtractTextAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var sc = new StreamContent(fileStream);
            sc.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(sc, "file", fileName);
            var resp = await _http.PostAsync("extract", form, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<ExtractResponse>(cancellationToken: ct);
            return json?.text;
        }
        catch { return null; }
    }

    private sealed class ExtractResponse { public string? text { get; set; } public string? format { get; set; } }
}
