using System;
using System.Runtime.CompilerServices;

static class Extra
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long AbcNoConcat(Abc u) => u switch { A a => a.I, B b => b.L, C c => c.I };

    public static void Run()
    {
        Abc a = new A { I = 1, S = "a" }; Abc b = new B { S = "b", L = 2 }; Abc c = new C { O = "o", I = 3 };
        for (var w = 0; w < 1000; w++) { AbcNoConcat(a); AbcNoConcat(b); AbcNoConcat(c); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var w = 0; w < 100_000; w++) sum += AbcNoConcat(a) + AbcNoConcat(b) + AbcNoConcat(c);
        Console.WriteLine("allocated by 300000 switches over Abc (no concat): " + (GC.GetAllocatedBytesForCurrentThread() - before) + " B, sum=" + sum);
        Console.WriteLine("sizeof(Abc) = " + Unsafe.SizeOf<Abc>());
    }
}
