﻿// ReSharper disable InvertIf
namespace Pure.DI.Core.Code;

using System.Collections.Generic;

internal class LocalVariableRenamingRewriter(
    IIdGenerator idGenerator,
    SemanticModel semanticModel)
    : CSharpSyntaxRewriter(true)
{
    private readonly Dictionary<string, string> _identifierNames = new();

    public LambdaExpressionSyntax Rewrite(LambdaExpressionSyntax lambda) => 
        (LambdaExpressionSyntax)Visit(lambda);

    public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node) => 
        base.VisitVariableDeclarator(node.WithIdentifier(SyntaxFactory.Identifier(GetUniqueName(node.Identifier.Text))));

    public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node) => 
        base.VisitSingleVariableDesignation(node.WithIdentifier(SyntaxFactory.Identifier(GetUniqueName(node.Identifier.Text))));

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (_identifierNames.TryGetValue(token.Text, out var newName)
            && token.IsKind(SyntaxKind.IdentifierToken)
            && token.Parent is { } parent
            && (semanticModel.SyntaxTree != parent.SyntaxTree || semanticModel.GetSymbolInfo(parent).Symbol is ILocalSymbol))
        {
            token = SyntaxFactory.Identifier(newName);
        }
        
        return base.VisitToken(token);
    }

    private string GetUniqueName(string name)
    {
        if (!_identifierNames.TryGetValue(name, out var newName))
        {
            newName = $"{name}_{Names.Salt}{idGenerator.Generate()}";
            _identifierNames.Add(name, newName);
        }

        return newName;
    }
}