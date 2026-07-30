using System.Threading;
using System.Threading.Tasks;
using AgenticInterview.AgenticSystem.State;

namespace AgenticInterview.AgenticSystem.Agents;

public interface IAgent
{
    string Name { get; }
    string Goal { get; }
    Task ExecuteAsync(InterviewBlackboard blackboard, CancellationToken cancellationToken = default);
}
