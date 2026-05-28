// ============================================================================
// Bootstrap/GameLifetimeScope.cs �?VContainer DI 装配
// ============================================================================

using IronCrown.Application;
using IronCrown.Domain;
using IronCrown.Contracts;
using IronCrown.Infrastructure;
using IronCrown.Simulation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace IronCrown.Bootstrap
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private int initialSeed = 12345;

        protected override void Configure(IContainerBuilder builder)
        {
            // === Domain 单例 ===
            builder.Register<EventBus>(Lifetime.Singleton).As<IEventPublisher>();
            builder.RegisterInstance<IRandom>(new RandomService(initialSeed));
            builder.Register<GameClock>(Lifetime.Singleton).As<ITurnClock>();

            // === Infrastructure 单例 ===
            builder.Register<NewtonsoftConfigRepository>(Lifetime.Singleton).As<IConfigRepository>();
            builder.Register<FileSaveRepository>(Lifetime.Singleton).As<ISaveRepository>();
            builder.Register<UnityAppLogger>(Lifetime.Singleton).As<IAppLogger>();

            // === Application 单例 ===
            builder.Register<ConfigRegistry>(Lifetime.Singleton).As<IConfigRegistry>();
            builder.Register<WorldInitializer>(Lifetime.Singleton);

            // === Simulation 单例 ===
            builder.Register<EconomyResolver>(Lifetime.Singleton);
            builder.Register<PoliticsResolver>(Lifetime.Singleton);
            builder.Register<BattleResolver>(Lifetime.Singleton);
            builder.Register<SupplyResolver>(Lifetime.Singleton);
            builder.Register<AIResolver>(Lifetime.Singleton);
            builder.Register<DiplomacyResolver>(Lifetime.Singleton);
            builder.Register<TurnResolver>(Lifetime.Singleton);

            // === Entry Point ===
            builder.RegisterEntryPoint<GameEntryPoint>();
        }
    }
}

