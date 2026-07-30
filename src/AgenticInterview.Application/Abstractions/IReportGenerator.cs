using System;
using System.Threading.Tasks;

namespace AgenticInterview.Application.Abstractions;

public interface IReportGenerator
{
    Task<byte[]> GenerateInterviewReportAsync(Guid sessionId);
}
