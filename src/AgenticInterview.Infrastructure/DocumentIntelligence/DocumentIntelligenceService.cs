using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.Application.Abstractions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace AgenticInterview.Infrastructure.DocumentIntelligence;

public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    public Task<string> ExtractTextFromPdfAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return Task.FromResult(sb.ToString());
    }

    public Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken cancellationToken = default)
    {
        using var wordDocument = WordprocessingDocument.Open(docxStream, false);
        var sb = new StringBuilder();
        
        var body = wordDocument.MainDocumentPart?.Document?.Body;
        if (body != null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                sb.AppendLine(paragraph.InnerText);
            }
        }
        return Task.FromResult(sb.ToString());
    }
}
