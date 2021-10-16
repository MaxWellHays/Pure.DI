﻿namespace Pure.DI.UsageScenarios.Tests
{
    using Shouldly;
    using Xunit;

    public class CompositionRoot
    {
        [Fact]
        public void Run()
        {
            // $visible=true
            // $tag=1 Basics
            // $priority=00
            // $description=Composition Root
            // $header=This sample demonstrates the most efficient way of getting a composition root object, free from any impact on memory consumption and performance. Each ordinary binding type has its method to resolve a related instance as a composition root object.
            // {
            DI.Setup("Composer")
                .Bind<IDependency>().To<Dependency>()
                .Bind<IService>().To<Service>();

            // Resolve an instance of interface `IService`
            var instance = Composer.ResolveIService();
            // }
            instance.ShouldBeOfType<Service>();
        }
    }
}