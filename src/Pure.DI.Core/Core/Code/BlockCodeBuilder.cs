﻿// ReSharper disable ClassNeverInstantiated.Global
namespace Pure.DI.Core.Code;

internal class BlockCodeBuilder: ICodeBuilder<Block>
{
    public void Build(BuildContext ctx, in Block block)
    {
        var variable = ctx.Variable;
        if (!IsNewInstanceRequired(variable))
        {
            return;
        }
        
        var info = block.Current.Info;
        if (info.HasCode)
        {
            ctx.Code.AppendLines(info.Code.Lines);
            return;
        }

        try
        {
            var code = info.Code;
            var level = ctx.Level;
            var isThreadSafe = ctx.DependencyGraph.Source.Hints.GetHint(Hint.ThreadSafe, SettingState.On) == SettingState.On;
            var lockIsRequired = ctx.LockIsRequired ?? isThreadSafe;
            var toCheckExistence =
                // The "singleton" or "scoped" instance must be created with a check each time
                variable.Node.Lifetime is Lifetime.Singleton or Lifetime.Scoped
                || variable.Node.Lifetime == Lifetime.Scoped
                // The "per resolve" instance should be created without checks if it is the only one in the composition
                || (variable.Node.Lifetime == Lifetime.PerResolve && variable.Info.RefCount > 1);

            var parent = "";
            if (variable.Node.Lifetime == Lifetime.Singleton)
            {
                parent = $"{Names.ParentFieldName}.";
            }
            
            if (toCheckExistence)
            {
                var checkExpression = variable.InstanceType.IsValueType
                    ? $"!{parent}{variable.VariableName}Created"
                    : $"ReferenceEquals({parent}{variable.VariableName}, null)";

                if (lockIsRequired)
                {
                    code.AppendLine($"if ({checkExpression})");
                    code.AppendLine("{");
                    code.IncIndent();
                    code.AppendLine($"lock ({Names.LockFieldName})");
                    code.AppendLine("{");
                    code.IncIndent();
                    ctx = ctx with { LockIsRequired = false };
                }

                code.AppendLine($"if ({checkExpression})");
                code.AppendLine("{");
                code.IncIndent();
                ctx = ctx with { Level = level + 1 };
            }
            
            foreach (var statement in block.Statements)
            {
                ctx.StatementBuilder.Build(ctx with { Variable = statement.Current, Code = code }, statement);
            }
            
            if (variable.Node.Lifetime is Lifetime.Singleton)
            {
                code.AppendLine($"{parent}{variable.VariableName} = {variable.VariableName};");
                variable.VariableCode = $"{parent}{variable.VariableName}";
            }

            if (!toCheckExistence)
            {
                return;
            }
            
            if (variable.Node.Lifetime is Lifetime.Singleton or Lifetime.Scoped && variable.Node.IsDisposable())
            {
                code.AppendLine($"{parent}{Names.DisposablesFieldName}[{parent}{Names.DisposeIndexFieldName}++] = {variable.VariableName};");
            }
            
            if (variable.InstanceType.IsValueType)
            {
                if (variable.Node.Lifetime is not Lifetime.Transient and not Lifetime.PerBlock && isThreadSafe)
                {
                    code.AppendLine($"{Names.SystemNamespace}Threading.Thread.MemoryBarrier();");
                }

                code.AppendLine($"{parent}{variable.VariableName}Created = true;");
            }

            code.DecIndent();
            code.AppendLine("}");
            if (!lockIsRequired)
            {
                return;
            }

            code.DecIndent();
            code.AppendLine("}");
            code.DecIndent();
            code.AppendLine("}");
            code.AppendLine();
        }
        finally
        {
            info.HasCode = true;
            if (block.Parent is not null
                && info is { PerBlockRefCount: > 2, Code.Lines.Count: > 4 }) 
            {
                var localFunctionsCode = ctx.LocalFunctionsCode;
                var localMethodName = $"{Names.LocalMethodPrefix}{variable.VariableName}{Names.EnsureExistsMethodNamePostfix}";
                if (variable.Node.Binding.SemanticModel.Compilation.GetLanguageVersion() >= LanguageVersion.CSharp9)
                {
                    localFunctionsCode.AppendLine($"[{Names.MethodImplAttribute}(({Names.MethodImplOptions})0x300)]");
                }
                
                localFunctionsCode.AppendLine($"void {localMethodName}()");
                localFunctionsCode.AppendLine("{");
                using (localFunctionsCode.Indent())
                {
                    localFunctionsCode.AppendLines(info.Code.Lines);
                }
                
                localFunctionsCode.AppendLine("}");
                ctx.Code.AppendLine($"{localMethodName}();");
            }
            else
            {
                ctx.Code.AppendLines(info.Code.Lines);
            }
        }
    }
    
    private static bool IsNewInstanceRequired(Variable variable)
    {
        if (variable.Node.Lifetime == Lifetime.Transient)
        {
            return true;
        }

        if (variable.Current.HasCycle)
        {
            return false;
        }
        
        var owners = variable
            .GetPath()
            .Skip(1)
            .TakeWhile(i => !i.Current.IsLazy)
            .OfType<Block>()
            .ToArray();

        if (variable.Info.Owners.Intersect(owners).Any())
        {
            return false;
        }

        variable.Info.Owners.Add(owners.FirstOrDefault());
        return true;
    }
}