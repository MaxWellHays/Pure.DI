/*
$v=true
$p=5
$d=Arguments
$h=Sometimes you need to pass some state to a composition class to use it when resolving dependencies. To do this, just use the `Arg<T>(string argName)` method, specify the type of argument and its name. You can also specify a tag for each argument. You can then use them as dependencies when building the object graph. If you have multiple arguments of the same type, just use tags to distinguish them. The values of the arguments are manipulated when you create a composition class by calling its constructor. It is important to remember that only those arguments that are used in the object graph will appear in the constructor. Arguments that are not involved will not be added to the constructor arguments.
*/

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable CheckNamespace
// ReSharper disable UnusedParameter.Local
// ReSharper disable ArrangeTypeModifiers
namespace Pure.DI.UsageTests.Basics.ArgumentsScenario2;

using Shouldly;
using Xunit;

interface IExternalDependency { }
class ExternalDependency : IExternalDependency { }

interface IService1
{
    IExternalDependency ExternalDependency { get; }
}

class Service1(IExternalDependency externalDependency) : IService1
{
    public IExternalDependency ExternalDependency { get; } = externalDependency;
}

interface IService2
{
    IExternalDependency ExternalDependency { get; }
}

class Service2(IExternalDependency externalDependency) : IService2
{
    public IExternalDependency ExternalDependency { get; } = externalDependency;
}

interface IRootService
{
    IService1 Service1 { get; }
    IService2 Service2 { get; }
}

class RootService(IService1 service1, IService2 service2) : IRootService
{
    public IService1 Service1 { get; } = service1;
    public IService2 Service2 { get; } = service2;
}

public class Scenario
{
    [Fact]
    public void Run()
    {
        DI.Setup("Composition")
            .Bind<IService1>().As(Lifetime.Singleton).To<Service1>()
            .Bind<IService2>().As(Lifetime.Singleton).To<Service2>()
            .Bind<IRootService>().As(Lifetime.Singleton).To<RootService>()
            .Arg<IExternalDependency>("externalDependency")
            .Root<IService1>("Service1")
            .Root<IRootService>("Root");

        var externalDependency = new ExternalDependency();
        var composition = new Composition(externalDependency);
        
        composition.Service1.ExternalDependency.ShouldBeEquivalentTo(externalDependency);
        composition.Root.Service1.ExternalDependency.ShouldBeEquivalentTo(externalDependency);
        composition.Root.Service2.ExternalDependency.ShouldBeEquivalentTo(externalDependency);
    }
}