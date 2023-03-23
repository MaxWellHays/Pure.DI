namespace Pure.DI.Core.CSharp;

internal class SingletonFieldsBuilder: IBuilder<CompositionCode, CompositionCode>
{
    public CompositionCode Build(CompositionCode composition, CancellationToken cancellationToken)
    {
        var code = composition.Code;
        var membersCounter = composition.MembersCount;
        if (!composition.Singletons.Any())
        {
            return composition;
        }

        if (composition.DisposableSingletonsCount > 0)
        {
            // DisposeIndex field
            code.AppendLine($"private int {Variable.DisposeIndexFieldName};");
            membersCounter++;
        }
            
        // Disposables field
        code.AppendLine($"private {CodeConstants.DisposableTypeName}[] {Variable.DisposablesFieldName};");
        membersCounter++;

        // Singleton fields
        foreach (var singletonField in composition.Singletons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            code.AppendLine($"private {singletonField.Node.Type} {singletonField.Name};");
            membersCounter++;

            if (!singletonField.Node.Type.IsValueType)
            {
                continue;
            }

            code.AppendLine($"private bool {singletonField.Name}Created;");
            membersCounter++;
        }

        return composition with { MembersCount = membersCounter };
    }
}