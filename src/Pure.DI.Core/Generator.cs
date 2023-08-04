namespace Pure.DI;

public sealed class Generator
{
    private static readonly CompositionBase CompositionBase = new();
    private readonly Composition _composition;

    public static IEnumerable<Source> GetApi(CancellationToken cancellationToken) => 
        CompositionBase.ApiBuilder.Build(Unit.Shared);

    public Generator(IOptions options, ISourcesRegistry sources, IDiagnostic diagnostic, CancellationToken cancellationToken) =>
        _composition = new Composition(
            options: options,
            sources: sources,
            diagnostic: diagnostic,
            cancellationToken: cancellationToken);

    public void Generate(IEnumerable<SyntaxUpdate> updates) =>
        _composition.Generator.Build(updates);

    public IDisposable RegisterObserver<T>(IObserver<T> observer) => 
        _composition.ObserversRegistry.Register(observer);
}