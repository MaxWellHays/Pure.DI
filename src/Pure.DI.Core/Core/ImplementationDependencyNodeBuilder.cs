﻿// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace Pure.DI.Core;

internal sealed class ImplementationDependencyNodeBuilder(
    ILogger<ImplementationDependencyNodeBuilder> logger,
    IBuilder<DpImplementation, IEnumerable<DpImplementation>> implementationVariantsBuilder)
    : IBuilder<MdSetup, IEnumerable<DependencyNode>>
{
    public IEnumerable<DependencyNode> Build(MdSetup setup)
    {
        var injectionsWalker = new DependenciesToInjectionsCountWalker();
        foreach (var binding in setup.Bindings)
        {
            if (binding.Implementation is not { } implementation)
            {
                continue;
            }

            if (implementation.Type is not INamedTypeSymbol implementationType)
            {
                yield break;
            }

            if (implementationType.IsAbstract)
            {
                throw new CompileErrorException($"The {implementationType} cannot be constructed due to being an abstract type.", implementation.Source.GetLocation(), LogId.ErrorInvalidMetadata);
            }

            var compilation = binding.SemanticModel.Compilation;
            var constructors = new List<DpMethod>();
            var hasExplicitlyDeclaredNonStaticCtor = implementationType.Constructors.Any(i => !i.IsImplicitlyDeclared && !i.IsStatic);
            foreach (var constructor in implementationType.Constructors)
            {
                if (hasExplicitlyDeclaredNonStaticCtor && constructor.IsImplicitlyDeclared)
                {
                    continue;
                }
                
                if (constructor.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.Public or Accessibility.Friend))
                {
                    continue;
                }

                constructors.Add(
                    new DpMethod(
                        constructor,
                        GetAttribute(setup.OrdinalAttributes, constructor, default(int?)),
                        GetParameters(setup, constructor.Parameters, compilation, setup.TypeConstructor)));
            }

            if (!constructors.Any())
            {
                throw new CompileErrorException($"The instance of {implementationType} cannot be instantiated due to no accessible constructor available.", implementation.Source.GetLocation(), LogId.ErrorInvalidMetadata);
            }

            var methods = new List<DpMethod>();
            var fields = new List<DpField>();
            var properties = new List<DpProperty>();
            foreach (var member in GetMembers(implementationType))
            {
                if (member.IsStatic || member.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.Public or Accessibility.Friend))
                {
                    continue;
                }

                switch (member)
                {
                    case IMethodSymbol method:
                        if (method.MethodKind == MethodKind.Ordinary)
                        {
                            var ordinal = GetAttribute(setup.OrdinalAttributes, member, default(int?));
                            if (ordinal.HasValue)
                            {
                                methods.Add(new DpMethod(method, ordinal.Value, GetParameters(setup, method.Parameters, compilation, setup.TypeConstructor)));
                            }
                        }

                        break;

                    case IFieldSymbol field:
                        if (field is { IsReadOnly: false, IsStatic: false, IsConst: false })
                        {
                            var type = field.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                            var ordinal = GetAttribute(setup.OrdinalAttributes, member, default(int?));
                            if (ordinal.HasValue || field.IsRequired)
                            {
                                fields.Add(
                                    new DpField(
                                        field,
                                        ordinal,
                                        new Injection(
                                            GetAttribute(setup.TypeAttributes, field, setup.TypeConstructor?.Construct(compilation, type) ?? type),
                                            GetAttribute(setup.TagAttributes, field, default(object?)))));
                            }
                        }

                        break;

                    case IPropertySymbol property:
                        if (property is { IsReadOnly: false, IsStatic: false, IsIndexer: false })
                        {
                            var type = property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
                            var ordinal = GetAttribute(setup.OrdinalAttributes, member, default(int?));
                            if (ordinal.HasValue || property.IsRequired)
                            {
                                properties.Add(
                                    new DpProperty(
                                        property,
                                        ordinal,
                                        new Injection(
                                            GetAttribute(setup.TypeAttributes, property, setup.TypeConstructor?.Construct(compilation, type) ?? type),
                                            GetAttribute(setup.TagAttributes, property, default(object?)))));
                            }
                        }

                        break;
                }
            }

            var methodsArray = methods.ToImmutableArray();
            var propertiesArray = properties.ToImmutableArray();
            var fieldsArray = fields.ToImmutableArray();
            var implementations = constructors
                .Select(constructor => 
                    new DpImplementation(
                        implementation,
                        binding,
                        constructor,
                        methodsArray,
                        propertiesArray,
                        fieldsArray))
                .ToArray();

            var implementationsWithOrdinal = implementations
                .Where(i => i.Constructor.Ordinal.HasValue)
                .ToArray();

            if (implementationsWithOrdinal.Any())
            {
                foreach (var node in CreateNodes(injectionsWalker, implementationsWithOrdinal.OrderBy(i => i.Constructor.Ordinal)))
                {
                    yield return node;
                }
                
                continue;
            }

            foreach (var node in CreateNodes(injectionsWalker, implementations))
            {
                yield return node;
            }
        }
    }

    private IEnumerable<DependencyNode> CreateNodes(DependenciesToInjectionsCountWalker walker, IEnumerable<DpImplementation> implementations) =>
        implementations
            .OrderByDescending(i => GetInjectionsCount(walker, i.Constructor))
            .ThenByDescending(i => i.Constructor.Method.DeclaredAccessibility)
            .SelectMany(implementationVariantsBuilder.Build)
            .Select((implementation, variantId) => new DependencyNode(variantId, implementation.Binding, Implementation: implementation));

    private static int GetInjectionsCount(DependenciesToInjectionsCountWalker walker, in DpMethod constructor)
    {
        walker.VisitConstructor(Unit.Shared, constructor);
        return walker.Count;
    }

    private static IEnumerable<ISymbol> GetMembers(ITypeSymbol type)
    {
        var members = new List<ISymbol>();
        while (true)
        {
            members.AddRange(type.GetMembers());
            if (type.BaseType is { } baseType)
            {
                type = baseType;
                continue;
            }

            break;
        }

        return members;
    }

    private ImmutableArray<DpParameter> GetParameters(
        in MdSetup setup,
        in ImmutableArray<IParameterSymbol> parameters,
        Compilation compilation,
        ITypeConstructor? typeConstructor)
    {
        if (parameters.Length == 0)
        {
            return ImmutableArray<DpParameter>.Empty;
        }
        
        var dependenciesBuilder = ImmutableArray.CreateBuilder<DpParameter>(parameters.Length);
        foreach (var parameter in parameters)
        {
            var type = parameter.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            dependenciesBuilder.Add(
                new DpParameter(
                    parameter,
                    new Injection(
                        GetAttribute(setup.TypeAttributes, parameter, typeConstructor?.Construct(compilation, type) ?? type),
                        GetAttribute(setup.TagAttributes, parameter, default(object?)))));
        }

        return dependenciesBuilder.MoveToImmutable();
    }
    
    private T GetAttribute<TMdAttribute, T>(
        in ImmutableArray<TMdAttribute> attributeMetadata,
        ISymbol member,
        T defaultValue)
        where TMdAttribute: IMdAttribute
    {
        foreach (var attribute in attributeMetadata)
        {
            var attributeData = member.GetAttributes(attribute.AttributeType);
            switch (attributeData.Count)
            {
                case 1:
                    var args = attributeData[0].ConstructorArguments;
                    if (attribute.ArgumentPosition > args.Length)
                    {
                        logger.CompileError($"The argument position {attribute.ArgumentPosition.ToString()} of attribute {attribute.Source} is out of range [0..{args.Length.ToString()}].", attribute.Source.GetLocation(), LogId.ErrorInvalidMetadata);
                    }

                    var typedConstant = args[attribute.ArgumentPosition];
                    if (typedConstant.Value is T value)
                    {
                        return value;
                    }

                    break;

                case > 1:
                    throw new CompileErrorException($"{member} of the type {member.ContainingType} cannot be processed because it is marked with multiple mutually exclusive attributes.", attribute.Source.GetLocation(), LogId.ErrorInvalidMetadata);                 
            }
        }

        return defaultValue;
    }

    private sealed class DependenciesToInjectionsCountWalker: DependenciesWalker<Unit>
    {
        public int Count { get; private set; }

        public override void VisitConstructor(in Unit ctx, in DpMethod constructor)
        {
            Count = 0;
            base.VisitConstructor(in ctx, in constructor);
        }

        public override void VisitInjection(
            in Unit ctx,
            in Injection injection,
            bool hasExplicitDefaultValue,
            object? explicitDefaultValue,
            in ImmutableArray<Location> locations) => Count++;
    }
}