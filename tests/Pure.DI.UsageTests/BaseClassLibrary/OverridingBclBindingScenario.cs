﻿/*
$v=true
$p=100
$d=Overriding the BCL binding
$h=At any time, the default binding to the BCL type can be changed to your own:
*/

// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeTypeModifiers
namespace Pure.DI.UsageTests.BCL.OverridingBclBindingScenario;

using Shouldly;
using Xunit;

// {
interface IDependency { }

class AbcDependency : IDependency { }

class XyzDependency : IDependency { }

interface IService
{
    IDependency[] Dependencies { get; }
}

class Service : IService
{
    public Service(IDependency[] dependencies) => 
        Dependencies = dependencies;

    public IDependency[] Dependencies { get; }
}
// }

public class Scenario
{
    [Fact]
    public void Run()
    {
// {            
        DI.Setup("Composition")
            .Bind<IDependency[]>().To(_ => new IDependency[]
            {
                new AbcDependency(),
                new XyzDependency(),
                new AbcDependency()
            })
            .Bind<IService>().To<Service>().Root<IService>("Root");

        var composition = new Composition();
        var service = composition.Root;
        service.Dependencies.Length.ShouldBe(3);
        service.Dependencies[0].ShouldBeOfType<AbcDependency>();
        service.Dependencies[1].ShouldBeOfType<XyzDependency>();
        service.Dependencies[2].ShouldBeOfType<AbcDependency>();
// }            
        composition.SaveClassDiagram();
    }
}