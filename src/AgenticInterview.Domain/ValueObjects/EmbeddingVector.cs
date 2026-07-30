using System;

namespace AgenticInterview.Domain.ValueObjects;

/// <summary>
/// Represents a vector embedding for a piece of text.
/// </summary>
public record EmbeddingVector(ReadOnlyMemory<float> Vector);
