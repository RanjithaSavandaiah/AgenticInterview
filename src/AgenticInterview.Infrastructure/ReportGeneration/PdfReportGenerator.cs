using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AgenticInterview.Application.Abstractions;

namespace AgenticInterview.Infrastructure.ReportGeneration;

/// <summary>
/// Generates a comprehensive post-interview PDF report using QuestPDF.
/// </summary>
public class PdfReportGenerator : IReportGenerator
{
    private readonly AgenticInterview.Domain.Interfaces.IRepository<AgenticInterview.Domain.Entities.InterviewSession> _sessionRepository;
    private readonly AgenticInterview.Domain.Interfaces.IRepository<AgenticInterview.Domain.Entities.CandidateProfile> _candidateRepository;
    public PdfReportGenerator(
        AgenticInterview.Domain.Interfaces.IRepository<AgenticInterview.Domain.Entities.InterviewSession> sessionRepository,
        AgenticInterview.Domain.Interfaces.IRepository<AgenticInterview.Domain.Entities.CandidateProfile> candidateRepository)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        _sessionRepository = sessionRepository;
        _candidateRepository = candidateRepository;
    }

    public async Task<byte[]> GenerateInterviewReportAsync(Guid sessionId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId);
        if (session == null) throw new ArgumentException("Session not found", nameof(sessionId));

        var candidate = await _candidateRepository.GetByIdAsync(session.CandidateProfileId);
        string candidateName = candidate?.Name ?? "Unknown Candidate";
        
        // Mocking role since JobDescription isn't fully implemented in this prototype layer yet
        string role = "Target Role"; 
        
        int finalScore = session.FinalScore?.Value ?? 0;
        
        // Transcript persistence is not yet in the DB model. For now, we list the asked questions.
        var transcript = session.Questions.Select(q => $"Question: {q.Content}").ToList();
        if (!transcript.Any()) transcript.Add("No questions were recorded in this session.");
        
        var flags = session.Incidents.Select(i => $"{i.Type} - {i.AgentReasoning}").ToList();
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(ComposeHeader);
                page.Content().Element(x => ComposeContent(x, candidateName, role, finalScore, transcript, flags));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return ms.ToArray();
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("AgenticInterview - Assessment Report").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Generated: {DateTime.Now:d}").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeContent(IContainer container, string candidateName, string role, int finalScore, List<string> transcript, List<string> flags)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(20);

            // Summary Section
            column.Item().Text("Executive Summary").FontSize(14).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(120);
                    columns.RelativeColumn();
                });

                table.Cell().Text("Candidate:");
                table.Cell().Text(candidateName).SemiBold();
                
                table.Cell().Text("Target Role:");
                table.Cell().Text(role);
                
                table.Cell().Text("Final Score:");
                table.Cell().Text($"{finalScore}/100").SemiBold().FontColor(finalScore > 70 ? Colors.Green.Medium : Colors.Red.Medium);
            });

            // Proctoring Flags
            column.Item().Text("Proctoring & Malpractice Events").FontSize(14).SemiBold();
            if (flags.Any())
            {
                foreach (var flag in flags)
                {
                    column.Item().Text($"• {flag}").FontColor(Colors.Red.Medium);
                }
            }
            else
            {
                column.Item().Text("No malpractice detected. Clean session.").FontColor(Colors.Green.Medium);
            }

            // Transcript snippet
            column.Item().Text("Interview Transcript (Snippet)").FontSize(14).SemiBold();
            foreach (var line in transcript.Take(10))
            {
                column.Item().Text(line).FontSize(10).FontColor(Colors.Grey.Darken2);
            }
        });
    }
}
