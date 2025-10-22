using XAITestProject.Api.Clients;
using XAITestProject.Api.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace XAITestProject.Api.Services;

public sealed class FileTextExtractor : IFileTextExtractor
{
    private readonly NlpClient _nlp;
    public FileTextExtractor(NlpClient nlp) { _nlp = nlp; }

    public async Task<string> ExtractAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file.Length == 0) return string.Empty;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // ? FIXED: Try Flask first with a single stream
        using (var stream = file.OpenReadStream())
        {
            var text = await _nlp.ExtractTextAsync(stream, file.FileName, file.ContentType, ct);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        // ? FIXED: Fallback - open fresh stream for local extraction
        // Plain text
        if (file.ContentType.StartsWith("text/") || ext == ".txt")
        {
            using var stream = file.OpenReadStream();
            using var sr = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return await sr.ReadToEndAsync(ct);
        }

        // DOCX
        if (ext == ".docx")
        {
            using var stream = file.OpenReadStream();
            return ExtractDocx(stream);
        }

        // PDF
        if (ext == ".pdf")
        {
            using var stream = file.OpenReadStream();
            return ExtractPdf(stream);
        }

        return string.Empty;
    }

    private static string ExtractDocx(Stream stream)
    {
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        mem.Position = 0;
        var sb = new StringBuilder();

        using (var doc = WordprocessingDocument.Open(mem, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is not null)
            {
                foreach (var para in body.Descendants<Paragraph>())
                    sb.AppendLine(para.InnerText);

                foreach (var table in body.Descendants<Table>())
                    foreach (var row in table.Descendants<TableRow>())
                        sb.AppendLine(string.Join(" ", row.Descendants<TableCell>()
                            .Select(c => c.InnerText.Replace("\n", " ").Trim())));
            }
        }
        return sb.ToString();
    }

    private static string ExtractPdf(Stream stream)
    {
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        mem.Position = 0;
        var sb = new StringBuilder();

        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(mem))
        {
            foreach (var page in pdf.GetPages())
                sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }
}