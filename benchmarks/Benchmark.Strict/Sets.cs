using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bench;

// The four sets of case types every union library is measured with. Each set has eight types; a union of 3 cases uses
// the first three, a union of 8 cases uses all of them. Values are static readonly fields (not constants), so the
// compiler cannot fold them, and the same in every adapter.

// ----- Values: numbers only -----
public static class Values
{
    public static readonly int C0 = 12345;
    public static readonly long C1 = 9876543210L;
    public static readonly double C2 = 3.14159;
    public static readonly Guid C3 = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");
    public static readonly byte C4 = 200;
    public static readonly short C5 = -1234;
    public static readonly float C6 = 2.5f;
    public static readonly DateTime C7 = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
}

// ----- Refs: references only -----
public sealed class RefA { public int X; }
public record class RefB(int Id, string Name);
public sealed class RefC { public string S = ""; public long L; }
public sealed class RefD { public double D; }
public sealed class RefE { public int A, B, C; }

public static class Refs
{
    public static readonly string C0 = "hello world";
    public static readonly RefA C1 = new() { X = 7 };
    public static readonly RefB C2 = new(8, "bee");
    public static readonly RefC C3 = new() { S = "see", L = 9 };
    public static readonly RefD C4 = new() { D = 10.5 };
    public static readonly int[] C5 = { 1, 2, 3 };
    public static readonly List<int> C6 = new() { 4, 5 };
    public static readonly RefE C7 = new() { A = 1, B = 2, C = 3 };
}

// ----- Mixed: structs that hold numbers and references -----
public struct MixA { public int I; public string S; }
public struct MixB { public string S; public long L; }
public struct MixC { public object? O; public int I; }
public struct MixD { public long L; public string S; public int I; }
public struct MixE { public string S1; public string S2; }
public struct MixF { public double D; public object? O; }
public struct MixG { public byte B; public int I; public string S; }
public struct MixH { public Guid G; public string S; }

public static class Mixed
{
    public static readonly MixA C0 = new() { I = 1, S = "one" };
    public static readonly MixB C1 = new() { S = "two", L = 2 };
    public static readonly MixC C2 = new() { O = "three", I = 3 };
    public static readonly MixD C3 = new() { L = 4, S = "four", I = 40 };
    public static readonly MixE C4 = new() { S1 = "five", S2 = "5" };
    public static readonly MixF C5 = new() { D = 6.5, O = "six" };
    public static readonly MixG C6 = new() { B = 7, I = 70, S = "seven" };
    public static readonly MixH C7 = new() { G = new Guid("11111111-2222-3333-4444-555555555555"), S = "eight" };
}

// ----- Big: 64 bytes per case (the first, second and fourth to sixth are numbers only, the others hold a string) -----
public struct BigN1 { public long A, B, C, D, E, F, G, H; }
public struct BigN2 { public long A, B, C, D, E, F, G, H; }
public struct BigM1 { public long A, B, C, D, E, F, G; public string S; }
public struct BigN3 { public long A, B, C, D, E, F, G, H; }
public struct BigN4 { public long A, B, C, D, E, F, G, H; }
public struct BigN5 { public long A, B, C, D, E, F, G, H; }
public struct BigM2 { public long A, B, C, D, E, F, G; public string S; }
public struct BigM3 { public long A, B, C, D, E, F, G; public string S; }

public static class Big
{
    public static readonly BigN1 C0 = new() { A = 1, H = 10 };
    public static readonly BigN2 C1 = new() { A = 2, H = 20 };
    public static readonly BigM1 C2 = new() { A = 3, G = 30, S = "big three" };
    public static readonly BigN3 C3 = new() { A = 4, H = 40 };
    public static readonly BigN4 C4 = new() { A = 5, H = 50 };
    public static readonly BigN5 C5 = new() { A = 6, H = 60 };
    public static readonly BigM2 C6 = new() { A = 7, G = 70, S = "big seven" };
    public static readonly BigM3 C7 = new() { A = 8, G = 80, S = "big eight" };
}

/// <summary>A number made from a case payload. The same function is used by every library, so results can be compared.</summary>
public static class Sums
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(int v) => v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(long v) => v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(double v) => (long)(v * 1000);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(Guid v) => v.GetHashCode();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(byte v) => v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(short v) => v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(float v) => (long)(v * 1000);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(DateTime v) => v.Ticks % 1_000_003;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(string v) => v.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(RefA v) => v.X;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(RefB v) => v.Id + v.Name.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(RefC v) => v.L + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(RefD v) => (long)v.D;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(int[] v) => v.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(List<int> v) => v.Count;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(RefE v) => v.A + v.B + v.C;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixA v) => v.I + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixB v) => v.L + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixC v) => v.I + (v.O is null ? 0 : 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixD v) => v.L + v.I + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixE v) => v.S1.Length + v.S2.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixF v) => (long)v.D + (v.O is null ? 0 : 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixG v) => v.B + v.I + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(MixH v) => v.G.GetHashCode() + v.S.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigN1 v) => v.A + v.H;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigN2 v) => v.A + v.H;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigM1 v) => v.A + v.G + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigN3 v) => v.A + v.H;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigN4 v) => v.A + v.H;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigN5 v) => v.A + v.H;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigM2 v) => v.A + v.G + v.S.Length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static long Of(BigM3 v) => v.A + v.G + v.S.Length;
}
