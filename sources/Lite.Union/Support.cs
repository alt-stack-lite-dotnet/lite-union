#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace System.Runtime.CompilerServices
{
    // Polyfill: lets netstandard2.0 use records and init accessors.
    internal static class IsExternalInit
    {
    }
} 

namespace Lite.Union
{
    internal sealed record UnionModel(
        string? Namespace,
        EquatableArray<ContainerInfo> Containers,
        string Name,
        bool IsReadOnly,
        int Layout,
        bool NativeUnion,
        EquatableArray<int> CheckOrder,
        EquatableArray<CaseInfo> Cases,
        EquatableArray<DiagnosticInfo> Diagnostics)
    {
        public string HintName
        {
            get
            {
                var parts = new List<string>();
                if (Namespace is not null)
                {
                    parts.Add(Namespace);
                }

                foreach (var container in Containers)
                {
                    parts.Add(container.Name);
                }

                parts.Add(Name);
                return string.Join(".", parts) + ".Union.g.cs";
            }
        }
    }

    /// <summary>Containing type: keyword ("class", "struct", "record struct", ...) and name.</summary>
    internal sealed record ContainerInfo(string Keyword, string Name);

    /// <summary>
    /// One case: fully qualified type and the name used in generated members (IsX, TryGetX, ...).
    /// The case is split into leaf fields: value leaves (unmanaged) and reference leaves (GC-tracked),
    /// stored in two separate overlapped payloads.
    /// </summary>
    internal sealed record CaseInfo(string Type, string Name, EquatableArray<LeafInfo> Values, EquatableArray<LeafInfo> Refs, bool IsOpaque, bool IsBoxed = false, bool NoImplicit = false, bool CanBeNull = true, string TestType = "", string? FallbackReason = null)
    {
        /// <summary>True when a leaf is a pointer: the generated code then has to be unsafe.</summary>
        public bool NeedsUnsafe
        {
            get
            {
                foreach (var leaf in Values)
                {
                    if (leaf.IsPointer)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool NeedsAccessors
        {
            get
            {
                foreach (var leaf in Values)
                {
                    if (leaf.Path.Length > 0)
                    {
                        return true;
                    }
                }

                foreach (var leaf in Refs)
                {
                    if (leaf.Path.Length > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    /// <summary>A leaf: its type and the chain of fields leading to it (empty when the leaf is the whole case).</summary>
    internal sealed record LeafInfo(string Type, EquatableArray<AccessStep> Path, bool IsPointer = false);

    /// <summary>One field access: declaring type, field name (metadata name) and field type.</summary>
    internal sealed record AccessStep(string Owner, string Field, string FieldType);

    internal readonly record struct LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
    {
        public static LocationInfo From(Location location)
            => new(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);

        public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
    }

    /// <summary>Two cases that are the same runtime object; Index is the later one.</summary>
    internal sealed record IndistinctPair(int Index, string First, string Second);

    /// <summary>
    /// What is known about a union without the project settings. ExplicitLayout and ExplicitFallback are -1 when the
    /// attribute does not set them.
    /// </summary>
    internal sealed record UnionAnalysis(
        string? Namespace,
        EquatableArray<ContainerInfo> Containers,
        string Name,
        bool IsReadOnly,
        bool NativeUnion,
        int ExplicitLayout,
        int ExplicitFallback,
        EquatableArray<CaseInfo> Cases,
        EquatableArray<LocationInfo> ArgumentLocations,
        LocationInfo IdentifierLocation,
        EquatableArray<int> CheckOrder,
        EquatableArray<IndistinctPair> Indistinct,
        bool AllowUnsafe,
        EquatableArray<DiagnosticInfo> Diagnostics);

    /// <summary>
    /// Project-wide defaults from the MSBuild properties LiteUnionLayout and LiteUnionFallback.
    /// A value that is not understood is kept so that it can be reported.
    /// </summary>
    internal sealed record GlobalSettings(int? Layout, int? Fallback, string? InvalidName, string? InvalidValue, string? InvalidAllowed)
    {
        public static GlobalSettings From(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options)
        {
            string? invalidName = null, invalidValue = null, invalidAllowed = null;

            int? Read(string property, string[] names)
            {
                if (!options.TryGetValue("build_property." + property, out var text) || string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                for (var i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], text.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                if (invalidName is null)
                {
                    invalidName = property;
                    invalidValue = text.Trim();
                    invalidAllowed = string.Join(", ", names);
                }

                return null;
            }

            var layout = Read("LiteUnionLayout", new[] { "Overlap", "Flat", "Boxed" });
            var fallback = Read("LiteUnionFallback", new[] { "Field", "Boxed" });
            return new GlobalSettings(layout, fallback, invalidName, invalidValue, invalidAllowed);
        }
    }

    internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo Loc, string Argument, string Argument2 = "", string Argument3 = "")
    {
        public Diagnostic ToDiagnostic() => Diagnostic.Create(Descriptor, Loc.ToLocation(), Argument, Argument2, Argument3);
    }

    internal static class Descriptors
    {
        private const string Category = "Lite.Union";

        public static readonly DiagnosticDescriptor NotPartial = new(
            "LUNION001",
            "Union type must be partial",
            "'{0}' and all of its containing types must be declared partial to use [Union<...>]",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateCase = new(
            "LUNION002",
            "Duplicate Union case name",
            "More than one type in [Union<...>] produces the case name '{0}'",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor RecordStruct = new(
            "LUNION004",
            "Union type must not be a record struct",
            "'{0}' must be a plain struct: a record struct synthesizes members that conflict with the generated ones",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor Indistinguishable = new(
            "LUNION006",
            "Cases cannot be told apart",
            "'{0}' and '{1}' cannot be told apart in Layout = Boxed: a value of either is the same object. Use Layout = Overlap or Flat, which keep a tag.",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidSetting = new(
            "LUNION007",
            "Unknown project setting",
            "The value '{1}' of <{0}> is not understood; expected one of: {2}. The default is used.",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor StoredAsIs = new(
            "LUNION100",
            "Case is not overlapped",
            "Case '{0}' is stored {1} instead of overlapping with the others: {2}",
            Category,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NeedsUnsafe = new(
            "LUNION005",
            "Unsafe code is required",
            "'{0}' contains pointers, so the generated code has to be unsafe; enable AllowUnsafeBlocks in the project",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericContainer = new(
            "LUNION003",
            "Union type must not be generic",
            "'{0}' must not be generic or nested in a generic type",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }

    /// <summary>Array with value equality, required for incremental generator caching.</summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    {
        private readonly T[]? _items;

        public EquatableArray(T[] items) => _items = items;

        public int Length => _items?.Length ?? 0;

        public T this[int index] => _items![index];

        public bool Equals(EquatableArray<T> other)
        {
            var left = _items ?? Array.Empty<T>();
            var right = other._items ?? Array.Empty<T>();
            if (left.Length != right.Length)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var i = 0; i < left.Length; i++)
            {
                if (!comparer.Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var comparer = EqualityComparer<T>.Default;
            var hash = 17;
            foreach (var item in _items ?? Array.Empty<T>())
            {
                hash = unchecked(hash * 31 + comparer.GetHashCode(item));
            }

            return hash;
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
