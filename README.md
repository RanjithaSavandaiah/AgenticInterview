# AgenticInterview

> **A fully autonomous, multi-agent AI interview platform** built with .NET 10, Angular 21, Microsoft Agent Framework (MAF), and real-time SignalR communication.

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-21-red)](https://angular.dev/)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        Angular 21 Frontend                       │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐ │
│  │ Setup Page  │  │Interview Room│  │    HR Dashboard         │ │
│  │(File Upload)│  │(Monaco+Voice)│  │ (Live Observation)      │ │
│  └─────────────┘  └──────────────┘  └─────────────────────────┘ │
│                          │ SignalR                                │
├──────────────────────────┼───────────────────────────────────────┤
│                     ASP.NET API Layer                            │
│  ┌──────────┐  ┌────────────┐  ┌─────────────┐                 │
│  │REST APIs │  │SignalR Hubs│  │  Swagger UI │                 │
│  └──────────┘  └────────────┘  └─────────────┘                 │
├─────────────────────────────────────────────────────────────────┤
│                   Application Layer (MediatR/CQRS)              │
├─────────────────────────────────────────────────────────────────┤
│                   Multi-Agent Orchestration System               │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│  │Technical │ │Behavioral│ │Proctoring│ │Evaluation│          │
│  │Intervwr  │ │Intervwr  │ │  Agent   │ │  Agent   │          │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│  │   HR     │ │Moderator │ │   Web    │ │  Code    │          │
│  │ Observer │ │  Agent   │ │ Search   │ │Execution │          │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘          │
├─────────────────────────────────────────────────────────────────┤
│                   Infrastructure Layer                           │
│  ┌────────┐ ┌──────────┐ ┌──────┐ ┌──────────┐ ┌───────────┐  │
│  │EF Core │ │IChatClient│ │ RAG │ │   OTel   │ │PDF Reports│  │
│  │SQLite  │ │ Pipeline │ │Store │ │ Metrics  │ │Generator  │  │
│  └────────┘ └──────────┘ └──────┘ └──────────┘ └───────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                      Domain Layer (DDD)                          │
│  InterviewSession │ CandidateProfile │ JobDescription │ Score   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Features

### Multi-Agent AI System
- **8 specialized agents**, each with distinct skills and responsibilities
- **Skill-based MCP tool resolution** — agents only receive tools matching their capabilities
- **MAF ChatClientAgent harness** with automatic context compaction (summarization + truncation)
- **Shared blackboard** for inter-agent communication

### Agent Communication (Blackboard Pattern)
Agents in this system do not call each other directly. Instead, they communicate using a **Blackboard Pattern** managed by the `InterviewOrchestrator`:
1. **Central State (Blackboard)**: The `InterviewStore` and `BlackboardManager` hold the current context (transcript, candidate code, current question, proctoring flags).
2. **Orchestrator**: The `InterviewOrchestrator` decides which agent should run based on the current interview state (e.g., passing control to the `CodeExecution` agent when code is submitted, or the `HR Observer` to generate live notes).
3. **Context Sharing**: When an agent is invoked, the Orchestrator pulls the latest relevant data from the Blackboard and passes it as `ChatMessages` into the agent's context, ensuring every agent has exactly the context it needs without tight coupling.

### Real-Time Interview Experience
- **Web Speech API** — Speech-to-Text for candidate and Text-to-Speech for AI interviewer
- **Monaco code editor** — Full IDE experience with C# syntax highlighting
- **SignalR** — Real-time bidirectional communication between frontend and backend
- **Face detection** — Chromium FaceDetector API for multi-face proctoring

### Proctoring & Anti-Cheating
- Tab switch detection (`visibilitychange`)
- Window blur monitoring
- Copy/paste interception
- Multi-face detection via camera
- Automatic strike counting with session termination

### HR Dashboard
- Live interview observation
- Real-time transcript streaming
- Candidate code view
- AI assessment notes
- Score & confidence metrics

### Clean Architecture
- **Domain Layer** — Entities, Value Objects, Domain Events (DDD)
- **Application Layer** — Commands/Queries via MediatR (CQRS)
- **Infrastructure Layer** — EF Core, IChatClient pipeline, RAG, Observability
- **API Layer** — REST endpoints, SignalR hubs, Swagger

---

## Agent Details

| Agent | Role | Skills | Tools |
|-------|------|--------|-------|
| **Technical Interviewer** | Asks coding/system design questions | `ask-technical-question`, `adaptive-difficulty` | `evaluate_answer`, `fetch_question`, `search_resume_context` |
| **Behavioral Interviewer** | STAR method questions, culture fit | `behavioral-question` | `fetch_question`, `search_resume_context` |
| **Code Execution** | Static code analysis | `static-analysis` | `evaluate_answer` |
| **Proctoring** | Monitors for cheating | `detect-malpractice` | `record_proctoring_event` |
| **Evaluation** | Scores answers, final report | `score-aggregation` | `evaluate_answer`, `submit_final_score` |
| **HR Observer** | Real-time HR summaries | `live-summary` | `search_resume_context` |
| **Moderator** | Orchestrates flow, timing | `orchestration` | `fetch_question` |
| **Web Search** | Fact-checks specialized answers | `web-lookup` | `search_web` |

---

## IChatClient Pipeline

The AI pipeline is built using the `Microsoft.Extensions.AI` middleware pattern:

```
Request → OpenTelemetry → Logging → FunctionInvocation → CachedDecorator → FallbackClient → LLM
                                                                                ├── Gemini (Primary)
                                                                                └── Groq (Fallback)
```

- **OpenTelemetry** — Full request/response tracing with custom interview metrics
- **Logging** — Structured logging with correlation IDs
- **FunctionInvocation** — Automatic MCP tool calling
- **CachedDecorator** — Response caching for repeated queries
- **FallbackClient** — Automatic failover from Gemini → Groq

---

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- API keys for [Google Gemini](https://aistudio.google.com/) and/or [Groq](https://console.groq.com/)

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/RanjithaSavandaiah/AgenticInterview.git
   cd AgenticInterview
   ```

2. **Configure API keys**
   
   Create `src/AgenticInterview.Api/appsettings.Development.json`:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=agentic_interview.db",
       "VectorDbConnection": "Data Source=vectorstore.db"
     },
     "AI": {
       "GeminiApiKey": "YOUR_GEMINI_API_KEY",
       "GroqApiKey": "YOUR_GROQ_API_KEY"
     }
   }
   ```

3. **Run the backend**
   ```bash
   cd src/AgenticInterview.Api
   dotnet run
   ```

4. **Run the frontend**
   ```bash
   cd src/AgenticInterview.UI
   npm install
   npm start
   ```

5. **Open the app** at `http://localhost:4200`

---

## Testing

### Backend Tests (32 tests)
```bash
dotnet test src/AgenticInterview.slnx
```

| Project | Tests | Coverage |
|---------|-------|----------|
| `AgenticInterview.Domain.Tests` | 9 | Entities, Value Objects |
| `AgenticInterview.Application.Tests` | 5 | Command Handlers |
| `AgenticInterview.AgenticSystem.Tests` | 6 | Orchestrator, Blackboard |
| `AgenticInterview.Api.Tests` | 8 | Controllers, Hubs |
| `AgenticInterview.Infrastructure.Tests` | 4 | CachedClient, FallbackClient |

### Frontend Tests (124 tests)
```bash
cd src/AgenticInterview.UI
npm test
```

| Spec File | Tests | Coverage |
|-----------|-------|----------|
| `interview.store.spec.ts` | 34 | State management, SignalR, HTTP |
| `setup-interview.component.spec.ts` | 31 | File upload, validation, error handling |
| `interview-room.spec.ts` | 28 | Proctoring, TTS, code editor, overlays |
| `hr-dashboard.spec.ts` | 15 | Metrics, transcript, live view |
| `app.routes.spec.ts` | 9 | Routing, navigation, wildcards |
| `signalr.spec.ts` | 4 | Service lifecycle |
| `app.spec.ts` | 3 | Root component |

---

## Project Structure

```
AgenticInterview/
├── src/
│   ├── AgenticInterview.Domain/          # Entities, Value Objects, Domain Events
│   ├── AgenticInterview.Application/     # Commands, Queries, MediatR Handlers
│   ├── AgenticInterview.Infrastructure/  # EF Core, AI Pipeline, RAG, Observability
│   ├── AgenticInterview.Api/             # REST Controllers, SignalR Hubs, Swagger
│   ├── AgenticInterview.AgenticSystem/   # Multi-Agent System
│   │   ├── Agents/                       # 8 AI Agents + BaseAgent
│   │   ├── AgentCards/                   # Agent metadata & skill definitions
│   │   ├── Core/                         # Orchestrator, Blackboard, ToolResolver
│   │   ├── Guardrails/                   # Output validation & sanitization
│   │   ├── McpTools/                     # MCP tool factory & skill mapping
│   │   ├── Memory/                       # Conversation memory store
│   │   └── State/                        # Interview state management
│   └── AgenticInterview.UI/             # Angular 21 Frontend
│       └── src/app/
│           ├── interview-room/           # Candidate interview experience
│           ├── hr-dashboard/             # HR live observation panel
│           ├── setup-interview/          # File upload & session start
│           └── state/                    # NgRx Signal Store
├── tests/
│   ├── AgenticInterview.Domain.Tests/
│   ├── AgenticInterview.Application.Tests/
│   ├── AgenticInterview.AgenticSystem.Tests/
│   ├── AgenticInterview.Api.Tests/
│   └── AgenticInterview.Infrastructure.Tests/
└── README.md
```

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| **Runtime** | .NET 10, C# 14 |
| **Frontend** | Angular 21, TypeScript 5.9 |
| **State** | NgRx Signal Store |
| **AI** | Microsoft.Extensions.AI, Microsoft Agent Framework (MAF) |
| **LLMs** | Google Gemini, Groq (Llama) |
| **Real-time** | SignalR |
| **Database** | SQLite + EF Core |
| **Code Editor** | Monaco Editor (VS Code engine) |
| **Speech** | Web Speech API (STT/TTS) |
| **Observability** | OpenTelemetry |
| **Testing** | xUnit, Moq, Vitest, Angular TestBed |
| **Architecture** | Clean Architecture, DDD, CQRS, MediatR |

---

## License

This project is licensed under the MIT License.

---

<p align="center">
  Built by <a href="https://github.com/RanjithaSavandaiah">Ranjitha Savandaiah</a>
</p>
