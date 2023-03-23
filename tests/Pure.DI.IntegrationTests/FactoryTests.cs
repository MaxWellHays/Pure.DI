﻿namespace Pure.DI.IntegrationTests;

using System.Collections.Immutable;

[Collection(nameof(NonParallelTestsCollectionDefinition))]
public class FactoryTests
{
    [Fact]
    public async Task ShouldSupportFactory()
    {
        // Given

        // When
        var result = await """
using System;
using Pure.DI;

namespace Sample
{
    interface IDependency {}

    class Dependency: IDependency {}

    interface IService
    {
        IDependency Dep { get; }
    }

    class Service: IService 
    {
        public Service(IDependency dep)
        { 
            Dep = dep;           
        }

        public IDependency Dep { get; }
    }

    static class Setup
    {
        private static void SetupComposition()
        {
            DI.Setup("Composition")
                .Bind<IDependency>().To(ctx => new Dependency())
                .Bind<IService>().To<Service>()    
                .Root<IService>("Service");
        }
    }

    public class Program
    {
        public static void Main()
        {
            var composition = new Composition();
            var service1 = composition.Service;
            var service2 = composition.Service;
            Console.WriteLine(service1.Dep != service2.Dep);                                            
        }
    }                
}
""".RunAsync();

        // Then
        result.Success.ShouldBeTrue(result.GeneratedCode);
        result.StdOut.ShouldBe(ImmutableArray.Create("True"), result.GeneratedCode);
    }
    
    [Fact]
    public async Task ShouldSupportFactoryWhenBlock()
    {
        // Given

        // When
        var result = await """
using System;
using Pure.DI;

namespace Sample
{
    interface IDependency {}

    class Dependency: IDependency {}

    interface IService
    {
        IDependency Dep { get; }
    }

    class Service: IService 
    {
        public Service(IDependency dep)
        { 
            Dep = dep;           
        }

        public IDependency Dep { get; }
    }

    static class Setup
    {
        private static void SetupComposition()
        {
            DI.Setup("Composition")
                .Bind<IDependency>().To(ctx => 
                    { 
                        return new Dependency(); 
                    }
                )
                .Bind<IService>().To<Service>()    
                .Root<IService>("Service");
        }
    }

    public class Program
    {
        public static void Main()
        {
            var composition = new Composition();
            var service1 = composition.Service;
            var service2 = composition.Service;
            Console.WriteLine(service1.Dep != service2.Dep);                                            
        }
    }                
}
""".RunAsync();

        // Then
        result.Success.ShouldBeTrue(result.GeneratedCode);
        result.StdOut.ShouldBe(ImmutableArray.Create("True"), result.GeneratedCode);
    }
    
    [Fact]
    public async Task ShouldSupportFactoryWhenSingleton()
    {
        // Given

        // When
        var result = await """
using System;
using Pure.DI;

namespace Sample
{
    interface IDependency {}

    class Dependency: IDependency {}

    interface IService
    {
        IDependency Dep { get; }
    }

    class Service: IService 
    {
        public Service(IDependency dep)
        { 
            Dep = dep;           
        }

        public IDependency Dep { get; }
    }

    static class Setup
    {
        private static void SetupComposition()
        {
            DI.Setup("Composition")
                .Bind<IDependency>().As(Lifetime.Singleton).To(ctx => new Dependency())
                .Bind<IService>().To<Service>()    
                .Root<IService>("Service");
        }
    }

    public class Program
    {
        public static void Main()
        {
            var composition = new Composition();
            var service1 = composition.Service;
            var service2 = composition.Service;
            Console.WriteLine(service1.Dep == service2.Dep);                                            
        }
    }                
}
""".RunAsync();

        // Then
        result.Success.ShouldBeTrue(result.GeneratedCode);
        result.StdOut.ShouldBe(ImmutableArray.Create("True"), result.GeneratedCode);
    }
    
    [Fact]
    public async Task ShouldSupportFactoryWithInject()
    {
        // Given

        // When
        var result = await """
using System;
using Pure.DI;

namespace Sample
{
    interface IDependency {}

    class Dependency: IDependency {}

    interface IService
    {
        IDependency Dep { get; }
    }

    class Service: IService 
    {
        public Service(IDependency dep)
        { 
            Dep = dep;           
        }

        public IDependency Dep { get; }
    }

    static class Setup
    {
        private static void SetupComposition()
        {
            DI.Setup("Composition")
                .Bind<IDependency>().To(ctx => new Dependency())
                .Bind<IService>().To(ctx => {
                    IDependency dependency1;
                    ctx.Inject<IDependency>(out dependency1); new Service(dependency1);
                    ctx.Inject<IDependency>(out var dependency2); return new Service(dependency2);
                })    
                .Root<IService>("Service");
        }
    }

    public class Program
    {
        public static void Main()
        {
            var composition = new Composition();
            var service1 = composition.Service;
            var service2 = composition.Service;
            Console.WriteLine(service1.Dep != service2.Dep);                                            
        }
    }                
}
""".RunAsync();

        // Then
        result.Success.ShouldBeTrue(result.GeneratedCode);
        result.StdOut.ShouldBe(ImmutableArray.Create("True"), result.GeneratedCode);
    }
    
    [Fact]
    public async Task ShouldSupportFactoryWithParentTagInjection()
    {
        // Given

        // When
        var result = await """
using System;
using Pure.DI;

namespace Sample
{
    interface IDependency {}

    class Dependency: IDependency {}

    interface IService
    {
        IDependency Dep { get; }
    }

    class Service: IService 
    {
        public Service(IDependency dep)
        { 
            Dep = dep;
            Console.WriteLine("Created");           
        }

        public IDependency Dep { get; }
    }

    static class Setup
    {
        private static void SetupComposition()
        {
            DI.Setup("Composition")
                .Bind<IDependency>(123).To(ctx => new Dependency())
                .Bind<IService>(123).To(ctx => {
                    ctx.Inject<IDependency>(ctx.Tag, out var dependency);
                    return new Service(dependency);
                })    
                .Root<IService>("Service", 123);
        }
    }

    public class Program
    {
        public static void Main()
        {
            var composition = new Composition();      
            var service = composition.Service;                                                 
        }
    }                
}
""".RunAsync();

        // Then
        result.Success.ShouldBeTrue(result.GeneratedCode);
        result.StdOut.ShouldBe(ImmutableArray.Create("Created"), result.GeneratedCode);
    }
}