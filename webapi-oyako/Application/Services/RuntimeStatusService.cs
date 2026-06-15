// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/RuntimeStatusService.cs for maintainers.
using System.Threading.Channels;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements the RuntimeStatusService component and its responsibilities in the Oyako codebase.
public sealed class RuntimeStatusService : IRuntimeStatusService
{
    // Stores state or a dependency required by the surrounding component.
    private readonly object _gate = new();
    // Stores state or a dependency required by the surrounding component.
    private readonly List<Channel<RuntimeStatusSnapshot>> _subscribers = new();
    // Stores state or a dependency required by the surrounding component.
    private RuntimeStatusSnapshot _current = new(
        "app",
        "ready_for_question",
        "ready",
        1,
        1,
        true,
        "Uygulama Hazır",
        "ready",
        "message",
        null,
        DateTime.UtcNow);

    public RuntimeStatusSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return _current;
            }
        }
    }

    public Task PublishAsync(
        string operation,
        string phase,
        string stepKey,
        int stepIndex,
        int stepCount,
        bool isTerminal,
        string message,
        string severity,
        string icon,
        int? pageCount = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeStepCount = Math.Max(1, stepCount);
        // Creates the object needed for the next step of the workflow.
        var snapshot = new RuntimeStatusSnapshot(
            operation,
            phase,
            stepKey,
            Math.Clamp(stepIndex, 1, safeStepCount),
            safeStepCount,
            isTerminal,
            message,
            severity,
            icon,
            pageCount,
            DateTime.UtcNow);

        lock (_gate)
        {
            _current = snapshot;
            for (var i = _subscribers.Count - 1; i >= 0; i--)
            {
                // Guards the following branch so the workflow handles this condition deliberately.
                if (!_subscribers[i].Writer.TryWrite(snapshot))
                {
                    _subscribers.RemoveAt(i);
                }
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<RuntimeStatusSnapshot> WatchAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Creates the object needed for the next step of the workflow.
        var channel = Channel.CreateUnbounded<RuntimeStatusSnapshot>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        lock (_gate)
        {
            // Registers or maps application behavior into the runtime pipeline.
            _subscribers.Add(channel);
            channel.Writer.TryWrite(_current);
        }

        try
        {
            // Awaits the asynchronous operation so the workflow continues only after the dependency completes.
            await foreach (var snapshot in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return snapshot;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
