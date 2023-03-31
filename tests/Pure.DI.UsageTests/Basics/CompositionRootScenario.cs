﻿/*
$v=true
$p=0
$d=Composition Root
$h=This example demonstrates the most efficient way to get the root object of a composition without impacting memory consumption or performance.
$f=Actually, the property _Root_ looks like:
$f=```csharp
$f=public Pure.DI.UsageTests.CompositionRoot.IService Root
$f={
$f=  get
$f=  {
$f=    return new Service(new Dependency());
$f=  }
$f=}
$f=``` 
*/

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable CheckNamespace
namespace Pure.DI.UsageTests.Basics.CompositionRootScenario;

using Shouldly;
using Xunit;

// {
internal interface IDependency { }

internal class Dependency : IDependency { }

internal interface IService { }

internal class Service : IService
{
    public Service(IDependency dependency)
    {
    }
}
// }

public class CompositionRootScenario
{
    [Fact]
    public void Run()
    {
// {            
        DI.Setup("Composition")
            .Bind<IDependency>().To<Dependency>()
            .Bind<IService>().To<Service>()
            .Root<IService>("Root");

        var composition = new Composition();
        var service = composition.Root;
// }            
        service.ShouldBeOfType<Service>();
    }
}