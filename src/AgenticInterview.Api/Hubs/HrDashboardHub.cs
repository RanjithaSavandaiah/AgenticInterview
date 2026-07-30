using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

using AgenticInterview.AgenticSystem.Common;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.Guardrails;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.Api.Hubs;

public class HrDashboardHub : Hub
{
    private readonly IBlackboardManager _blackboardManager;
    private readonly AgentGuardrails _guardrails;

    public HrDashboardHub(IBlackboardManager blackboardManager, AgentGuardrails guardrails)
    {
        _blackboardManager = blackboardManager;
        _guardrails = guardrails;
    }

    public async Task SendInterviewUpdate(string sessionId, string message)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid))
            return;

        var blackboard = _blackboardManager.GetOrCreate(sessionGuid);

        // Check if this is a malpractice event
        if (message.StartsWith("[MALPRACTICE]"))
        {
            var violationType = message.Replace("[MALPRACTICE] ", "").Trim();
            await HandleMalpracticeEvent(blackboard, violationType, sessionId);
            return; // Do NOT add malpractice tags to the transcript
        }

        // Input guardrail: validate candidate message before it reaches the blackboard
        var guardrailResult = _guardrails.ValidateInput(message, sessionId);
        if (!guardrailResult.IsAccepted)
        {
            // Silently reject — don't tell the candidate their input was blocked
            // (this prevents them from probing the guardrail's detection patterns)
            return;
        }

        var sanitizedMessage = guardrailResult.SanitizedContent;

        // Regular candidate message — add to blackboard + transcript
        blackboard.AddMessage(new BlackboardMessage(AgenticConstants.CandidateSourceName, sanitizedMessage, DateTime.UtcNow));

        var currentTranscript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
        blackboard.Set(AgenticConstants.CurrentTranscriptKey, currentTranscript + $"\n{AgenticConstants.CandidateSourceName}: {sanitizedMessage}");
    }

    private async Task HandleMalpracticeEvent(InterviewBlackboard blackboard, string violationType, string sessionId)
    {
        // Check if already terminated
        if (blackboard.Get<string>(AgenticConstants.SessionStatusKey) == AgenticConstants.StatusTerminated)
        {
            return; // Ignore further violations once terminated
        }

        // Increment strike count
        var strikes = blackboard.Get<int>(AgenticConstants.ProctoringStrikeCountKey) + 1;
        blackboard.Set(AgenticConstants.ProctoringStrikeCountKey, strikes);

        // Increment observability metric
        AgenticInterview.AgenticSystem.Common.AgentMetrics.ProctoringViolations.Add(1,
            new System.Collections.Generic.KeyValuePair<string, object?>("violation.type", violationType));

        // Set flag for ProctoringAgent to analyze on its next cycle
        blackboard.Set(AgenticConstants.PendingMalpracticeKey, violationType);

        // Determine the human-readable violation description
        var violationDescription = violationType switch
        {
            "TAB_SWITCH" => "you switched away from the interview window",
            "WINDOW_BLUR" => "you navigated away from the interview window",
            "COPY_ATTEMPT" => "you attempted to copy content",
            "PASTE_ATTEMPT" => "you attempted to paste content",
            "MULTIPLE_FACES" => "another person was detected in your camera frame",
            _ => "a suspicious activity was detected"
        };

        // Generate spoken warning based on strike count
        string warningMessage;
        if (strikes >= AgenticConstants.MaxProctoringStrikes)
        {
            warningMessage = $"This is your third and final violation. I detected that {violationDescription}. " +
                             "The interview is now terminated due to repeated malpractice. Thank you for your time.";
            blackboard.Set(AgenticConstants.SessionStatusKey, AgenticConstants.StatusTerminated);

            // Notify UI about status change
            await Clients.All.SendAsync("ReceiveUpdate", sessionId, System.Text.Json.JsonSerializer.Serialize(new { type = "StatusChanged", status = AgenticConstants.StatusTerminated }));

            // Add termination to transcript so agents know the session ended
            var transcript = blackboard.Get<string>(AgenticConstants.CurrentTranscriptKey) ?? string.Empty;
            blackboard.Set(AgenticConstants.CurrentTranscriptKey, transcript + $"\n[{AgenticConstants.SystemSourceName}]: Interview terminated due to {AgenticConstants.MaxProctoringStrikes} malpractice violations.");
        }
        else
        {
            var remaining = AgenticConstants.MaxProctoringStrikes - strikes;
            warningMessage = $"Warning {strikes} of {AgenticConstants.MaxProctoringStrikes}. I noticed that {violationDescription}. " +
                             $"Please be aware that this has been recorded. " +
                             $"You have {remaining} warning{(remaining == 1 ? "" : "s")} remaining before the interview is automatically terminated.";
        }

        // Post the warning as a message from the Proctor agent — this will be spoken by TTS
        blackboard.AddMessage(new BlackboardMessage(AgenticConstants.ProctoringAgentName, warningMessage, DateTime.UtcNow));
    }

    public async Task JoinSession(string sessionId)
    {
        if (Guid.TryParse(sessionId, out var sessionGuid))
        {
            var blackboard = _blackboardManager.GetOrCreate(sessionGuid);
            blackboard.Set(AgenticConstants.CandidateJoinedKey, true);
        }
    }
}
