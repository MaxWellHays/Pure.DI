// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable HeapView.BoxingAllocation
// ReSharper disable ClassNeverInstantiated.Global
namespace Pure.DI.Core.Code;

internal sealed class ClassDiagramBuilder: IBuilder<CompositionCode, LinesBuilder>
{
    private readonly IBuilder<ContractsBuildContext, ISet<Injection>> _injectionsBuilder;
    private readonly CancellationToken _cancellationToken;
    private static readonly FormatOptions DefaultFormatOptions = new();
    
    public ClassDiagramBuilder(
        IBuilder<ContractsBuildContext, ISet<Injection>> injectionsBuilder,
        CancellationToken cancellationToken)
    {
        _injectionsBuilder = injectionsBuilder;
        _cancellationToken = cancellationToken;
    }

    public LinesBuilder Build(CompositionCode composition)
    {
        var lines = new LinesBuilder();
        lines.AppendLine("classDiagram");
        using (lines.Indent())
        {
            var hasResolveMethods = composition.Source.Source.Hints.GetHint(Hint.Resolve, SettingState.On) == SettingState.On;
            var rootProperties = composition.Roots.ToDictionary(i => i.Injection, i => i);
            if (hasResolveMethods || rootProperties.Any())
            {
                lines.AppendLine($"class {composition.Source.Source.Name.ClassName} {{");
                using (lines.Indent())
                {
                    foreach (var root in composition.Roots.OrderByDescending(i => i.IsPublic).ThenBy(i => i.Name))
                    {
                        var rootArgsStr = "";
                        if (!root.Args.IsEmpty)
                        {
                            rootArgsStr = $"({string.Join(", ", root.Args.Select(arg => $"{arg.InstanceType} {arg.VarName}"))})";
                        }

                        lines.AppendLine($"{(root.IsPublic ? "+" : "-")}{FormatType(root.Injection.Type, DefaultFormatOptions)} {root.PropertyName}{rootArgsStr}");
                    }
                    
                    if (hasResolveMethods)
                    {
                        var hints = composition.Source.Source.Hints;
                        var genericParameterT = $"{DefaultFormatOptions.StartGenericArgsSymbol}T{DefaultFormatOptions.FinishGenericArgsSymbol}";
                        lines.AppendLine($"{(hints.GetValueOrDefault(Hint.ResolveMethodModifiers, "") == "" ? "+ " : "")}T {hints.GetValueOrDefault(Hint.ResolveMethodName, Names.ResolverMethodName)}{genericParameterT}()");
                        lines.AppendLine($"{(hints.GetValueOrDefault(Hint.ResolveByTagMethodModifiers, "") == "" ? "+ " : "")}T {hints.GetValueOrDefault(Hint.ResolveByTagMethodName, Names.ResolverMethodName)}{genericParameterT}(object? tag)");
                        lines.AppendLine($"{(hints.GetValueOrDefault(Hint.ObjectResolveMethodModifiers, "") == "" ? "+ " : "")}object {hints.GetValueOrDefault(Hint.ObjectResolveMethodName, Names.ResolverMethodName)}(Type type)");
                        lines.AppendLine($"{(hints.GetValueOrDefault(Hint.ObjectResolveByTagMethodModifiers, "") == "" ? "+ " : "")}object {hints.GetValueOrDefault(Hint.ObjectResolveByTagMethodName, Names.ResolverMethodName)}(Type type, object? tag)");
                    }
                }

                lines.AppendLine("}");
            }
            else
            {
                lines.AppendLine($"class {composition.Source.Source.Name.ClassName}");
            }

            if (composition.DisposableSingletonsCount > 0)
            {
                lines.AppendLine($"{composition.Source.Source.Name.ClassName} --|> IDisposable");
            }

            var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var graph = composition.Source.Graph;
            foreach (var node in graph.Vertices.GroupBy(i => i.Type, SymbolEqualityComparer.Default).Select(i => i.First()))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (node.Root is not null)
                {
                    continue;
                }
                
                var contracts = _injectionsBuilder.Build(new ContractsBuildContext(node.Binding, MdTag.ContextTag));
                foreach (var contract in contracts)
                {
                    if (node.Type.Equals(contract.Type, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    types.Add(contract.Type);
                    lines.AppendLine($"{FormatType(node.Type, DefaultFormatOptions)} --|> {FormatType(contract.Type, DefaultFormatOptions)} : {FormatTag(contract.Tag)}");
                }

                var classDiagramWalker = new ClassDiagramWalker(lines, DefaultFormatOptions);
                classDiagramWalker.VisitDependencyNode(Unit.Shared, node);
            }

            foreach (var type in types)
            {
                var typeKind = "";
                if (type.IsRecord)
                {
                    typeKind = "record";
                }
                else
                {
                    if (type.IsTupleType)
                    {
                        typeKind = "tuple";
                    }
                    else
                    {
                        if (type.IsAbstract)
                        {
                            typeKind = "abstract";
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(typeKind))
                {
                    continue;
                }

                lines.AppendLine($"class {FormatType(type, DefaultFormatOptions)} {{");
                using (lines.Indent())
                {
                    lines.AppendLine($"<<{typeKind}>>");    
                }
                lines.AppendLine("}");
            }
            
            foreach (var dependency in graph.Edges)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (dependency.Target.Root is not null && rootProperties.TryGetValue(dependency.Injection, out var root))
                {
                    lines.AppendLine($"{composition.Source.Source.Name.ClassName} ..> {FormatType(dependency.Source.Type, DefaultFormatOptions)} : {FormatInjection(root.Injection, DefaultFormatOptions)} {root.PropertyName}");
                }
                else
                {
                    if (dependency.Source.Arg is { } arg)
                    {
                        var tags = arg.Binding.Contracts.SelectMany(i => i.Tags.Select(tag => tag.Value)).ToArray();
                        lines.AppendLine($"{FormatType(dependency.Target.Type, DefaultFormatOptions)} o-- {FormatType(dependency.Source.Type, DefaultFormatOptions)} : {(tags.Any() ? FormatTags(tags) + " " : "")}Argument \\\"{arg.Source.ArgName}\\\"");
                    }
                    else
                    {
                        if (SymbolEqualityComparer.Default.Equals(dependency.Source.Type, dependency.Target.Type))
                        {
                            continue;
                        }

                        var relationship = dependency.Source.Lifetime == Lifetime.Transient ? "*--" : "o--";
                        lines.AppendLine($"{FormatType(dependency.Target.Type, DefaultFormatOptions)} {relationship} {FormatCardinality(dependency.Source.Lifetime)} {FormatType(dependency.Source.Type, DefaultFormatOptions)} : {FormatDependency(dependency, DefaultFormatOptions)}");   
                    }
                }
            }
        }
        
        return lines;
    }

    private static string FormatCardinality(Lifetime lifetime) =>
        lifetime switch
        {
            Lifetime.Transient => "",
            _ => $" \\\"{lifetime}\\\""
        };

    private static string FormatInjection(Injection injection, FormatOptions options) => 
        $"{FormatTag(injection.Tag)}{FormatSymbol(injection.Type, options)}";

    private static string FormatDependency(Dependency dependency, FormatOptions options) => 
        $"{(dependency.Injection.Tag == default ? "" : FormatTag(dependency.Injection.Tag) + " ")}{FormatSymbol(dependency.Injection.Type, options)}";

    private static string FormatTag(object? tag) =>
        tag != default ? $"{tag.ValueToString("").Replace("\"", "\\\"")} " : "";
    
    private static string FormatTags(IEnumerable<object?> tags) =>
        string.Join(", ", tags.Distinct().Select(FormatTag).OrderBy(i => i));

    private static string FormatSymbol(ISymbol? symbol, FormatOptions options) =>
        symbol switch
        {
            IParameterSymbol parameter => FormatParameter(parameter, options),
            IPropertySymbol property => FormatProperty(property, options),
            IFieldSymbol field => FormatField(field, options),
            IMethodSymbol method => FormatMethod(method, options),
            ITypeSymbol type => FormatType(type, options),
            _ => symbol?.ToString() ?? ""
        };

    private static string FormatMethod(IMethodSymbol method, FormatOptions options)
    {
        if (method is { Kind: SymbolKind.Property, ContainingSymbol: IPropertySymbol property })
        {
            return $"{Format(method.DeclaredAccessibility)}{property.Name} : {FormatType(method.ReturnType, options)}";
        }

        // ReSharper disable once InvertIf
        if (method.MethodKind == MethodKind.Constructor)
        {
            return $"{Format(method.DeclaredAccessibility)}{method.ContainingType.Name}({string.Join(", ", method.Parameters.Select(FormatPropertyLocal))})";
            string FormatPropertyLocal(IParameterSymbol parameter) => $"{FormatType(parameter.Type, options)} {parameter.Name}";
        }

        return $"{Format(method.DeclaredAccessibility)}{method.Name}({string.Join(", ", method.Parameters.Select(FormatParameterLocal))}) : {FormatType(method.ReturnType, options)}";

        string FormatParameterLocal(IParameterSymbol parameter) => $"{FormatType(parameter.Type, options)} {parameter.Name}";
    }

    private static string FormatProperty(IPropertySymbol property, FormatOptions options) =>
        $"{Format(property.GetMethod?.DeclaredAccessibility ?? property.DeclaredAccessibility)}{FormatType(property.Type, options)} {property.Name}";

    private static string FormatField(IFieldSymbol field, FormatOptions options) =>
        $"{Format(field.DeclaredAccessibility)}{FormatType(field.Type, options)} {field.Name}";
    
    private static string FormatParameter(IParameterSymbol parameter, FormatOptions options) =>
        $"{FormatType(parameter.Type, options)} {parameter.Name}";

    private static string FormatType(ISymbol typeSymbol, FormatOptions options)
    {
        return typeSymbol switch
        {
            INamedTypeSymbol { IsGenericType: true } namedTypeSymbol => $"{namedTypeSymbol.Name}{options.StartGenericArgsSymbol}{string.Join(options.TypeArgsSeparator, namedTypeSymbol.TypeArguments.Select(FormatSymbolLocal))}{options.FinishGenericArgsSymbol}",
            IArrayTypeSymbol array => $"Array{options.StartGenericArgsSymbol}{FormatType(array.ElementType, options)}{options.FinishGenericArgsSymbol}",
            _ => typeSymbol.Name
        };
        
        string FormatSymbolLocal(ITypeSymbol i) => FormatSymbol(i, options);
    }

    private static string Format(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.NotApplicable => "",
            Accessibility.Private => "-",
            Accessibility.ProtectedAndInternal => "#",
            Accessibility.Protected => "#",
            Accessibility.Internal => "~",
            Accessibility.ProtectedOrInternal => "~",
            Accessibility.Public => "+",
            _ => ""
        };

    private record FormatOptions(
        string StartGenericArgsSymbol = "ᐸ",
        string FinishGenericArgsSymbol = "ᐳ",
        string TypeArgsSeparator = "ˏ");

    private class ClassDiagramWalker : DependenciesWalker<Unit>
    {
        private readonly LinesBuilder _lines;
        private readonly LinesBuilder _nodeLines = new();
        private readonly FormatOptions _options;

        public ClassDiagramWalker(LinesBuilder lines, FormatOptions options)
        {
            _lines = lines;
            _options = options;
        }

        public override void VisitDependencyNode(in Unit ctx, DependencyNode node)
        {
            base.VisitDependencyNode(ctx, node);
            if (!_nodeLines.Lines.Any())
            {
                _lines.AppendLine($"class {FormatType(node.Type, _options)}");
                return;
            }

            _lines.AppendLine($"class {FormatType(node.Type, _options)} {{");
            using (_lines.Indent())
            {
                _lines.AppendLines(_nodeLines.Lines);
            }
            
            _lines.AppendLine("}");
        }

        public override void VisitConstructor(in Unit ctx, in DpMethod constructor)
        {
            _nodeLines.AppendLine(FormatMethod(constructor.Method, _options));
            base.VisitConstructor(ctx, in constructor);
        }

        public override void VisitMethod(in Unit ctx, in DpMethod method)
        {
            _nodeLines.AppendLine(FormatMethod(method.Method, _options));
            base.VisitMethod(ctx, in method);
        }

        public override void VisitProperty(in Unit ctx, in DpProperty property)
        {
            _nodeLines.AppendLine(FormatProperty(property.Property, _options));
            base.VisitProperty(ctx, in property);
        }

        public override void VisitField(in Unit ctx, in DpField field)
        {
            _nodeLines.AppendLine(FormatField(field.Field, _options));
            base.VisitField(ctx, in field);
        }
    }
}