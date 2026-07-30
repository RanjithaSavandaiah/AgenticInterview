using System;

namespace AgenticInterview.AgenticSystem.State;

public record BlackboardMessage(string SourceAgent, string Content, DateTime Timestamp);
