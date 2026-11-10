using Avalonia.Controls;
using Avalonia.Interactivity;
using Ti.Avalonia.Shadcn.Controls;

namespace MouseEnhance.Views;

public partial class MouseEnhanceConfigWindow : ShadWindow
{
    private MouseEnhanceConfigViewModel? _viewModel;

    public MouseEnhanceConfigWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            _viewModel = MouseEnhanceRegister.Instance.ComponentContext.Resolve<MouseEnhanceConfigViewModel>();
            _viewModel.Window = this;
            DataContext = _viewModel;
            _viewModel.LoadFromPlugin();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _viewModel?.LoadFromPlugin();
    }
}
