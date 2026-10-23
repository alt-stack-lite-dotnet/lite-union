using System;
using System.Collections.Generic;
using System.Threading;
using Lite.Union;

namespace Lite.Union.Test;

// ----- case types -----
public class K { public int X; public override string ToString() => "K" + X; }
public enum Color : byte { Red, Green, Blue }
public struct Empty { }

public struct A { public int _i; public string _s; }
public struct B { public string _s; public long _l; }
public struct C { public object _o; public int _i; }

public struct Mixed1 { public int I; public string S; }
public struct Mixed2 { private byte _b; private object _o; private short _s; public long L; public double D; private string _t;
    public Mixed2(byte b, object o, short s, long l, double d, string t) { _b = b; _o = o; _s = s; L = l; D = d; _t = t; }
    public byte B => _b; public object O => _o; public short S => _s; public string T => _t; }
public readonly struct ReadOnlyMixed { public readonly int A; public readonly string B; public ReadOnlyMixed(int a, string b) { A = a; B = b; } }
public readonly record struct Rec(string Name, int Id);
public readonly record struct RefRec(string A, object B);
public struct Nested { public Mixed1 Inner; public long X; public string Y; }
public struct Deep { public Nested N; public ReadOnlyMixed R; public Color C; }
public struct WithEnum { public Color C; public string S; }
public struct WithFixed { public unsafe fixed int Buf[4]; public string S; }
public struct WithPrivateNested
{
    private struct Hidden { public int A; }
    private Hidden _h;
    public string S;
    public WithPrivateNested(int a, string s) { _h = new Hidden { A = a }; S = s; }
    public int Value => _h.A;
}
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public struct ExplicitNumbers { [System.Runtime.InteropServices.FieldOffset(0)] public int I; [System.Runtime.InteropServices.FieldOffset(0)] public float F; }
public struct WithExplicit { public ExplicitNumbers N; public string S; }

// ----- unions -----
[Union<Mixed1, Mixed2, ReadOnlyMixed, Rec>] public partial struct UMixed;
[Union<Nested, Deep, WithEnum, Empty>] public partial struct UNested;
[Union<int?, double, Guid, decimal, DateTime, Color>] public partial struct UValues;
[Union<string, K, int[], object>] public partial struct URefs;
[Union<RefRec, Rec, int>] public partial struct URefStructs;
[Union<(int, string), KeyValuePair<string, int>, CancellationToken, int>] public partial struct UOpaque;
[Union<WithFixed, WithPrivateNested, WithExplicit, int>] public partial struct UOdd;

[Union<A, B, C>] public partial struct UAbc;
[Union<A, B, C>(Layout = UnionLayout.Flat)] public partial struct UAbcFlat;
[Union<A, B, C>(Layout = UnionLayout.Boxed)] public partial struct UAbcBoxed;

[Union<byte, sbyte, short, ushort, int, uint, long, ulong, float, double, char, bool, string, object, K, Mixed1>]
public partial struct USixteen;

public partial struct Outer
{
    public partial struct Inner
    {
        [Union<int, string>] public partial struct Nested2;
    }
}

public unsafe struct OnlyPointers { public int* P; public long L; }
public unsafe struct WithPointer { public int* P; public string S; public int N; }
[Union<OnlyPointers, WithPointer, int>] public partial struct UPointers;

// ----- more shapes -----
public enum BigEnum : long { One = 1, Huge = 1L << 40 }
[Flags] public enum Flags : ushort { None = 0, A = 1, B = 2 }
public struct Holder { public KeyValuePair<string, int> Kv; public int X; }
public struct HolderWithTuple { public (int, int) Pair; public string S; }
public struct Big
{
    public int A1, A2, A3, A4, A5, A6, A7, A8; public string S1, S2, S3; public long L1, L2; public object? O1; public byte B1; public double D1;
}
public struct NullableRefs { public string? S; public object? O; public int? N; }
public delegate int Calc(int x);
public interface IThing { int Id { get; } }
public class Thing : IThing { public int Id { get; set; } }
public struct ThingStruct : IThing { public int Id { get; set; } public string Label; }

[Union<Holder, HolderWithTuple, int>] public partial struct UHolders;
[Union<Big, NullableRefs, long>] public partial struct UBig;
[Union<BigEnum, Flags, Color>] public partial struct UEnums;
[Union<IThing, Calc, Action, Delegate, Enum, ValueType>] public partial struct UInterfaces;
[Union<ThingStruct, Thing, int>] public partial struct UImplementers;
[Union<int[], long[], string[], int[][]>] public partial struct UArrays;
[Union<Mixed1, Rec, string>(Layout = UnionLayout.Boxed)] public partial struct UBoxedMixed;
[Union<Mixed1, Rec, string>(Layout = UnionLayout.Flat)] public partial struct UFlatMixed;

public struct MutableUnionHost { public int Before; }
[Union<int, string>] public partial struct UMutable;       // not readonly


// ----- rarer shapes -----
public struct SelfRefs { public string A; public string B; }

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
public struct ExplicitWithReference
{
    [System.Runtime.InteropServices.FieldOffset(0)] public string S;      // a reference at offset 0
    [System.Runtime.InteropServices.FieldOffset(8)] public long L;
    [System.Runtime.InteropServices.FieldOffset(8)] public double D;      // overlaps L
}

public struct MixedNullableHolder { public Mixed1? Maybe; public int X; }
public struct TupleHolder { public (int Id, string Name) Pair; public long L; }

// a union as a case of another union
[Union<Mixed1, Rec>] public partial struct UInner;
[Union<UInner, string, int>] public partial struct UOuter;
[Union<UOuter, UInner, long>] public partial struct UOuterOuter;

[Union<ExplicitWithReference, int>] public partial struct UExplicit;
[Union<Mixed1?, int>] public partial struct UNullableMixed;
[Union<MixedNullableHolder, TupleHolder, (int, string)>] public partial struct UHolders2;
[Union<SelfRefs, string>] public partial struct URefPair;

[Union<int, long>] public partial struct UInnerNumbers;
[Union<UInnerNumbers, double, string>] public partial struct UOuterNumbers;

// ----- tagless Boxed, fallback -----
public class Animal { }
public class Dog : Animal { }
public class Cat : Animal { }

[Union<Animal, Dog, object>(Layout = UnionLayout.Boxed)] public partial struct UAnimals;
[Union<IThing, Thing, string>(Layout = UnionLayout.Boxed)] public partial struct UThings;
[Union<int, long, double>(Layout = UnionLayout.Boxed)] public partial struct UBoxedNumbers;
[Union<int?, string>(Layout = UnionLayout.Boxed)] public partial struct UBoxedNullable;

[Union<Holder, int>] public partial struct UFallbackField;                                               // default: a field of its own
[Union<Holder, int>(Fallback = UnionFallback.Boxed)] public partial struct UFallbackBoxed;
[Union<Holder, long, string, Mixed1>(Fallback = UnionFallback.Boxed)] public partial struct UFallbackMixture;
