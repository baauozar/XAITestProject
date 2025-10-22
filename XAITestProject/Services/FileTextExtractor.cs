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

        // Try Flask first
        var text = await _nlp.ExtractTextAsync(file.OpenReadStream(), file.FileName, file.ContentType, ct);
        if (!string.IsNullOrWhiteSpace(text)) return text;

        // Fallback local
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.ContentType.StartsWith("text/") || ext == ".txt")
        {
            using var sr = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
            return await sr.ReadToEndAsync();
        }
        if (ext == ".docx") return ExtractDocx(file.OpenReadStream());
        if (ext == ".pdf") return ExtractPdf(file.OpenReadStream());
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
                foreach (var para in body.Descendants<Paragraph>()) sb.AppendLine(para.InnerText);
                foreach (var table in body.Descendants<Table>())
                    foreach (var row in table.Descendants<TableRow>())
                        sb.AppendLine(string.Join(" ", row.Descendants<TableCell>().Select(c => c.InnerText.Replace("\n", " ").Trim())));
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

        // Fix: Use UglyToad.PdfPig for PDF text extraction, since PdfDocument does not have an Open method
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(mem))
        {
            foreach (var page in pdf.GetPages()) sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }
}
