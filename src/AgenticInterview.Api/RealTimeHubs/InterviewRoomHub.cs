using Microsoft.AspNetCore.SignalR;

namespace AgenticInterview.Api.RealTimeHubs;

/// <summary>
/// SignalR Hub for the candidate's interview room.
/// Handles real-time bidirectional communication between the candidate's browser
/// and the agentic backend (e.g., receiving speech transcripts, sending AI questions,
/// streaming code evaluation results).
/// </summary>
public class InterviewRoomHub : Hub
{
    private readonly ILogger<InterviewRoomHub> _logger;

    public InterviewRoomHub(ILogger<InterviewRoomHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a candidate connects to the interview room.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Candidate connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("ReceiveSystemMessage", "Connected to the Interview Room. Please wait for the AI interviewer.");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a candidate disconnects from the interview room.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Candidate disconnected: {ConnectionId}. Reason: {Reason}",
            Context.ConnectionId, exception?.Message ?? "Normal disconnect");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Receives a speech transcript segment from the candidate.
    /// </summary>
    public async Task SendTranscript(string sessionId, string transcript)
    {
        _logger.LogInformation("Transcript received for session {SessionId}: {Snippet}",
            sessionId, transcript.Length > 50 ? transcript[..50] + "..." : transcript);
        
        // Forward to all HR observers watching this session
        await Clients.Others.SendAsync("ReceiveTranscript", sessionId, transcript);
    }

    /// <summary>
    /// Receives a code submission from the candidate's Monaco editor.
    /// </summary>
    public async Task SubmitCode(string sessionId, string code)
    {
        _logger.LogInformation("Code submitted for session {SessionId}. Length: {Length} chars.", sessionId, code.Length);
        
        // Forward to HR dashboard for live code view
        await Clients.Others.SendAsync("ReceiveCodeUpdate", sessionId, code);
    }

    /// <summary>
    /// Receives a proctoring violation event from the candidate's browser.
    /// </summary>
    public async Task ReportProctoringEvent(string sessionId, string eventType)
    {
        _logger.LogWarning("Proctoring event for session {SessionId}: {EventType}", sessionId, eventType);
        
        // Forward to HR dashboard
        await Clients.Others.SendAsync("ReceiveProctoringAlert", sessionId, eventType);
    }

    /// <summary>
    /// Sends an AI-generated question to the candidate.
    /// This is invoked server-side by the agents, not by the client.
    /// </summary>
    public async Task SendQuestionToCandidate(string connectionId, string question)
    {
        _logger.LogInformation("Sending question to candidate {ConnectionId}", connectionId);
        await Clients.Client(connectionId).SendAsync("ReceiveQuestion", question);
    }
}
