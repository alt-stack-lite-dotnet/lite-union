namespace Bench;

/// <summary>
/// One union library (or one layout of it) over one set of types and one number of cases. Implemented by an empty struct
/// so that the generic workloads are specialized by the JIT for each combination.
/// </summary>
public interface IAdapter<TUnion>
{
    /// <summary>How many cases the union has (3 or 8).</summary>
    static abstract int Cases { get; }

    /// <summary>Size of one value in an array: the struct size, or a reference for a class union.</summary>
    static abstract int ByteSize { get; }

    /// <summary>Makes a union holding the value of case <paramref name="index"/> of the set.</summary>
    static abstract TUnion Create(int index);

    /// <summary>Takes the union apart the way the library does it and returns a number made from the payload.</summary>
    static abstract long Match(TUnion union);

    /// <summary>What <see cref="Match"/> must return for case <paramref name="index"/>, worked out from the set directly.</summary>
    static abstract long Expected(int index);

    /// <summary>The default equality of the union type.</summary>
    static abstract bool Equal(TUnion a, TUnion b);
}
