using Microsoft.Web.WebView2.Core;
using MioKit.Webview2;
using MouseEnhance.Services;
using Ti.Avalonia.Shadcn.Controls;

namespace MouseEnhance.Views;

public partial class MouseEnhanceConfigWindow : ShadWindow
{
    private const string LocalHost = "mouse-enhance-settings.invalid";
    private const string PageAddress = "https://" + LocalHost + "/index.html";
    private MouseEnhancePlugin? _plugin;
    private ExcludeAppPicker? _picker;
    private CursorWrapSettingsJsService? _cursorWrap;
    private ServiceProvider? _bridgeProvider;
    private Task? _initialization;
    private bool _closed;

    public MouseEnhanceConfigWindow()
    {
        InitializeComponent();
        Opened += (_, _) => _initialization ??= InitializeSettingsAsync();
        Closed += OnClosed;
    }

    private async Task InitializeSettingsAsync()
    {
        try
        {
            var context = MouseEnhanceRegister.Instance.ComponentContext;
            _plugin = context.Resolve<MouseEnhancePlugin>();
            var folder = Path.Combine(_plugin.Context.PluginPath, "ui", "dist");
            if (!File.Exists(Path.Combine(folder, "index.html")))
                throw new FileNotFoundException("插件包缺少设置页资源。开发时请先构建 plugin/vue-ui。");

            await SettingsWebView.EnsureCoreWebView2Async();
            if (_closed)
                return;

            var web = SettingsWebView.CoreWebView2!;
            web.SetVirtualHostNameToFolderMapping(LocalHost, folder, CoreWebView2HostResourceAccessKind.Deny);
            web.Settings.AreHostObjectsAllowed = false;
            ulong? settingsNavigation = null;
            web.NavigationStarting += (_, args) =>
            {
                args.Cancel = !Uri.TryCreate(args.Uri, UriKind.Absolute, out var target) ||
                              target.GetLeftPart(UriPartial.Query) != PageAddress;
                if (!args.Cancel)
                    settingsNavigation = args.NavigationId;
            };
            web.FrameNavigationStarting += (_, args) => args.Cancel = true;
            web.NavigationCompleted += (_, args) =>
            {
                if (!_closed && args.NavigationId == settingsNavigation && !args.IsSuccess)
                    ShowLoadError($"页面导航失败（{args.WebErrorStatus}）。");
            };

            _picker = context.Resolve<ExcludeAppPicker>();
            _cursorWrap = context.Resolve<CursorWrapSettingsJsService>();
            var services = new ServiceCollection();
            services.AddSingleton(context.Resolve<MouseEnhanceSettingsJsService>(
                new TypedParameter(typeof(Window), this), new TypedParameter(typeof(ExcludeAppPicker), _picker)));
            services.AddSingleton(context.Resolve<FindMouseSettingsJsService>());
            services.AddSingleton(_cursorWrap);
            _bridgeProvider = services.BuildServiceProvider();
            await SettingsWebView.InitializeBridgeAsync(services, _bridgeProvider);
            if (!_closed)
                web.Navigate(PageAddress);
        }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception error)
        {
            _plugin?.Context.Logger.Warning(error, "Could not initialize Mouse Enhance settings");
            if (!_closed)
                ShowLoadError(error is WebView2RuntimeNotFoundException
                    ? "请安装 Microsoft Edge WebView2 Runtime 后重新打开。"
                    : error.Message);
        }
    }

    private async void OnClosed(object? sender, EventArgs args)
    {
        _closed = true;
        _picker?.Cancel();
        if (_cursorWrap is IDisposable cursorWrap)
            cursorWrap.Dispose();
        try
        {
            await SettingsWebView.StopAsync();
            if (_initialization is not null)
                await _initialization;
        }
        catch (Exception error)
        {
            _plugin?.Context.Logger.Warning(error, "Could not stop Mouse Enhance settings");
        }
        finally
        {
            _bridgeProvider?.Dispose();
        }
    }

    private void ShowLoadError(string message)
    {
        SettingsWebView.IsVisible = false;
        LoadError.Text = $"鼠标增强设置加载失败。{Environment.NewLine}{message}";
        LoadError.IsVisible = true;
    }
}

public sealed class MouseEnhanceSettingsWebView : MioWebview2
{
    protected override void HandleNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e) =>
        e.Handled = true;
}
