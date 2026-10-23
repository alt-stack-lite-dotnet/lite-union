#if NET11_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Lite.Union.Test;

// The same unions as in the other tests; on .NET 11 they are also C# 15 unions.
public class NativeUnionTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Describe(UAbc u) => u switch { A a => a._i, B b => b._l, C c => c._i };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DescribeMixed(UMixed u) => u switch { Mixed1 m => m.I, Mixed2 m => m.D > 0 ? 2 : 0, ReadOnlyMixed r => r.A, Rec r => r.Id };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long DescribeBoxed(UAbcBoxed u) => u switch { A a => a._i, B b => b._l, C c => c._i };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long DescribeFlat(UAbcFlat u) => u switch { A a => a._i, B b => b._l, C c => c._i };

    [Fact]
    public void Switch_expressions_pick_the_right_case_without_a_default_arm()
    {
        UAbc a = new A { _i = 1, _s = "a" };
        UAbc b = new B { _s = "b", _l = 2 };
        UAbc c = new C { _o = "c", _i = 3 };
        Assert.Equal(1, Describe(a));
        Assert.Equal(2, Describe(b));
        Assert.Equal(3, Describe(c));

        Assert.Equal(7, DescribeMixed(new Mixed1 { I = 7, S = "x" }));
        Assert.Equal(2, DescribeMixed(new Mixed2(1, "o", 2, 3, 4.5, "t")));
        Assert.Equal(9, DescribeMixed(new ReadOnlyMixed(9, "r")));
        Assert.Equal(5, DescribeMixed(new Rec("n", 5)));
        Assert.Equal(8, DescribeBoxed(new A { _i = 8 }));
        Assert.Equal(6, DescribeFlat(new B { _l = 6 }));
    }

    [Fact]
    public void Is_patterns_work()
    {
        UAbc u = new B { _s = "b", _l = 2 };
        Assert.True(u is B { _l: 2 });
        Assert.False(u is A);
        Assert.True(u is not C);
    }

    [Fact]
    public void Creation_works_through_constructors_and_conversions()
    {
        var viaCtor = new UAbc(new A { _i = 4 });
        UAbc viaConversion = new A { _i = 4 };
        Assert.Equal(viaCtor, viaConversion);
    }

    [Fact]
    public void Value_boxes_the_case_and_is_null_for_default()
    {
        UAbc u = new A { _i = 4, _s = "s" };
        var value = ((IUnion)u).Value;
        Assert.IsType<A>(value);
        Assert.Equal(4, ((A)value!)._i);

        Assert.Null(((IUnion)default(UAbc)).Value);
        Assert.False(default(UAbc).HasValue);
        Assert.True(u.HasValue);
    }

    [Fact]
    public void A_null_reference_counts_as_no_value()
    {
        URefs s = (string)null!;
        Assert.False(s.HasValue);
        Assert.Null(((IUnion)s).Value);
        Assert.False(s.TryGetValue(out string? text));
        Assert.Null(text);

        URefs filled = "x";
        Assert.True(filled.HasValue);
        Assert.True(filled.TryGetValue(out string? t) && t == "x");
    }

    [Fact]
    public void A_null_nullable_value_counts_as_no_value()
    {
        UValues none = (int?)null;
        UValues some = (int?)5;
        Assert.False(none.HasValue);
        Assert.Null(((IUnion)none).Value);
        Assert.True(some.HasValue);
        Assert.True(some.TryGetValue(out int? n) && n == 5);
    }

    [Fact]
    public void TryGetValue_is_true_only_for_the_held_case()
    {
        UAbc u = new B { _s = "b", _l = 2 };
        Assert.False(u.TryGetValue(out A _));
        Assert.True(u.TryGetValue(out B b));
        Assert.Equal(2L, b._l);
        Assert.False(u.TryGetValue(out C _));
    }

    [Fact]
    public void Switching_does_not_allocate()
    {
        UAbc a = new A { _i = 1, _s = "a" };
        UAbc b = new B { _s = "b", _l = 2 };
        UAbc c = new C { _o = "o", _i = 3 };
        UMixed m = new Mixed1 { I = 1, S = "s" };
        UMixed r = new Rec("n", 2);
        UAbcBoxed boxed = new A { _i = 1 };
        UAbcFlat flat = new B { _l = 2 };

        for (var i = 0; i < 2000; i++)
        {
            Describe(a); Describe(b); Describe(c); DescribeMixed(m); DescribeMixed(r); DescribeFlat(flat); DescribeBoxed(boxed);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var i = 0; i < 100_000; i++)
        {
            sum += Describe(a) + Describe(b) + Describe(c) + DescribeMixed(m) + DescribeMixed(r) + DescribeFlat(flat);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);

        // Boxed storage keeps value cases in an object: matching them unboxes, it does not allocate either.
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            sum += DescribeBoxed(boxed);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
#endif
