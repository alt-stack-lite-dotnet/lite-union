using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Lite.Union;

namespace Lite.Union.GeneratorTest;

internal sealed record RunResult(
    Compilation Output,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    IReadOnlyList<string> GeneratedFiles)
{
    public IEnumerable<string> GeneratorIds => GeneratorDiagnostics.Select(d => d.Id);
    /// <summary>Errors and warnings of the generator; informational messages are not problems.</summary>
    public IEnumerable<Diagnostic> GeneratorProblems => GeneratorDiagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning);
    public IEnumerable<Diagnostic> Errors => CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
    public IEnumerable<Diagnostic> Warnings => CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}

internal static class Harness
{
    private static readonly ImmutableArray<MetadataReference> References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(p => Path.GetFileName(p).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(p) == "mscorlib.dll" || Path.GetFileName(p) == "netstandard.dll")
        .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
        .ToImmutableArray();

    public static CSharpCompilation Compile(string source, bool allowUnsafe = false, params string[] more)
    {
        var trees = new[] { source }.Concat(more).Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Latest)));
        return CSharpCompilation.Create(
            "TestAssembly",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: allowUnsafe));
    }

    public static RunResult Run(string source, bool allowUnsafe = false, params string[] more)
    {
        var compilation = Compile(source, allowUnsafe, more);
        var driver = CSharpGeneratorDriver.Create(new UnionGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var files = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToList();
        return new RunResult(output, diagnostics, output.GetDiagnostics(), files);
    }
}

internal static class DiagnosticText
{
    /// <summary>The text a single-line diagnostic underlines.</summary>
    public static string Underlined(this Diagnostic diagnostic, string source)
    {
        var span = diagnostic.Location.GetLineSpan().Span;
        var line = source.Split('\n')[span.Start.Line];
        return line.Substring(span.Start.Character, span.End.Character - span.Start.Character);
    }
}

internal static class OldPlatform
{
    /// <summary>Reference assemblies of net6.0 from the local NuGet cache, or null when they are not there.</summary>
    public static ImmutableArray<MetadataReference>? Net6References()
    {
        var packs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "microsoft.netcore.app.ref");
        if (!Directory.Exists(packs))
        {
            return null;
        }

        var folder = Directory.GetDirectories(packs, "6.0.*").OrderBy(p => p).LastOrDefault();
        var refs = folder is null ? null : Path.Combine(folder, "ref", "net6.0");
        return refs is not null && Directory.Exists(refs)
            ? Directory.GetFiles(refs, "*.dll").Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray()
            : null;
    }

    public static RunResult Run(string source, ImmutableArray<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            "OldAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new RunResult(output, diagnostics, output.GetDiagnostics(), driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToList());
    }
}

internal static class NetStandardPlatform
{
    /// <summary>Reference assemblies of netstandard2.0 (they hide the fields of BCL structs), or null if not in the NuGet cache.</summary>
    public static ImmutableArray<MetadataReference>? References()
    {
        var packs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "netstandard.library");
        if (!Directory.Exists(packs))
        {
            return null;
        }

        var folder = Directory.GetDirectories(packs, "2.0.*").OrderBy(p => p).LastOrDefault();
        var refs = folder is null ? null : Path.Combine(folder, "build", "netstandard2.0", "ref");
        return refs is not null && Directory.Exists(refs)
            ? Directory.GetFiles(refs, "*.dll").Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray()
            : null;
    }
}

/// <summary>Project-wide MSBuild properties as the generator sees them (build_property.*).</summary>
internal sealed class TestOptionsProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
{
    private readonly Options _global;

    public TestOptionsProvider(System.Collections.Generic.IDictionary<string, string> properties)
        => _global = new Options(properties.ToDictionary(p => "build_property." + p.Key, p => p.Value));

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => _global;

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;

    public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;

    private sealed class Options : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _values;

        public Options(System.Collections.Generic.Dictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}

internal static class SettingsHarness
{
    public static RunResult Run(string source, params (string Name, string Value)[] properties)
    {
        var compilation = Harness.Compile(source);
        var provider = new TestOptionsProvider(properties.ToDictionary(p => p.Name, p => p.Value));
        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver
            .Create(new[] { new UnionGenerator().AsSourceGenerator() }, optionsProvider: provider)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new RunResult(output, diagnostics, output.GetDiagnostics(), driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToList());
    }

    public static string Generated(RunResult run, string union)
        => run.Output.SyntaxTrees.Single(t => System.IO.Path.GetFileName(t.FilePath) == union + ".Union.g.cs").ToString();
}

internal static class LibraryHarness
{
    /// <summary>Compiles <paramref name="source"/> next to a library (built from <paramref name="library"/>) that it references.</summary>
    public static RunResult Run(string library, string source)
    {
        var libraryCompilation = CSharpCompilation.Create(
            "ReferencedLibrary",
            new[] { CSharpSyntaxTree.ParseText(library, new CSharpParseOptions(LanguageVersion.Latest)) },
            Harness.Compile("").References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilation = Harness.Compile(source).AddReferences(libraryCompilation.ToMetadataReference());
        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(new UnionGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new RunResult(output, diagnostics, output.GetDiagnostics(), driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToList());
    }
}
