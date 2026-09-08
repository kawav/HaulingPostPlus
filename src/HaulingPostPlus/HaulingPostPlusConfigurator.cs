using Bindito.Core;
using Timberborn.EntityPanelSystem;

namespace HaulingPostPlus;

[Context("Game")]
public sealed class HaulingPostPlusConfigurator : Configurator
{
    protected override void Configure()
    {
        // Retry optional discovery after all enabled mod assemblies are loaded.
        // Installation is idempotent per panel, not a single global boolean.
        WorkplacePortraitPatch.Install();
        Bind<HaulingPostFragment>().AsSingleton();
        MultiBind<EntityPanelModule>().ToProvider<PanelModuleProvider>().AsSingleton();
    }

    private sealed class PanelModuleProvider : IProvider<EntityPanelModule>
    {
        private readonly HaulingPostFragment _fragment;
        public PanelModuleProvider(HaulingPostFragment fragment) => _fragment = fragment;

        public EntityPanelModule Get()
        {
            var builder = new EntityPanelModule.Builder();
            builder.AddTopFragment(_fragment, 10);
            return builder.Build();
        }
    }
}
