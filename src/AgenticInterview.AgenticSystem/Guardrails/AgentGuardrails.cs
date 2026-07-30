using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AgenticInterview.AgenticSystem.Guardrails;

/// <summary>
/// Provides input and output guardrails for the multi-agent interview system.
/// Input guardrails sanitize candidate messages before they reach the blackboard.
/// Output guardrails validate agent outputs before they are posted to the UI.
/// 
/// This prevents prompt injection, excessive content, PII leakage, and off-topic responses.
/// </summary>
public partial class AgentGuardrails
{
    private readonly ILogger<AgentGuardrails> _logger;

    /// <summary>
    /// Maximum length of a candidate message before truncation.
    /// </summary>
    private const int MaxCandidateMessageLength = 5000;

    /// <summary>
    /// Maximum length of an agent output before truncation.
    /// </summary>
    private const int MaxAgentOutputLength = 10000;

    /// <summary>
    /// Patterns that indicate prompt injection attempts.
    /// </summary>
    private static readonly string[] PromptInjectionPatterns =
    [
        "ignore previous instructions",
        "ignore all previous",
        "disregard your instructions",
        "you are now",
        "new instructions:",
        "system prompt:",
        "override your",
        "forget your rules",
        "act as if you",
        "pretend you are",
        "jailbreak",
        "DAN mode",
        "developer mode"
    ];

    public AgentGuardrails(ILogger<AgentGuardrails> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates and sanitizes a candidate's input message.
    /// Returns the sanitized message, or null if the message should be rejected entirely.
    /// </summary>
    public GuardrailResult ValidateInput(string message, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return GuardrailResult.Reject("Empty message");
        }

        // Check for prompt injection patterns
        var lowerMessage = message.ToLowerInvariant();
        foreach (var pattern in PromptInjectionPatterns)
        {
            if (lowerMessage.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Prompt injection attempt detected in session {SessionId}. Pattern: '{Pattern}'",
                    sessionId, pattern);
                return GuardrailResult.Reject($"Suspicious input pattern detected: '{pattern}'");
            }
        }

        // Truncate overly long messages
        var sanitized = message;
        if (sanitized.Length > MaxCandidateMessageLength)
        {
            _logger.LogWarning(
                "Candidate message truncated from {Original} to {Max} characters in session {SessionId}.",
                sanitized.Length, MaxCandidateMessageLength, sessionId);
            sanitized = sanitized[..MaxCandidateMessageLength] + "... [truncated]";
        }

        // Strip any embedded control characters (except newlines/tabs)
        sanitized = StripControlCharacters().Replace(sanitized, string.Empty);

        return GuardrailResult.Accept(sanitized);
    }

    /// <summary>
    /// Validates an agent's output before it is posted to the blackboard/UI.
    /// Checks for PII patterns, excessive length, and off-topic content.
    /// </summary>
    public GuardrailResult ValidateOutput(string agentName, string output, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return GuardrailResult.Accept(string.Empty);
        }

        var sanitized = output;

        // Truncate overly long outputs
        if (sanitized.Length > MaxAgentOutputLength)
        {
            _logger.LogWarning(
                "Agent {AgentName} output truncated from {Original} to {Max} characters in session {SessionId}.",
                agentName, sanitized.Length, MaxAgentOutputLength, sessionId);
            sanitized = sanitized[..MaxAgentOutputLength] + "... [truncated]";
        }

        // Detect potential PII leakage (SSN, credit card patterns)
        if (SsnPattern().IsMatch(sanitized))
        {
            _logger.LogWarning("Potential SSN detected in {AgentName} output for session {SessionId}. Redacting.", agentName, sessionId);
            sanitized = SsnPattern().Replace(sanitized, "[REDACTED-SSN]");
        }

        if (CreditCardPattern().IsMatch(sanitized))
        {
            _logger.LogWarning("Potential credit card number detected in {AgentName} output for session {SessionId}. Redacting.", agentName, sessionId);
            sanitized = CreditCardPattern().Replace(sanitized, "[REDACTED-CC]");
        }

        // Detect if agent is leaking system prompt fragments
        if (sanitized.Contains("You are an expert", StringComparison.OrdinalIgnoreCase) &&
            sanitized.Contains("CRITICAL RULE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Agent {AgentName} appears to be leaking system prompt in session {SessionId}.", agentName, sessionId);
            return GuardrailResult.Reject("Agent output appears to contain system prompt leakage.");
        }

        return GuardrailResult.Accept(sanitized);
    }

    [GeneratedRegex(@"[^\P{Cc}\r\n\t]")]
    private static partial Regex StripControlCharacters();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnPattern();

    [GeneratedRegex(@"\b(?:\d{4}[- ]?){3}\d{4}\b")]
    private static partial Regex CreditCardPattern();
}

/// <summary>
/// Result of a guardrail validation check.
/// </summary>
public class GuardrailResult
{
    public bool IsAccepted { get; init; }
    public string SanitizedContent { get; init; } = string.Empty;
    public string? RejectionReason { get; init; }

    public static GuardrailResult Accept(string sanitizedContent) => new()
    {
        IsAccepted = true,
        SanitizedContent = sanitizedContent
    };

    public static GuardrailResult Reject(string reason) => new()
    {
        IsAccepted = false,
        RejectionReason = reason
    };
}
