using System;
using System.Runtime.CompilerServices;
using Lite.Union;

public struct A { public int I; public string S; }
public struct B { public string S; public long L; }
public struct C { public object O; public int I; }
public class K { public int X; }

[Union<A, B, C>] public partial struct Abc { }
[Union<int, long, double>] public partial struct Num { }
[Union<string, K, int>] public partial struct Mix { }

class P
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static string Describe(Abc u) => u switch { A a => "A" + a.I + a.S, B b => "B" + b.L + b.S, C c => "C" + c.I + c.O };

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int NumSwitch(Num n) => n switch { int i => i, long l => (int)l, double d => (int)d };

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int MixSwitch(Mix m) => m switch { string s => s.Length, K k => k.X, int i => i };

    static void Main()
    {
        Abc a = new A { I = 1, S = "a" };
        Abc b = new B { S = "b", L = 2 };
        Abc c = new C { O = "o", I = 3 };
        Extra.Run(); Console.WriteLine(Describe(a) + " " + Describe(b) + " " + Describe(c));

        Num n = 7; Mix m = "hello"; Mix k = new K { X = 4 }; Mix i = 9;
        Console.WriteLine(NumSwitch(n) + " " + MixSwitch(m) + " " + MixSwitch(k) + " " + MixSwitch(i));
        Console.WriteLine("Value boxed: " + n.Value + ", HasValue: " + n.HasValue + ", default HasValue: " + default(Num).HasValue);
        Mix nullString = (string)null!;
        Console.WriteLine("null string -> HasValue: " + nullString.HasValue + ", Value null: " + (nullString.Value is null));

        for (var w = 0; w < 1000; w++) { Describe(a); NumSwitch(n); MixSwitch(m); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var w = 0; w < 100_000; w++) { NumSwitch(n); MixSwitch(m); MixSwitch(k); MixSwitch(i); }
        Console.WriteLine("allocated by 400000 switches over Num/Mix: " + (GC.GetAllocatedBytesForCurrentThread() - before) + " B");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var w = 0; w < 100_000; w++) { Describe(a); Describe(b); Describe(c); }
        var abcBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine("allocated by 300000 switches over Abc (includes string concat in Describe): " + abcBytes + " B");
    }
}
