using Agent.Shared.Abstractions;
using Agent.Shared.Models;
using Agent.Windows.Native;

namespace Agent.Windows.Collectors;

public class WindowsAppCollector : IAppCollector
{
    public Task<AppFocusEvent?> GetFocusedAppAsync(CancellationToken cancellationToken)
    {
        var foreground = WindowsInput.GetForegroundApp();
        if (foreground is null || string.IsNullOrWhiteSpace(foreground.AppName))
        {
            return Task.FromResult<AppFocusEvent?>(null);
        }

        return Task.FromResult<AppFocusEvent?>(
            new AppFocusEvent(DateTimeOffset.UtcNow, foreground.AppName, foreground.WindowTitle));
    }
}
