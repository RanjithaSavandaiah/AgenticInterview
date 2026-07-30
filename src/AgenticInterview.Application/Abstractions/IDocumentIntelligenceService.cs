using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticInterview.Application.Abstractions;

public interface IDocumentIntelligenceService
{
    Task<string> ExtractTextFromPdfAsync(Stream pdfStream, CancellationToken cancellationToken = default);
    Task<string> ExtractTextFromDocxAsync(Stream docxStream, CancellationToken cancellationToken = default);
}
