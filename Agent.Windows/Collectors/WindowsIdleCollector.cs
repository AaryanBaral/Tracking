using Agent.Shared.Abstractions;
using Agent.Shared.Models;
using Agent.Windows.Native;

namespace Agent.Windows.Collectors;

public class WindowsIdleCollector : IIdleCollector
{
    public Task<IdleEvent?> GetIdleAsync(CancellationToken cancellationToken)
    {
        var seconds = WindowsInput.GetIdleSeconds();
        return Task.FromResult<IdleEvent?>(
            new IdleEvent(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(seconds)));
    }
}
