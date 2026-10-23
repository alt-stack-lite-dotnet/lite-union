using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Lite.Union;
using Xunit;

namespace Lite.Union.GeneratorTest;

public class DiagnosticTests
{
    [Fact]
    public void A_struct_that_is_not_partial_is_reported_on_its_name()
    {
        const string source = "using Lite.Union;\n[Union<int, string>] public struct S;";
        var run = Harness.Run(source);

        var diagnostic = Assert.Single(run.GeneratorDiagnostics);
        Assert.Equal("LUNION001", diagnostic.Id);
        Assert.Equal("S", diagnostic.Underlined(source));
        Assert.DoesNotContain(run.GeneratedFiles, f => f.Contains(".Union.g.cs"));
    }

    [Fact]
    public void A_containing_type_that_is_not_partial_is_reported()
    {
        var run = Harness.Run("using Lite.Union;\npublic class Outer { [Union<int, string>] public partial struct S; }");
        Assert.Contains("LUNION001", run.GeneratorIds);
    }

    [Fact]
    public void Two_cases_with_the_same_name_are_reported()
    {
        var run = Harness.Run(@"
using Lite.Union;
namespace N1 { public struct A { } }
namespace N2 { public struct A { } }
[Union<N1.A, N2.A>] public partial struct U;");

        Assert.Contains("LUNION002", run.GeneratorIds);
    }

    [Fact]
    public void A_generic_union_is_reported()
    {
        var run = Harness.Run("using Lite.Union;\n[Union<int, string>] public partial struct U<T>;");
        Assert.Contains("LUNION003", run.GeneratorIds);
    }

    [Fact]
    public void A_union_nested_in_a_generic_type_is_reported()
    {
        var run = Harness.Run("using Lite.Union;\npublic partial class Outer<T> { [Union<int, string>] public partial struct U; }");
        Assert.Contains("LUNION003", run.GeneratorIds);
    }

    [Fact]
    public void A_record_struct_is_reported()
    {
        var run = Harness.Run("using Lite.Union;\n[Union<int, string>] public partial record struct U;");
        Assert.Contains("LUNION004", run.GeneratorIds);
    }

    private const string PointerSource = @"
using Lite.Union;
public unsafe struct WithPointer { public int* P; public string S; }
[Union<int, WithPointer, string>] public partial struct U;";

    [Fact]
    public void Pointers_without_unsafe_are_reported_on_the_type_argument()
    {
        var run = Harness.Run(PointerSource, allowUnsafe: false);

        var diagnostic = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LUNION005");
        Assert.Equal("WithPointer", diagnostic.Underlined(PointerSource));
        Assert.DoesNotContain(run.GeneratedFiles, f => f.Contains("U.Union.g.cs"));
    }

    [Fact]
    public void Pointers_with_unsafe_generate_clean_code()
    {
        var run = Harness.Run(PointerSource, allowUnsafe: true);

        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.CompilationDiagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));
        Assert.Contains(run.GeneratedFiles, f => f.EndsWith("U.Union.g.cs"));
    }

    [Fact]
    public void An_unknown_case_type_produces_nothing_and_does_not_throw()
    {
        var run = Harness.Run("using Lite.Union;\n[Union<int, DoesNotExist>] public partial struct U;");
        Assert.DoesNotContain(run.GeneratedFiles, f => f.EndsWith("U.Union.g.cs"));
        Assert.Empty(run.GeneratorProblems);
    }

    [Fact]
    public void Structs_with_other_attributes_are_left_alone()
    {
        var run = Harness.Run("using System;\n[Serializable] public partial struct S;");
        Assert.Equal(new[] { "UnionAttribute.g.cs" }, run.GeneratedFiles);
    }

    [Fact]
    public void An_attribute_with_the_same_name_from_another_namespace_is_ignored()
    {
        var run = Harness.Run(@"
namespace Other { public class UnionAttribute<T1, T2> : System.Attribute { } }
[Other.Union<int, string>] public partial struct S;");

        Assert.DoesNotContain(run.GeneratedFiles, f => f.EndsWith("S.Union.g.cs"));
    }
}

public class CleanOutputTests
{
    private const string Types = @"
using System;
using System.Collections.Generic;
namespace Shapes
{
    public class K { public int X; }
    public interface IThing { }
    public enum Color : byte { Red }
    public struct Mixed { public int I; public string S; }
    public struct Refs { public string A; public object B; }
    public struct Plain { public int X; public long Y; }
    public struct Empty { }
    public delegate void Handler();
}";

    public static IEnumerable<object[]> Unions() => new[]
    {
        "[Union<int, string>] public partial struct U;",
        "[Union<int, string>] internal partial struct U;",
        "[Union<int, string>] public readonly partial struct U;",
        "[Union<Shapes.Mixed, Shapes.Refs, Shapes.Plain, Shapes.Empty>] public partial struct U;",
        "[Union<Shapes.K, Shapes.IThing, Shapes.Handler, object>] public partial struct U;",
        "[Union<Shapes.Color, long, double, decimal, Guid, DateTime, TimeSpan>] public partial struct U;",
        "[Union<List<int>, Dictionary<string, List<int>>, int[], string[][]>] public partial struct U;",
        "[Union<int?, (int, string), KeyValuePair<string, int>, Shapes.Mixed>] public partial struct U;",
        "[Union<Shapes.Mixed, Shapes.Refs, string>(Layout = UnionLayout.Flat)] public partial struct U;",
        "[Union<Shapes.Mixed, Shapes.Refs, string>(Layout = UnionLayout.Boxed)] public partial struct U;",
        "[Union<Shapes.Mixed, Shapes.Plain>(Layout = UnionLayout.Overlap)] public readonly partial struct U;",
        "namespace Ns { [Union<int, string>] public partial struct U; }",
        "namespace A.B.C { [Union<Shapes.Mixed, int>] public partial struct U; }",
        "public partial class Host { [Union<int, string>] public partial struct U; }",
        "public partial class Host { public partial struct Mid { [Union<int, string>] public partial struct U; } }",
        "public partial record Host { [Union<int, string>] public partial struct U; }",
        "public partial interface IHost { [Union<int, string>] public partial struct U; }",
        "#nullable disable\n[Union<int, string>] public partial struct U;",
        "[Union<byte, sbyte, short, ushort, int, uint, long, ulong, float, double, char, bool, string, object, Shapes.K, Shapes.Mixed>] public partial struct U;",
    }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Unions))]
    public void Generated_code_compiles_without_errors_or_warnings(string union)
    {
        var run = Harness.Run("using Lite.Union;\nusing System;\nusing System.Collections.Generic;\n" + union, allowUnsafe: false, Types);

        Assert.Empty(run.GeneratorProblems);
        Assert.Contains(run.GeneratedFiles, f => f.EndsWith("U.Union.g.cs"));
        Assert.Contains(run.Output.SyntaxTrees, t => t.FilePath.EndsWith("U.Union.g.cs"));
        var problems = run.CompilationDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning && d.Location.SourceTree?.FilePath is { Length: > 0 })
            .Select(d => d.ToString())
            .ToList();
        Assert.Empty(problems);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void Output_is_deterministic()
    {
        const string source = "using Lite.Union;\n[Union<int, string>] public partial struct U;";
        static List<string> Texts(RunResult r) => r.Output.SyntaxTrees.Select(t => t.ToString()).OrderBy(t => t).ToList();

        Assert.Equal(Texts(Harness.Run(source)), Texts(Harness.Run(source)));
    }

    [Fact]
    public void Two_unions_in_one_file_get_two_outputs()
    {
        var run = Harness.Run("using Lite.Union;\n[Union<int, string>] public partial struct A;\n[Union<long, string>] public partial struct B;");
        Assert.Contains(run.GeneratedFiles, f => f.EndsWith("A.Union.g.cs"));
        Assert.Contains(run.GeneratedFiles, f => f.EndsWith("B.Union.g.cs"));
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void The_attribute_supports_two_to_sixteen_cases()
    {
        var types = new[] { "int", "long", "string", "object", "byte", "short", "char", "bool", "float", "double", "decimal", "Guid", "DateTime", "TimeSpan", "uint", "ulong" };
        var sources = Enumerable.Range(2, 15).Select(n => $"[Union<{string.Join(", ", types.Take(n))}>] public partial struct U{n};");

        var run = Harness.Run("using Lite.Union;\nusing System;\n" + string.Join("\n", sources));
        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.Errors);
        Assert.Equal(15, run.GeneratedFiles.Count(f => f.EndsWith(".Union.g.cs")));
    }
}

public class CachingTests
{
    private const string Source = "using Lite.Union;\n[Union<int, string>] public partial struct U;";

    private static GeneratorDriver NewDriver()
        => CSharpGeneratorDriver.Create(
            new[] { new UnionGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static IEnumerable<IncrementalStepRunReason> Reasons(GeneratorDriver driver)
        => driver.GetRunResult().Results[0].TrackedSteps[TrackingNames.Models].SelectMany(s => s.Outputs).Select(o => o.Reason);

    [Fact]
    public void An_unrelated_edit_reuses_the_model()
    {
        var compilation = Harness.Compile(Source);
        var driver = NewDriver().RunGenerators(compilation);
        Assert.All(Reasons(driver), r => Assert.Equal(IncrementalStepRunReason.New, r));

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Unrelated { int x; }"));
        driver = driver.RunGenerators(edited);

        Assert.All(Reasons(driver), r => Assert.True(r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, r.ToString()));
    }

    [Fact]
    public void A_comment_inside_the_union_file_does_not_change_the_model()
    {
        var compilation = Harness.Compile(Source);
        var driver = NewDriver().RunGenerators(compilation);

        var changed = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), CSharpSyntaxTree.ParseText("// a comment\n" + Source));
        driver = driver.RunGenerators(changed);

        Assert.All(Reasons(driver), r => Assert.True(r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, r.ToString()));
    }

    [Fact]
    public void Changing_the_cases_changes_the_model()
    {
        var compilation = Harness.Compile(Source);
        var driver = NewDriver().RunGenerators(compilation);

        var changed = compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.First(),
            CSharpSyntaxTree.ParseText("using Lite.Union;\n[Union<int, long>] public partial struct U;"));
        driver = driver.RunGenerators(changed);

        Assert.Contains(Reasons(driver), r => r == IncrementalStepRunReason.Modified);
    }
}

public class PlatformTests
{
    private const string MixedUnion = @"
using Lite.Union;
public struct Mixed { public int I; public string S; }
[Union<Mixed, int>] public partial struct U { }";

    private static string Generated(RunResult run) => run.Output.SyntaxTrees.Single(t => t.FilePath.EndsWith("U.Union.g.cs")).ToString();

    private static readonly bool RuntimeKnowsUnions = System.Type.GetType("System.Runtime.CompilerServices.UnionAttribute") is not null;

    [Fact]
    public void Mixed_structs_are_split_where_UnsafeAccessor_exists()
    {
        var generated = Generated(Harness.Run(MixedUnion));
        Assert.Contains("UnsafeAccessor", generated);
        Assert.DoesNotContain("__Opaque", generated);
    }

    [Fact]
    public void Mixed_structs_fall_back_to_a_field_of_their_own_before_net8()
    {
        var references = OldPlatform.Net6References();
        if (references is null)
        {
            return; // the net6.0 reference pack is not in the NuGet cache of this machine
        }

        var run = OldPlatform.Run(MixedUnion, references.Value);

        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.Errors);
        var generated = Generated(run);
        Assert.DoesNotContain("UnsafeAccessor", generated);
        Assert.Contains("__Opaque", generated);
    }

    [Fact]
    public void Union_members_are_generated_only_where_the_runtime_has_unions()
    {
        var generated = Generated(Harness.Run(MixedUnion));
        Assert.Equal(RuntimeKnowsUnions, generated.Contains("IUnion"));
        Assert.Equal(RuntimeKnowsUnions, generated.Contains("bool HasValue"));
        Assert.Equal(RuntimeKnowsUnions, generated.Contains("bool TryGetValue("));
    }

    [Fact]
    public void Union_members_are_not_generated_on_old_platforms()
    {
        var references = OldPlatform.Net6References();
        if (references is null)
        {
            return;
        }

        var generated = Generated(OldPlatform.Run(MixedUnion, references.Value));
        Assert.DoesNotContain("IUnion", generated);
        Assert.DoesNotContain("HasValue", generated);
    }
}

public class HiddenFieldTests
{
    private static string Generated(RunResult run, string union) => run.Output.SyntaxTrees.Single(t => System.IO.Path.GetFileName(t.FilePath) == union + ".Union.g.cs").ToString();

    // The compiler sees netstandard's BCL structs as empty; the generator has to find out what they really hold.
    [Fact]
    public void Structs_without_visible_fields_are_classified_by_what_they_really_hold()
    {
        var references = NetStandardPlatform.References();
        if (references is null)
        {
            return; // the netstandard 2.0 reference pack is not in the NuGet cache of this machine
        }

        var run = OldPlatform.Run(@"
using System;
using System.Collections.Generic;
using System.Threading;
using Lite.Union;
[Union<Guid, string>] public partial struct OnlyNumbers { }
[Union<KeyValuePair<int, int>, string>] public partial struct GenericNumbers { }
[Union<KeyValuePair<string, int>, int>] public partial struct GenericWithReference { }
[Union<CancellationToken, int>] public partial struct WithReference { }
public struct Holder { public KeyValuePair<string, int> Kv; public int X; }
[Union<Holder, int>] public partial struct Nested { }", references.Value);

        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.Errors);

        // plain numbers: case 0 is a value and overlaps with the other cases
        Assert.Contains("global::System.Guid f0", Generated(run, "OnlyNumbers"));
        Assert.Contains("struct __V0", Generated(run, "OnlyNumbers"));
        Assert.DoesNotContain("__Opaque", Generated(run, "OnlyNumbers"));
        Assert.Contains("KeyValuePair<int, int> f0", Generated(run, "GenericNumbers"));
        Assert.Contains("struct __V0", Generated(run, "GenericNumbers"));
        Assert.DoesNotContain("__Opaque", Generated(run, "GenericNumbers"));

        // they hold a reference: kept as they are, never part of the overlapped values
        foreach (var union in new[] { "GenericWithReference", "WithReference", "Nested" })
        {
            var text = Generated(run, union);
            Assert.Contains("__Opaque", text);
            Assert.DoesNotContain("struct __V0", text);
        }
    }
}

public class FallbackDiagnosticTests
{
    private const string Prelude = @"
using System.Collections.Generic;
using Lite.Union;
public struct Holder { public KeyValuePair<string, int> Kv; public int X; }
public class Animal { }
public class Dog : Animal { }
";

    private static Diagnostic Info(RunResult run) => Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LUNION100");

    [Fact]
    public void A_case_that_is_not_overlapped_is_reported_with_the_reason_and_does_not_stop_the_generation()
    {
        var source = Prelude + "[Union<Holder, int>] public partial struct U { }";
        var run = Harness.Run(source);

        var info = Info(run);
        Assert.Equal(DiagnosticSeverity.Info, info.Severity);
        Assert.Equal("Holder", info.Underlined(source));
        Assert.Contains("generic struct that mixes values and references", info.GetMessage());
        Assert.Contains("in a field of its own", info.GetMessage());
        Assert.Contains(run.GeneratedFiles, f => f.EndsWith("U.Union.g.cs"));
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void Fallback_Boxed_is_named_in_the_message_and_changes_the_layout()
    {
        var source = Prelude + "[Union<Holder, int>(Fallback = UnionFallback.Boxed)] public partial struct U { }";
        var run = Harness.Run(source);

        Assert.Contains("boxed in an object field", Info(run).GetMessage());
        var text = run.Output.SyntaxTrees.Single(t => System.IO.Path.GetFileName(t.FilePath) == "U.Union.g.cs").ToString();
        Assert.Contains("object? _obj", text);
        Assert.DoesNotContain("__Opaque", text);
        Assert.Empty(run.Errors);
    }

    [Theory]
    [InlineData("Flat")]
    [InlineData("Boxed")]
    public void An_explicit_layout_other_than_Overlap_reports_nothing(string layout)
    {
        var run = Harness.Run(Prelude + $"[Union<Holder, int>(Layout = UnionLayout.{layout})] public partial struct U {{ }}");
        Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "LUNION100");
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void Cases_that_overlap_report_nothing()
    {
        var run = Harness.Run(Prelude + "[Union<int, string, long>] public partial struct U { }");
        Assert.Empty(run.GeneratorDiagnostics);
    }

    [Fact]
    public void Without_UnsafeAccessor_the_message_says_so()
    {
        var references = OldPlatform.Net6References();
        if (references is null)
        {
            return;
        }

        var run = OldPlatform.Run(@"
using Lite.Union;
public struct Mixed { public int I; public string S; }
[Union<Mixed, int>] public partial struct U { }", references.Value);

        var info = Info(run);
        Assert.Contains("UnsafeAccessor", info.GetMessage());
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void Boxed_cases_that_are_the_same_object_cannot_be_told_apart()
    {
        var source = "using Lite.Union;\n[Union<int, int?>(Layout = UnionLayout.Boxed)] public partial struct U { }";
        var run = Harness.Run(source);

        var error = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LUNION006");
        Assert.Equal("int?", error.Underlined(source));
        Assert.DoesNotContain(run.GeneratedFiles, f => f.EndsWith("U.Union.g.cs"));
    }

    [Fact]
    public void Boxed_asks_for_the_more_specific_case_first()
    {
        var run = Harness.Run(Prelude + "[Union<object, Animal, Dog>(Layout = UnionLayout.Boxed)] public partial struct U { }");

        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.Errors);
        var text = run.Output.SyntaxTrees.Single(t => System.IO.Path.GetFileName(t.FilePath) == "U.Union.g.cs").ToString();
        var dog = text.IndexOf("o is global::Dog", System.StringComparison.Ordinal);
        var animal = text.IndexOf("o is global::Animal", System.StringComparison.Ordinal);
        var any = text.IndexOf("o is object", System.StringComparison.Ordinal);
        Assert.True(dog >= 0 && dog < animal && animal < any, $"order: dog={dog}, animal={animal}, object={any}");
        Assert.DoesNotContain("byte _kind;", text);
    }
}

public class ProjectSettingsTests
{
    private const string Prelude = @"
using System.Collections.Generic;
using Lite.Union;
public struct Holder { public KeyValuePair<string, int> Kv; public int X; }
public struct A { public int I; public string S; }
";

    private static string Overlapped(string text) => text.Contains("struct __Values") ? "overlap" : "no";

    [Fact]
    public void Without_settings_the_default_is_Overlap()
    {
        var run = SettingsHarness.Run(Prelude + "[Union<int, string>] public partial struct U { }");
        Assert.Contains("struct __Values", SettingsHarness.Generated(run, "U"));
    }

    [Theory]
    [InlineData("Flat", "struct __Opaque")]
    [InlineData("flat", "struct __Opaque")]          // not case sensitive
    [InlineData("Boxed", "object? _obj")]
    public void The_project_layout_applies_when_the_attribute_says_nothing(string value, string expected)
    {
        var run = SettingsHarness.Run(Prelude + "[Union<int, string>] public partial struct U { }", ("LiteUnionLayout", value));
        var text = SettingsHarness.Generated(run, "U");
        Assert.Contains(expected, text);
        Assert.DoesNotContain("struct __Values", text);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void The_attribute_wins_over_the_project_layout_even_when_it_asks_for_the_default()
    {
        var run = SettingsHarness.Run(
            Prelude + "[Union<int, string>(Layout = UnionLayout.Overlap)] public partial struct U { }",
            ("LiteUnionLayout", "Boxed"));

        var text = SettingsHarness.Generated(run, "U");
        Assert.Contains("struct __Values", text);
        Assert.DoesNotContain("object? _obj", text);
    }

    [Fact]
    public void The_project_fallback_applies_to_cases_that_cannot_be_overlapped()
    {
        var run = SettingsHarness.Run(Prelude + "[Union<Holder, int>] public partial struct U { }", ("LiteUnionFallback", "Boxed"));

        var text = SettingsHarness.Generated(run, "U");
        Assert.Contains("object? _obj", text);
        Assert.DoesNotContain("__Opaque", text);
        Assert.Contains("boxed in an object field", Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LUNION100").GetMessage());
    }

    [Fact]
    public void The_attribute_fallback_wins_over_the_project_fallback()
    {
        var run = SettingsHarness.Run(
            Prelude + "[Union<Holder, int>(Fallback = UnionFallback.Field)] public partial struct U { }",
            ("LiteUnionFallback", "Boxed"));

        var text = SettingsHarness.Generated(run, "U");
        Assert.Contains("__Opaque", text);
        Assert.DoesNotContain("object? _obj", text);
    }

    [Fact]
    public void The_project_layout_and_fallback_work_together()
    {
        var run = SettingsHarness.Run(
            Prelude + "[Union<Holder, A, int>] public partial struct U { }",
            ("LiteUnionLayout", "Overlap"), ("LiteUnionFallback", "Boxed"));

        var text = SettingsHarness.Generated(run, "U");
        Assert.Contains("struct __Values", text);
        Assert.Contains("object? _obj", text);
        Assert.Empty(run.Errors);
    }

    [Fact]
    public void An_unknown_value_is_reported_and_the_default_is_used()
    {
        var run = SettingsHarness.Run(Prelude + "[Union<int, string>] public partial struct U { }", ("LiteUnionLayout", "Compact"));

        var warning = Assert.Single(run.GeneratorDiagnostics, d => d.Id == "LUNION007");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Compact", warning.GetMessage());
        Assert.Contains("Overlap, Flat, Boxed", warning.GetMessage());
        Assert.Contains("struct __Values", SettingsHarness.Generated(run, "U"));
    }

    [Fact]
    public void An_empty_value_means_not_set()
    {
        var run = SettingsHarness.Run(Prelude + "[Union<int, string>] public partial struct U { }", ("LiteUnionLayout", "  "));
        Assert.Empty(run.GeneratorDiagnostics);
        Assert.Contains("struct __Values", SettingsHarness.Generated(run, "U"));
    }
}

public class PolyfillTests
{
    // StructUnion ships internal copies of UnionAttribute and IUnion for .NET 10. They are found by name but cannot be used,
    // so a union generated next to them must not name them.
    private const string InternalPolyfill = @"
namespace System.Runtime.CompilerServices
{
    internal sealed class UnionAttribute : System.Attribute { }
    internal interface IUnion { object? Value { get; } }
}";

    [Fact]
    public void Internal_copies_of_the_union_types_in_a_referenced_library_are_ignored()
    {
        var run = LibraryHarness.Run(InternalPolyfill, "using Lite.Union;\n[Union<int, string>] public partial struct U { }");

        Assert.Empty(run.GeneratorProblems);
        Assert.Empty(run.Errors);

        // Where the framework has the real types (.NET 11) they are used; where it has not (.NET 10) the internal copy is not.
        var text = SettingsHarness.Generated(run, "U");
        Assert.Equal(System.Type.GetType("System.Runtime.CompilerServices.UnionAttribute") is not null, text.Contains("IUnion"));
    }

    [Fact]
    public void Public_union_types_in_a_referenced_library_are_used()
    {
        if (System.Type.GetType("System.Runtime.CompilerServices.UnionAttribute") is not null)
        {
            return; // the framework has them too: two public copies would be ambiguous for any code, not only the generated one
        }

        var run = LibraryHarness.Run(
            InternalPolyfill.Replace("internal sealed class", "public sealed class").Replace("internal interface", "public interface"),
            "using Lite.Union;\n[Union<int, string>] public partial struct U { }");

        Assert.Empty(run.Errors);
        Assert.Contains("IUnion", SettingsHarness.Generated(run, "U"));
    }
}
