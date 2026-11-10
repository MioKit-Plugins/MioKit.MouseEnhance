using MouseEnhance.Nodes;
using MouseEnhance.Services;
using MouseEnhance.Views;

namespace MouseEnhance;

public class MouseEnhanceRegister : RegisterBase<MouseEnhanceRegister>
{
    public override void RegisterService(ContainerBuilder builder, IServiceCollection services)
    {
        var plugin = new MouseEnhancePlugin();

        builder.RegisterInstance(plugin)
            .As<IPlugin>()
            .Keyed<IPlugin>(MouseEnhanceConst.PluginId)
            .As(plugin.GetType());

        builder.RegisterType<MouseEnhanceSearchGroup>().AsSelf().InstancePerDependency();
        builder.RegisterType<StartMouseEnhanceNode>().AsSelf().InstancePerDependency();
        builder.RegisterType<StopMouseEnhanceNode>().AsSelf().InstancePerDependency();

        builder.Register(ctx => new ModifierKeyInjector(plugin.Context.Logger))
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<WindowDragCore>()
            .AsSelf()
            .SingleInstance();

        builder.Register(ctx => new MouseEnhanceService(
                plugin,
                ctx.Resolve<WindowDragCore>(),
                ctx.Resolve<IMouseHook>(),
                ctx.Resolve<IKeyboardHook>(),
                plugin.Context.Logger))
            .AsSelf()
            .As<IPluginLifecycleComponent>()
            .SingleInstance();

        builder.RegisterType<MouseEnhanceWindowService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<MouseEnhanceConfigViewModel>()
            .AsSelf();
    }
}
