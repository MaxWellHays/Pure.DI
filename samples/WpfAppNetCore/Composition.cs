// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable RedundantNameQualifier
namespace WpfAppNetCore;

using Pure.DI;
using static Pure.DI.Lifetime;

internal partial class Composition
{
    private static void Setup() => DI.Setup(nameof(Composition))
        // Provides the composition root for clock view model
        .Root<IClockViewModel>("ClockViewModel")
        
        // View Models
        .Bind<IClockViewModel>().As(Singleton).To<ClockViewModel>()

        // Models
        .Bind<ILog<TT>>().To<Log<TT>>()
        .Bind<TimeSpan>().To(_ => TimeSpan.FromSeconds(1))
        .Bind<Clock.Models.ITimer>().As(Singleton).To<Clock.Models.Timer>()
        .Bind<IClock>().As(PerBlock).To<SystemClock>()
    
        // Infrastructure
        .Bind<IDispatcher>().To<Dispatcher>();
}