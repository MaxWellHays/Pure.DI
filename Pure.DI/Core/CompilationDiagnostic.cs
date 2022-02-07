﻿namespace Pure.DI.Core;

// ReSharper disable once ClassNeverInstantiated.Global
internal class CompilationDiagnostic : IDiagnostic
{
    private readonly ILog<CompilationDiagnostic> _log;

    public CompilationDiagnostic(ILog<CompilationDiagnostic> log) => _log = log;

    public IExecutionContext? Context { get; set; }

    public void Error(string id, string message, params Location[] locations)
    {
        try
        {
            Context?.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id,
                    "Error",
                    message,
                    "Error",
                    DiagnosticSeverity.Error,
                    true),
                GetMainLocation(locations),
                GetAdditionalLocations(locations)));
        }
        catch
        {
            // ignored
        }

        if (id != Diagnostics.Error.Unhandled)
        {
            _log.Trace(() => new[]
            {
                $"{id} {message}"
            });
        }
    }

    public void Warning(string id, string message, params Location[] locations)
    {
        try
        {
            Context?.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id,
                    "Warning",
                    message,
                    "Warning",
                    DiagnosticSeverity.Warning,
                    true),
                GetMainLocation(locations),
                GetAdditionalLocations(locations)));
        }
        catch
        {
            // ignored
        }

        _log.Trace(() => new[]
        {
            $"{id} {message}"
        });
    }

    public void Information(string id, string message, params Location[] locations)
    {
        try
        {
            Context?.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id,
                    "Info",
                    message,
                    "Info",
                    DiagnosticSeverity.Info,
                    true),
                GetMainLocation(locations),
                GetAdditionalLocations(locations)));
        }
        catch
        {
            // ignored
        }

        _log.Trace(() => new[]
        {
            $"{id} {message}"
        });
    }

    private static Location? GetMainLocation(params Location[] locations) =>
        locations.FirstOrDefault(i => i.IsInSource);

    private static IEnumerable<Location> GetAdditionalLocations(params Location[] locations)
    {
        var main = GetMainLocation(locations);
        return main != default ? locations.Except(new []{main}) : ImmutableArray<Location>.Empty;
    }
}