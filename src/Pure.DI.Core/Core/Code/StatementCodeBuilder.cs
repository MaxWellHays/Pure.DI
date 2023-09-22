﻿// ReSharper disable RedundantJumpStatement
// ReSharper disable InvertIf
namespace Pure.DI.Core.Code;

internal class StatementCodeBuilder : ICodeBuilder<IStatement>
{
    private readonly ICodeBuilder<Block> _blockBuilder;
    private readonly ICodeBuilder<Variable> _variableBuilder;
    public StatementCodeBuilder(
        ICodeBuilder<Block> blockBuilder,
        ICodeBuilder<Variable> variableBuilder)
    {
        _blockBuilder = blockBuilder;
        _variableBuilder = variableBuilder;
    }

    public void Build(BuildContext ctx, in IStatement statement)
    {
        ctx = ctx with { ContextTag = statement.Current.Injection.Tag };
        switch (statement)
        {
            case Variable variable:
                if (!variable.IsCreated)
                {
                    _variableBuilder.Build(ctx, variable);
                }

                break;

            case Block block:
                _blockBuilder.Build(ctx, block);
                break;
        }
    }
}