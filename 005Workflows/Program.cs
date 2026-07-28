using Microsoft.Agents.AI.Workflows;

namespace WorkflowSample;

public static class Program
{
    private static async Task Main()
    {
        Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
        var upperCase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");

        ReverseTextExecutor reverse = new();

        WorkflowBuilder builder = new(upperCase);
        builder.AddEdge(upperCase, reverse).WithOutputFrom(reverse);
        var workflow = builder.Build();

        await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
        foreach (WorkflowEvent evt in run.NewEvents)
        {
            if (evt is ExecutorCompletedEvent executorComplete)
            {
                Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
            }
        }
    }
}

internal sealed class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}