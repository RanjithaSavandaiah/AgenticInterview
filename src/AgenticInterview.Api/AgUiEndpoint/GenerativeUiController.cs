using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Core;

namespace AgenticInterview.Api.AgUiEndpoint;

[ApiController]
[Route("api/agui")]
public class GenerativeUiController : ControllerBase
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Microsoft.Extensions.AI.IChatClient _chatClient;
    private readonly IBlackboardManager _blackboardManager;

    public GenerativeUiController(
        Microsoft.Extensions.AI.IChatClient chatClient,
        IBlackboardManager blackboardManager)
    {
        _chatClient = chatClient;
        _blackboardManager = blackboardManager;
    }

    /// <summary>
    /// Implements the Agentic UI (AgUI) protocol.
    /// This endpoint allows the AI to dynamically stream structured JSON defining
    /// frontend Angular components (e.g., triggering a whiteboard or specific code editor layout)
    /// instead of just plain text responses.
    /// </summary>
    [HttpPost("generate-component")]
    public async Task<IActionResult> GenerateComponent([FromBody] AgUiRequest request)
    {
        // Use the actual LLM to dynamically select the UI component
        var systemPrompt = @"You are a Generative UI orchestrator. Based on the interview context, decide which frontend widget to display to the candidate.
Output ONLY a JSON string containing ComponentType, StatePayload, and AgentAction.
Valid ComponentTypes: 'MonacoEditorWidget', 'BehavioralPromptWidget', 'WhiteboardWidget'.";

        var chatOptions = new Microsoft.Extensions.AI.ChatOptions { ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json };
        var aiResponse = await _chatClient.GetResponseAsync(
            new[] { 
                new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.System, systemPrompt),
                new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, $"Context: {request.Context}")
            }, chatOptions);

        try
        {
            var response = JsonSerializer.Deserialize<AgUiResponse>(aiResponse.Text ?? "{}");
            if (response != null && !string.IsNullOrEmpty(response.ComponentType))
            {
                return Ok(response);
            }
        }
        catch
        {
            // Fallback if LLM fails JSON parsing
        }

        return Ok(new AgUiResponse
        {
            ComponentType = "BehavioralPromptWidget",
            AgentAction = "CONTINUE",
            StatePayload = new { }
        });
    }

    /// <summary>
    /// AG-UI Server-Sent Events (SSE) endpoint.
    /// Streams structured AG-UI lifecycle events for a given session:
    /// LIFECYCLE_START, STATE_DELTA, TEXT_MESSAGE_CONTENT, TOOL_CALL, LIFECYCLE_END.
    /// 
    /// Conforms to the AG-UI protocol specification for real-time agent → UI communication.
    /// </summary>
    [HttpGet("events/{sessionId}")]
    [Produces("text/event-stream")]
    public async Task StreamEvents(Guid sessionId, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var blackboard = _blackboardManager.GetOrCreate(sessionId);

        // Send LIFECYCLE_START
        await WriteAgUiEvent(new AgUiEvent
        {
            Type = "LIFECYCLE_START",
            SessionId = sessionId.ToString(),
            Timestamp = DateTime.UtcNow
        });

        // Subscribe to blackboard messages and stream them as AG-UI events
        var messageQueue = System.Threading.Channels.Channel.CreateUnbounded<AgenticInterview.AgenticSystem.State.BlackboardMessage>();

        void OnMessageAdded(object? sender, AgenticInterview.AgenticSystem.State.BlackboardMessage msg)
        {
            messageQueue.Writer.TryWrite(msg);
        }

        blackboard.MessageAdded += OnMessageAdded;

        try
        {
            await foreach (var message in messageQueue.Reader.ReadAllAsync(cancellationToken))
            {
                var eventType = message.SourceAgent switch
                {
                    AgenticInterview.AgenticSystem.Common.AgenticConstants.CandidateSourceName => "TEXT_MESSAGE_CONTENT",
                    AgenticInterview.AgenticSystem.Common.AgenticConstants.ProctoringAgentName => "STATE_DELTA",
                    _ => "TEXT_MESSAGE_CONTENT"
                };

                await WriteAgUiEvent(new AgUiEvent
                {
                    Type = eventType,
                    SessionId = sessionId.ToString(),
                    Timestamp = message.Timestamp,
                    Data = new
                    {
                        sourceAgent = message.SourceAgent,
                        content = message.Content
                    }
                });

                // Check if the session has ended
                var status = blackboard.Get<string>(AgenticInterview.AgenticSystem.Common.AgenticConstants.SessionStatusKey);
                if (status is AgenticInterview.AgenticSystem.Common.AgenticConstants.StatusCompleted
                    or AgenticInterview.AgenticSystem.Common.AgenticConstants.StatusTerminated)
                {
                    break;
                }
            }
        }
        finally
        {
            blackboard.MessageAdded -= OnMessageAdded;
        }

        // Send LIFECYCLE_END
        await WriteAgUiEvent(new AgUiEvent
        {
            Type = "LIFECYCLE_END",
            SessionId = sessionId.ToString(),
            Timestamp = DateTime.UtcNow
        });
    }

    private async Task WriteAgUiEvent(AgUiEvent agUiEvent)
    {
        var json = JsonSerializer.Serialize(agUiEvent, CamelCaseOptions);

        await Response.WriteAsync($"event: {agUiEvent.Type}\n", CancellationToken.None);
        await Response.WriteAsync($"data: {json}\n\n", CancellationToken.None);
        await Response.Body.FlushAsync(CancellationToken.None);
    }
}

public class AgUiRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
}

public class AgUiResponse
{
    public string ComponentType { get; set; } = string.Empty;
    public object StatePayload { get; set; } = new object();
    public string AgentAction { get; set; } = string.Empty;
}

/// <summary>
/// Represents a structured AG-UI event conforming to the protocol specification.
/// </summary>
public class AgUiEvent
{
    public string Type { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}
