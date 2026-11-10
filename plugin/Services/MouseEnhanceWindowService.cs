using Avalonia.Threading;
using MouseEnhance.Views;

namespace MouseEnhance.Services;

public sealed class MouseEnhanceWindowService
{
    public Task ShowConfigWindowAsync()
    {
        Dispatcher.UIThread.Post(() =>
            _ = WindowManager.ShowUniqueWindow<MouseEnhanceConfigWindow>(
                MouseEnhanceConst.PluginId,
                MouseEnhanceConst.PluginId));
        return Task.CompletedTask;
    }
}
