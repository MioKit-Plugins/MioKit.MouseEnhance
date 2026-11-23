using MouseEnhance.Nodes;
using MouseEnhance.Services;

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
        builder.RegisterType<StartCursorWrapNode>().AsSelf().InstancePerDependency();
        builder.RegisterType<StopCursorWrapNode>().AsSelf().InstancePerDependency();

        builder.Register(ctx => new ModifierKeyInjector(plugin.Context.Logger))
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<WindowDragCore>()
            .AsSelf()
            .SingleInstance();

        builder.Register(_ => new FindMouseSpotlight(error =>
                plugin.Context.Logger.Warning(error, "Find Mouse spotlight failed")))
            .SingleInstance();
        builder.Register(ctx => new FindMouseController(
                ctx.Resolve<FindMouseSpotlight>(), plugin.Context.Logger))
            .SingleInstance();
        builder.Register(ctx => new FindMouseSettingsJsService(plugin, ctx.Resolve<FindMouseController>()))
            .SingleInstance();

        builder.RegisterType<CursorWrapController>().SingleInstance();
        builder.RegisterType<CursorWrapSettingsJsService>().ExternallyOwned();

        builder.Register(ctx => new MouseEnhanceService(
                plugin,
                ctx.Resolve<WindowDragCore>(),
                ctx.Resolve<IMouseHook>(),
                ctx.Resolve<IKeyboardHook>(),
                ctx.Resolve<FindMouseController>(),
                ctx.Resolve<CursorWrapController>(),
                plugin.Context.Logger))
            .AsSelf()
            .As<IPluginLifecycleComponent>()
            .SingleInstance();

        builder.RegisterType<MouseEnhanceWindowService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<ExcludeAppPicker>();
        builder.RegisterType<MouseEnhanceSettingsJsService>();
    }
}
