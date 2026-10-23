#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lite.Union;

public static class TrackingNames
{
    /// <summary>Name of the step that produces one model per [Union] struct; lets tests check incremental caching.</summary>
    public const string Models = "Models";
}

[Generator(LanguageNames.CSharp)]
public sealed class UnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // UnionAttribute<T1..Tn> is emitted into the consuming compilation (internal).
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("UnionAttribute.g.cs", SourceText.From(AttributeSource.Build(), Encoding.UTF8)));

        // Project-wide defaults: <LiteUnionLayout> and <LiteUnionFallback> in the project file (see build/Lite.Union.props).
        var settings = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => GlobalSettings.From(provider.GlobalOptions));

        var analyses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 } type
                    && (type.IsKind(SyntaxKind.StructDeclaration) || type.IsKind(SyntaxKind.RecordStructDeclaration)),
                static (ctx, ct) => Parse(ctx, ct))
            .Where(static analysis => analysis is not null)
            .Select(static (analysis, _) => analysis!);

        var models = analyses
            .Combine(settings)
            .Select(static (pair, _) => Settle(pair.Left, pair.Right))
            .WithTrackingName(TrackingNames.Models);

        context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model));
    }

    /// <summary>
    /// Everything that can be known about a [Union] struct without the project-wide settings. Layout and Fallback are
    /// kept as written in the attribute (-1 when not written); <see cref="Settle"/> decides the rest.
    /// </summary>
    private static UnionAnalysis? Parse(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var syntax = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(syntax, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        INamedTypeSymbol? attribute = null;
        AttributeSyntax? attributeSyntax = null;
        var layout = -1;
        var fallback = -1;
        foreach (var data in symbol.GetAttributes())
        {
            if (data.AttributeClass is { IsGenericType: true, Name: "UnionAttribute" } candidate
                && candidate.ContainingNamespace.ToDisplayString() == AttributeSource.Namespace)
            {
                attribute = candidate;
                attributeSyntax = data.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;
                foreach (var argument in data.NamedArguments)
                {
                    if (argument.Key == "Layout" && argument.Value.Value is int value)
                    {
                        layout = value;
                    }
                    else if (argument.Key == "Fallback" && argument.Value.Value is int fallbackValue)
                    {
                        fallback = fallbackValue;
                    }
                }

                break;
            }
        }

        if (attribute is null)
        {
            return null;
        }

        var cases = new List<CaseInfo>();
        foreach (var type in attribute.TypeArguments)
        {
            if (type.TypeKind == TypeKind.Error)
            {
                return null; // the compiler already reports it
            }

            cases.Add(BuildCase(type, context.SemanticModel.Compilation, symbol));
        }

        var none = new EquatableArray<LeafInfo>(System.Array.Empty<LeafInfo>());

        // Splitting a struct that mixes references and values needs UnsafeAccessor (.NET 8+);
        // without it such a case is kept as it is, in a field of its own.
        var compilation = context.SemanticModel.Compilation;
        if (!IsUsable(compilation, "System.Runtime.CompilerServices.UnsafeAccessorAttribute", mustComeFromAnotherAssembly: true))
        {
            for (var i = 0; i < cases.Count; i++)
            {
                if (cases[i].NeedsAccessors)
                {
                    cases[i] = cases[i] with
                    {
                        Values = none,
                        Refs = none,
                        IsOpaque = true,
                        FallbackReason = "the target framework has no UnsafeAccessor (.NET 8 or later), so it cannot be split into values and references",
                    };
                }
            }
        }

        var diagnostics = new List<DiagnosticInfo>();
        var location = LocationInfo.From(syntax.Identifier.GetLocation());

        // C# 15 unions: when the compilation knows them, the struct also declares itself a union.
        var nativeUnion = IsUsable(compilation, "System.Runtime.CompilerServices.UnionAttribute", mustComeFromAnotherAssembly: false)
            && IsUsable(compilation, "System.Runtime.CompilerServices.IUnion", mustComeFromAnotherAssembly: false);

        // Containing types (outermost first) and the partial check.
        var containers = new List<ContainerInfo>();
        var allPartial = HasModifier(syntax, SyntaxKind.PartialKeyword);
        SyntaxNode? parent = syntax.Parent;
        while (parent is TypeDeclarationSyntax container)
        {
            allPartial &= HasModifier(container, SyntaxKind.PartialKeyword);
            containers.Insert(0, new ContainerInfo(Keyword(container), container.Identifier.ValueText));
            parent = container.Parent;
        }

        if (!allPartial)
        {
            diagnostics.Add(new DiagnosticInfo(Descriptors.NotPartial, location, symbol.Name));
        }

        if (syntax is RecordDeclarationSyntax)
        {
            diagnostics.Add(new DiagnosticInfo(Descriptors.RecordStruct, location, symbol.Name));
        }

        var isGeneric = false;
        for (INamedTypeSymbol? type = symbol; type is not null; type = type.ContainingType)
        {
            isGeneric |= type.IsGenericType;
        }

        if (isGeneric)
        {
            diagnostics.Add(new DiagnosticInfo(Descriptors.GenericContainer, location, symbol.Name));
        }

        var seen = new HashSet<string>();
        foreach (var @case in cases)
        {
            if (!seen.Add(@case.Name))
            {
                diagnostics.Add(new DiagnosticInfo(Descriptors.DuplicateCase, location, @case.Name));
            }
        }

        // For Layout = Boxed (which may come from the project settings): the order of the type tests and the clashes.
        var indistinct = new List<IndistinctPair>();
        var checkOrder = CheckOrder(attribute.TypeArguments, compilation, indistinct);

        var argumentLocations = new LocationInfo[cases.Count];
        for (var i = 0; i < argumentLocations.Length; i++)
        {
            argumentLocations[i] = ArgumentLocation(attributeSyntax, i, location);
        }

        return new UnionAnalysis(
            symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            new EquatableArray<ContainerInfo>(containers.ToArray()),
            symbol.Name,
            symbol.IsReadOnly,
            nativeUnion,
            layout,
            fallback,
            new EquatableArray<CaseInfo>(cases.ToArray()),
            new EquatableArray<LocationInfo>(argumentLocations),
            location,
            new EquatableArray<int>(checkOrder),
            new EquatableArray<IndistinctPair>(indistinct.ToArray()),
            compilation.Options is CSharpCompilationOptions { AllowUnsafe: true },
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    /// <summary>
    /// Applies the layout and the fallback: what the attribute says wins, then the project settings, then the defaults
    /// (Overlap, Field). Turns the analysis into the model that is emitted.
    /// </summary>
    private static UnionModel Settle(UnionAnalysis analysis, GlobalSettings settings)
    {
        var diagnostics = new List<DiagnosticInfo>(analysis.Diagnostics);
        var layout = analysis.ExplicitLayout >= 0 ? analysis.ExplicitLayout : settings.Layout ?? 0;
        var fallback = analysis.ExplicitFallback >= 0 ? analysis.ExplicitFallback : settings.Fallback ?? 0;

        if (settings.InvalidName is not null)
        {
            diagnostics.Add(new DiagnosticInfo(
                Descriptors.InvalidSetting, analysis.IdentifierLocation, settings.InvalidName, settings.InvalidValue ?? "", settings.InvalidAllowed ?? ""));
        }

        var cases = new List<CaseInfo>(analysis.Cases);
        var none = new EquatableArray<LeafInfo>(System.Array.Empty<LeafInfo>());

        // Layout = Flat: every case keeps its own field. Layout = Boxed: every case lives in one object field.
        if (layout is 1 or 2)
        {
            for (var i = 0; i < cases.Count; i++)
            {
                cases[i] = cases[i] with { Values = none, Refs = none, IsOpaque = layout == 1, IsBoxed = layout == 2, FallbackReason = null };
            }
        }

        // Fallback = Boxed: the cases that could not be overlapped share one object field instead of each having its own.
        if (layout == 0 && fallback == 1)
        {
            for (var i = 0; i < cases.Count; i++)
            {
                if (cases[i].IsOpaque)
                {
                    cases[i] = cases[i] with { IsOpaque = false, IsBoxed = true };
                }
            }
        }

        // Tell where there is no overlap, and why.
        if (layout == 0)
        {
            for (var i = 0; i < cases.Count; i++)
            {
                if (cases[i].FallbackReason is { } why)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Descriptors.StoredAsIs,
                        analysis.ArgumentLocations[i],
                        cases[i].Name,
                        cases[i].IsBoxed ? "boxed in an object field" : "in a field of its own",
                        why));
                }
            }
        }

        // Layout = Boxed has no tag, so two cases that are the same object cannot be told apart.
        if (layout == 2)
        {
            foreach (var pair in analysis.Indistinct)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Descriptors.Indistinguishable, analysis.ArgumentLocations[pair.Index], pair.First, pair.Second));
            }
        }

        // Cases with pointers are split into unsafe code: underline the offending type argument when it is not allowed.
        for (var i = 0; i < cases.Count; i++)
        {
            if (cases[i].NeedsUnsafe && !analysis.AllowUnsafe)
            {
                diagnostics.Add(new DiagnosticInfo(Descriptors.NeedsUnsafe, analysis.ArgumentLocations[i], cases[i].Type));
            }
        }

        return new UnionModel(
            analysis.Namespace,
            analysis.Containers,
            analysis.Name,
            analysis.IsReadOnly,
            layout,
            analysis.NativeUnion,
            layout == 2 ? analysis.CheckOrder : new EquatableArray<int>(System.Array.Empty<int>()),
            new EquatableArray<CaseInfo>(cases.ToArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static void Emit(SourceProductionContext context, UnionModel model)
    {
        var failed = false;
        foreach (var diagnostic in model.Diagnostics)
        {
            context.ReportDiagnostic(diagnostic.ToDiagnostic());
            failed |= diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error;
        }

        // Informational messages do not stop the generation; errors do.
        if (failed)
        {
            return;
        }

        context.AddSource(model.HintName, SourceText.From(UnionEmitter.Emit(model), Encoding.UTF8));
    }

    /// <summary>
    /// Whether a type the generated code wants to name is really there for this project. A type of the same name can sit in
    /// a referenced library as an internal polyfill (StructUnion ships one for UnionAttribute on .NET 10): it is found by
    /// name but cannot be used. A runtime feature (UnsafeAccessor) must come from the framework: a copy declared in the
    /// project itself compiles but does nothing at run time.
    /// </summary>
    private static bool IsUsable(Compilation compilation, string metadataName, bool mustComeFromAnotherAssembly)
    {
        // Several types can carry the name (the framework's and an internal copy in a library): any one the project can see counts.
        foreach (var type in compilation.GetTypesByMetadataName(metadataName))
        {
            if (compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
                && (!mustComeFromAnotherAssembly || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The runtime type of a boxed case: Nullable&lt;T&gt; boxes as T.</summary>
    private static ITypeSymbol RuntimeType(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable ? nullable.TypeArguments[0] : type;

    /// <summary>An object of the first type is also an object of the second (a subtype, an implementation, a boxed value).</summary>
    private static bool IsInstanceOf(Compilation compilation, ITypeSymbol from, ITypeSymbol to)
    {
        if (SymbolEqualityComparer.Default.Equals(from, to))
        {
            return true;
        }

        var conversion = compilation.ClassifyConversion(from, to);
        return conversion.IsIdentity || (conversion.IsImplicit && (conversion.IsReference || conversion.IsBoxing));
    }

    /// <summary>
    /// The order in which a tagless union asks "is it this case?": a case whose objects are also objects of other cases
    /// goes first (Dog before Animal, anything before object); the rest keep their order. Two cases that are the same
    /// runtime type (int and int?) cannot be told apart and are reported.
    /// </summary>
    private static int[] CheckOrder(
        System.Collections.Immutable.ImmutableArray<ITypeSymbol> types,
        Compilation compilation,
        List<IndistinctPair> indistinct)
    {
        var runtime = types.Select(RuntimeType).ToArray();
        var narrower = new int[runtime.Length];
        for (var i = 0; i < runtime.Length; i++)
        {
            for (var j = 0; j < runtime.Length; j++)
            {
                if (i == j)
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(runtime[i], runtime[j]))
                {
                    if (i < j)
                    {
                        indistinct.Add(new IndistinctPair(j, types[i].ToDisplayString(), types[j].ToDisplayString()));
                    }

                    continue;
                }

                if (IsInstanceOf(compilation, runtime[i], runtime[j]) && !IsInstanceOf(compilation, runtime[j], runtime[i]))
                {
                    narrower[i]++;
                }
            }
        }

        return Enumerable.Range(0, runtime.Length).OrderByDescending(i => narrower[i]).ThenBy(i => i).ToArray();
    }

    /// <summary>Location of the i-th type argument of [Union&lt;...&gt;]; the type's name when it cannot be found.</summary>
    private static LocationInfo ArgumentLocation(AttributeSyntax? attribute, int index, LocationInfo fallback)
    {
        var name = attribute?.Name is QualifiedNameSyntax qualified ? qualified.Right : attribute?.Name;
        if (name is GenericNameSyntax generic && index < generic.TypeArgumentList.Arguments.Count)
        {
            return LocationInfo.From(generic.TypeArgumentList.Arguments[index].GetLocation());
        }

        return fallback;
    }

    private static string Fqn(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static CaseInfo BuildCase(ITypeSymbol type, Compilation compilation, ISymbol within)
    {
        var values = new List<LeafInfo>();
        var refs = new List<LeafInfo>();
        // C# does not allow a user-defined conversion from object, from interfaces or from pointers.
        var noImplicit = type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType or SpecialType.System_Enum
            || type.TypeKind is TypeKind.Interface or TypeKind.Pointer or TypeKind.FunctionPointer or TypeKind.Dynamic or TypeKind.TypeParameter;

        // A value of this case can be null: a reference type or Nullable<T>.
        var canBeNull = !type.IsValueType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        // What a runtime type test has to ask for: Nullable<T> boxes as T.
        var testType = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? Fqn(nullable.TypeArguments[0])
            : Fqn(type);

        var reason = new Reason();
        if (!Flatten(type, new List<AccessStep>(), values, refs, compilation, within, reason))
        {
            // Cannot be split: stored as is, in a field of its own.
            return new CaseInfo(
                Fqn(type),
                CaseName(type),
                new EquatableArray<LeafInfo>(System.Array.Empty<LeafInfo>()),
                new EquatableArray<LeafInfo>(System.Array.Empty<LeafInfo>()),
                IsOpaque: true,
                NoImplicit: noImplicit,
                CanBeNull: canBeNull,
                TestType: testType,
                FallbackReason: reason.Text ?? "it cannot be split into values and references");
        }

        return new CaseInfo(
            Fqn(type),
            CaseName(type),
            new EquatableArray<LeafInfo>(values.ToArray()),
            new EquatableArray<LeafInfo>(refs.ToArray()),
            IsOpaque: false,
            NoImplicit: noImplicit,
            CanBeNull: canBeNull,
            TestType: testType);
    }

    /// <summary>Why a case could not be overlapped; shown to the user as an informational message.</summary>
    private sealed class Reason
    {
        public string? Text;
    }

    private static bool Fail(Reason reason, string text)
    {
        reason.Text ??= text;
        return false;
    }

    /// <summary>
    /// Splits a type into leaves. An unmanaged type is one value leaf, a reference type is one reference leaf,
    /// a struct that mixes both is split field by field (private fields included).
    /// Returns false when the type cannot be split: a generic struct that mixes both
    /// (UnsafeAccessor does not reach into generic types).
    /// </summary>
    private static bool Flatten(
        ITypeSymbol type, List<AccessStep> path, List<LeafInfo> values, List<LeafInfo> refs, Compilation compilation, ISymbol within, Reason reason)
    {
        // The generated code names every type on the way to a leaf, so each of them must be visible from the union:
        // a private nested type or a compiler-generated fixed buffer is not.
        if (path.Count > 0 && (!compilation.IsSymbolAccessibleWithin(type, within) || type.Name.IndexOf('<') >= 0))
        {
            return Fail(reason, "it contains '" + type.ToDisplayString() + "', which is not accessible from the union (or is generated by the compiler)");
        }

        // A union that is a case of another union: the code generated for it is not part of the compilation this generator
        // sees, so to the compiler it is an empty struct. What it holds is known from its own attribute.
        if (TryGetUnionAttribute(type, out var innerUnion))
        {
            if (UnionHoldsOnlyValues(innerUnion, 0))
            {
                values.Add(new LeafInfo(Fqn(type), new EquatableArray<AccessStep>(path.ToArray())));
                return true;
            }

            return Fail(reason, "it is a union that holds references (the code generated for it is private to it)");
        }

        // Some reference assemblies (netstandard) hide the private fields of BCL structs. Such a struct looks empty,
        // so the compiler calls it unmanaged although it may hold references; overlapping it would crash the type load.
        if (ContainsHiddenFields(type, 0))
        {
            return Fail(reason, "its fields are hidden by the reference assemblies and it may hold references");
        }

        var leaf = new LeafInfo(
            Fqn(type),
            new EquatableArray<AccessStep>(path.ToArray()),
            type is IPointerTypeSymbol or IFunctionPointerTypeSymbol);
        if (type.IsUnmanagedType)
        {
            values.Add(leaf);
            return true;
        }

        if (type.IsReferenceType)
        {
            refs.Add(leaf);
            return true;
        }

        // Reference assemblies of the BCL replace private fields of structs with placeholders
        // (_dummy, _dummyPrimitive); the real fields are not visible, so such a type cannot be split.
        if (HasPlaceholderFields(type))
        {
            return Fail(reason, "its real fields are hidden by the reference assemblies (placeholders only)");
        }

        // A struct made of references only needs no splitting: it overlaps with other references as a whole.
        if (IsAllReferences(type))
        {
            refs.Add(leaf);
            return true;
        }

        if (type is INamedTypeSymbol named)
        {
            for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
            {
                if (current.IsGenericType)
                {
                    return Fail(reason, "it is a generic struct that mixes values and references (UnsafeAccessor cannot reach into generic types)");
                }
            }
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                continue;
            }

            // A fixed-size buffer is a pointer for the compiler and needs an unsafe context to be touched.
            if (field.IsFixedSizeBuffer)
            {
                return Fail(reason, "it has a fixed-size buffer next to references (the buffer's type has no name to write in code)");
            }

            path.Add(new AccessStep(Fqn(type), field.Name, Fqn(field.Type)));
            var ok = Flatten(field.Type, path, values, refs, compilation, within, reason);
            path.RemoveAt(path.Count - 1);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The [Union&lt;...&gt;] attribute of a type, if it has one.</summary>
    private static bool TryGetUnionAttribute(ITypeSymbol type, out AttributeData attribute)
    {
        foreach (var data in type.GetAttributes())
        {
            if (data.AttributeClass is { IsGenericType: true, Name: "UnionAttribute" } candidate
                && candidate.ContainingNamespace.ToDisplayString() == AttributeSource.Namespace)
            {
                attribute = data;
                return true;
            }
        }

        attribute = null!;
        return false;
    }

    /// <summary>
    /// True when a union holds no references: every case is plain values and the layout does not box.
    /// Then the union is itself a plain struct of numbers and may overlap with other values.
    /// </summary>
    private static bool UnionHoldsOnlyValues(AttributeData attribute, int depth)
    {
        if (depth > 8 || attribute.AttributeClass is null)
        {
            return false;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Layout" && argument.Value.Value is 2)
            {
                return false;
            }
        }

        foreach (var caseType in attribute.AttributeClass.TypeArguments)
        {
            if (TryGetUnionAttribute(caseType, out var inner))
            {
                if (!UnionHoldsOnlyValues(inner, depth + 1))
                {
                    return false;
                }
            }
            else if (!caseType.IsUnmanagedType || ContainsHiddenFields(caseType, depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    private enum RefState
    {
        /// <summary>Known to hold no references.</summary>
        None,

        /// <summary>Holds a reference, or it could not be established that it does not.</summary>
        Some,
    }

    /// <summary>
    /// The fields of a struct from a reference assembly are not always visible to the compiler (netstandard hides all of them),
    /// so for such a struct the generator asks the runtime it is running in: the real System.Private.CoreLib / mscorlib has them.
    /// Whether a BCL struct holds references does not depend on the runtime, so the answer carries over to the one that runs
    /// the compiled code. If the type cannot be found or inspected, the answer is Some: the struct is then kept as it is.
    /// </summary>
    private static RefState ClassifyHidden(INamedTypeSymbol hidden, int depth)
    {
        var type = ResolveRuntimeType(hidden.OriginalDefinition);
        if (type is null || !type.IsValueType || depth > 8)
        {
            return RefState.Some;
        }

        return ClassifyRuntimeStruct(type, hidden.TypeArguments, depth + 1);
    }

    private static RefState ClassifyRuntimeStruct(System.Type type, System.Collections.Immutable.ImmutableArray<ITypeSymbol> arguments, int depth)
    {
        if (depth > 8)
        {
            return RefState.Some;
        }

        const System.Reflection.BindingFlags all =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        foreach (var field in type.GetFields(all))
        {
            var fieldType = field.FieldType;
            if (fieldType.IsGenericParameter)
            {
                // A parameter of this struct: the matching type argument decides.
                if (fieldType.DeclaringMethod is not null || fieldType.GenericParameterPosition >= arguments.Length
                    || ClassifySymbol(arguments[fieldType.GenericParameterPosition], depth + 1) != RefState.None)
                {
                    return RefState.Some;
                }
            }
            else if (fieldType.IsPointer || fieldType.IsPrimitive || fieldType.IsEnum)
            {
                continue;
            }
            else if (!fieldType.IsValueType || fieldType.IsGenericType)
            {
                // A class, interface, array or delegate holds a reference; a nested generic struct is not worked out.
                return RefState.Some;
            }
            else if (ClassifyRuntimeStruct(fieldType, System.Collections.Immutable.ImmutableArray<ITypeSymbol>.Empty, depth + 1) != RefState.None)
            {
                return RefState.Some;
            }
        }

        return RefState.None;
    }

    private static RefState ClassifySymbol(ITypeSymbol type, int depth)
        => type.IsReferenceType || !type.IsUnmanagedType || ContainsHiddenFields(type, depth) ? RefState.Some : RefState.None;

    /// <summary>The type as the running runtime knows it: System.Private.CoreLib first, then every loaded assembly.</summary>
    private static System.Type? ResolveRuntimeType(INamedTypeSymbol definition)
    {
        var name = RuntimeName(definition);
        var type = typeof(object).Assembly.GetType(name, throwOnError: false);
        if (type is not null)
        {
            return type;
        }

        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                type = assembly.GetType(name, throwOnError: false);
            }
            catch (System.Exception)
            {
                continue;
            }

            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static string RuntimeName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } outer)
        {
            return RuntimeName(outer) + "+" + type.MetadataName;
        }

        var ns = type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : string.Empty;
        return ns.Length > 0 ? ns + "." + type.MetadataName : type.MetadataName;
    }

    /// <summary>
    /// True when the type is, or contains, a struct from metadata whose fields cannot be seen (no instance fields at all)
    /// and which is not known, by looking at the runtime, to hold only numbers.
    /// Source structs and structs with visible fields are not affected.
    /// </summary>
    private static bool ContainsHiddenFields(ITypeSymbol type, int depth)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct, SpecialType: SpecialType.None } named || depth > 16)
        {
            return false;
        }

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return ContainsHiddenFields(named.TypeArguments[0], depth + 1);
        }

        var anyField = false;
        foreach (var member in named.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                continue;
            }

            anyField = true;
            if (ContainsHiddenFields(field.Type, depth + 1))
            {
                return true;
            }
        }

        if (anyField || !named.Locations.All(static l => l.IsInMetadata))
        {
            return false;
        }

        return ClassifyHidden(named, depth) != RefState.None;
    }

    private static bool HasPlaceholderFields(ITypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false } field && field.Name.StartsWith("_dummy", System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllReferences(ITypeSymbol type)
    {
        var any = false;
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false, IsConst: false } field)
            {
                continue;
            }

            any = true;
            if (field.Type.IsReferenceType)
            {
                continue;
            }

            if (field.Type.IsUnmanagedType || !IsAllReferences(field.Type))
            {
                return false;
            }
        }

        return any;
    }

    /// <summary>Type name -> case name: Circle, Int32, NullableInt32 (generic arguments are appended).</summary>
    private static string CaseName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return CaseName(array.ElementType) + "Array";
        }

        if (type is IPointerTypeSymbol pointer)
        {
            return CaseName(pointer.PointedAtType) + "Ptr";
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            var builder = new StringBuilder(named.Name);
            foreach (var argument in named.TypeArguments)
            {
                builder.Append(CaseName(argument));
            }

            return builder.ToString();
        }

        return type.Name;
    }

    private static bool HasModifier(MemberDeclarationSyntax node, SyntaxKind kind)
    {
        foreach (var modifier in node.Modifiers)
        {
            if (modifier.IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }

    private static string Keyword(TypeDeclarationSyntax type)
        => type is RecordDeclarationSyntax rec && rec.ClassOrStructKeyword.ValueText is { Length: > 0 } classOrStruct
            ? "record " + classOrStruct
            : type.Keyword.ValueText;
}
