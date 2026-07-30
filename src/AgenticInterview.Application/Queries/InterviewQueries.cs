using System;
using System.Collections.Generic;
using MediatR;
using AgenticInterview.Application.DataTransferObjects;

namespace AgenticInterview.Application.Queries;

/// <summary>
/// Query to retrieve the current status of an interview session.
/// </summary>
public record GetInterviewSessionStatusQuery(Guid SessionId) : IRequest<InterviewStatusDto?>;

/// <summary>
/// Query to retrieve all active interview sessions (for HR Dashboard).
/// </summary>
public record GetActiveInterviewsQuery() : IRequest<IReadOnlyList<InterviewStatusDto>>;

/// <summary>
/// Query to retrieve a candidate's profile by ID.
/// </summary>
public record GetCandidateProfileQuery(Guid CandidateId) : IRequest<CandidateProfileDto?>;
