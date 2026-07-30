using System;
using System.Text.Json;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.Core;
using AgenticInterview.AgenticSystem.State;
using Microsoft.AspNetCore.SignalR;

namespace AgenticInterview.Api.Hubs;

public class SignalRSessionNotifier : ISessionNotifier
{
    private readonly IHubContext<HrDashboardHub> _hubContext;

    public SignalRSessionNotifier(IHubContext<HrDashboardHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMessageAddedAsync(Guid sessionId, BlackboardMessage message)
    {
        // Serialize the message so it matches what the frontend expects
        var payload = JsonSerializer.Serialize(new
        {
            sourceAgent = message.SourceAgent,
            content = message.Content,
            timestamp = message.Timestamp.ToString("O")
        });

        // Send to all clients
        await _hubContext.Clients.All.SendAsync("ReceiveUpdate", sessionId.ToString(), payload);
    }
}
