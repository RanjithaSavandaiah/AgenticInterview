using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace AgenticInterview.Infrastructure.PromptOptimization;

/// <summary>
/// Simulates DSPy-like Prompt Optimization by compiling a few-shot prompt
/// based on positive examples and rubric scoring.
/// </summary>
public class InterviewPromptOptimizer
{
    private readonly Kernel _kernel;

    public InterviewPromptOptimizer(Kernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>
    /// Compiles an optimized system prompt for an agent by bootstrapping few-shot examples.
    /// This acts as a 'Teleprompter' in DSPy terminology.
    /// </summary>
    public async Task<string> OptimizePromptAsync(string basePrompt, List<Example> trainingExamples, string metricDescription)
    {
        // In a full DSPy implementation, this would iteratively invoke the LLM to maximize the metric.
        // For this architecture, we compile the best examples directly into the system prompt.
        var optimizedPrompt = $"{basePrompt}\n\n## Rubric & Optimization Metric\n{metricDescription}\n\n## Few-Shot Examples for Alignment\n";

        foreach (var example in trainingExamples)
        {
            optimizedPrompt += $"Input: {example.Input}\nIdeal Response: {example.Output}\n\n";
        }

        // We can use Semantic Kernel to re-write and refine the prompt itself (Meta-Prompting).
        var metaPrompt = $"You are an expert prompt engineer. Enhance the following instructions to ensure maximum adherence to the rubric. Make it strict and agentic.\n\nInstructions:\n{optimizedPrompt}";
        
        try
        {
            var result = await _kernel.InvokePromptAsync(metaPrompt);
            return result.GetValue<string>() ?? optimizedPrompt;
        }
        catch
        {
            // Fallback to static compiled prompt if LLM fails
            return optimizedPrompt;
        }
    }
}

public class Example
{
    public string Input { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
}
