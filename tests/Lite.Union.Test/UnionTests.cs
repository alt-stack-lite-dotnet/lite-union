using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace Lite.Union.Test;

public class RoundtripTests
{
    [Fact]
    public void Mixed_structs_come_back_intact()
    {
        UMixed m1 = new Mixed1 { I = 7, S = "seven" };
        UMixed m2 = new Mixed2(1, "obj", 2, 3L, 4.5, "tee");
        UMixed ro = new ReadOnlyMixed(9, "nine");
        UMixed rec = new Rec("name", 42);

        Assert.Equal(7, m1.GetMixed1().I);
        Assert.Equal("seven", m1.GetMixed1().S);

        var back = m2.GetMixed2();
        Assert.Equal((byte)1, back.B);
        Assert.Equal("obj", back.O);
        Assert.Equal((short)2, back.S);
        Assert.Equal(3L, back.L);
        Assert.Equal(4.5, back.D);
        Assert.Equal("tee", back.T);

        Assert.Equal(9, ro.GetReadOnlyMixed().A);
        Assert.Equal("nine", ro.GetReadOnlyMixed().B);
        Assert.Equal(new Rec("name", 42), rec.GetRec());
    }

    [Fact]
    public void Nested_structs_come_back_intact()
    {
        UNested n = new Nested { Inner = new Mixed1 { I = 1, S = "in" }, X = 2, Y = "y" };
        UNested d = new Deep { N = new Nested { Inner = new Mixed1 { I = 3, S = "deep" }, X = 4, Y = "z" }, R = new ReadOnlyMixed(5, "r"), C = Color.Blue };
        UNested e = new WithEnum { C = Color.Green, S = "g" };
        UNested empty = new Empty();

        var nn = n.GetNested();
        Assert.Equal(1, nn.Inner.I);
        Assert.Equal("in", nn.Inner.S);
        Assert.Equal(2L, nn.X);
        Assert.Equal("y", nn.Y);

        var dd = d.GetDeep();
        Assert.Equal(3, dd.N.Inner.I);
        Assert.Equal("deep", dd.N.Inner.S);
        Assert.Equal(4L, dd.N.X);
        Assert.Equal("z", dd.N.Y);
        Assert.Equal(5, dd.R.A);
        Assert.Equal("r", dd.R.B);
        Assert.Equal(Color.Blue, dd.C);

        Assert.Equal(Color.Green, e.GetWithEnum().C);
        Assert.Equal("g", e.GetWithEnum().S);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void Value_cases_come_back_intact()
    {
        var guid = Guid.NewGuid();
        var now = DateTime.UtcNow;
        UValues a = (int?)5;
        UValues b = 2.5;
        UValues c = guid;
        UValues d = 12.34m;
        UValues e = now;
        UValues f = Color.Blue;
        UValues n = (int?)null;

        Assert.Equal(5, a.GetNullableInt32());
        Assert.Equal(2.5, b.GetDouble());
        Assert.Equal(guid, c.GetGuid());
        Assert.Equal(12.34m, d.GetDecimal());
        Assert.Equal(now, e.GetDateTime());
        Assert.Equal(Color.Blue, f.GetColor());
        Assert.True(n.IsNullableInt32);
        Assert.Null(n.GetNullableInt32());
    }

    [Fact]
    public void Reference_cases_come_back_intact_including_null()
    {
        var k = new K { X = 3 };
        var arr = new[] { 1, 2, 3 };
        object o = new object();
        URefs s = "str";
        URefs kk = k;
        URefs aa = arr;
        var oo = URefs.FromObject(o);
        URefs nullString = (string)null!;

        Assert.Equal("str", s.GetString());
        Assert.Same(k, kk.GetK());
        Assert.Same(arr, aa.GetInt32Array());
        Assert.Same(o, oo.GetObject());
        Assert.True(nullString.IsString);
        Assert.Null(nullString.GetString());
    }

    [Fact]
    public void Reference_only_structs_overlap_with_other_references()
    {
        URefStructs a = new RefRec("a", new object());
        URefStructs b = new Rec("b", 2);
        URefStructs c = 3;
        Assert.Equal("a", a.GetRefRec().A);
        Assert.Equal(new Rec("b", 2), b.GetRec());
        Assert.Equal(3, c.GetInt32());
    }

    [Fact]
    public void Generic_and_BCL_structs_are_kept_as_they_are()
    {
        UOpaque t = (1, "x");
        UOpaque kv = new KeyValuePair<string, int>("k", 2);
        UOpaque ct = CancellationToken.None;
        UOpaque i = 4;
        Assert.Equal((1, "x"), t.GetValueTupleInt32String());
        Assert.Equal("k", kv.GetKeyValuePairStringInt32().Key);
        Assert.Equal(2, kv.GetKeyValuePairStringInt32().Value);
        Assert.Equal(CancellationToken.None, ct.GetCancellationToken());
        Assert.Equal(4, i.GetInt32());
    }

    [Fact]
    public unsafe void Odd_structs_fall_back_instead_of_breaking()
    {
        var withFixed = new WithFixed { S = "fx" };
        withFixed.Buf[2] = 77;
        UOdd f = withFixed;
        UOdd p = new WithPrivateNested(5, "pn");
        UOdd e = new WithExplicit { N = new ExplicitNumbers { I = 0x3f800000 }, S = "ex" };

        var back = f.GetWithFixed();
        Assert.Equal(77, back.Buf[2]);
        Assert.Equal("fx", back.S);
        Assert.Equal(5, p.GetWithPrivateNested().Value);
        Assert.Equal("pn", p.GetWithPrivateNested().S);
        Assert.Equal(1.0f, e.GetWithExplicit().N.F);
        Assert.Equal("ex", e.GetWithExplicit().S);
    }

    [Fact]
    public void Sixteen_cases_work()
    {
        USixteen a = (byte)1;
        USixteen b = (sbyte)-2;
        USixteen c = (short)3;
        USixteen d = (ushort)4;
        USixteen e = 5;
        USixteen f = 6u;
        USixteen g = 7L;
        USixteen h = 8UL;
        USixteen i = 9f;
        USixteen j = 10.0;
        USixteen k = 'k';
        USixteen l = true;
        USixteen m = "m";
        var n = USixteen.FromObject(new object());
        USixteen o = new K { X = 15 };
        USixteen p = new Mixed1 { I = 16, S = "sixteen" };

        Assert.Equal((byte)1, a.GetByte());
        Assert.Equal((sbyte)-2, b.GetSByte());
        Assert.Equal((short)3, c.GetInt16());
        Assert.Equal((ushort)4, d.GetUInt16());
        Assert.Equal(5, e.GetInt32());
        Assert.Equal(6u, f.GetUInt32());
        Assert.Equal(7L, g.GetInt64());
        Assert.Equal(8UL, h.GetUInt64());
        Assert.Equal(9f, i.GetSingle());
        Assert.Equal(10.0, j.GetDouble());
        Assert.Equal('k', k.GetChar());
        Assert.True(l.GetBoolean());
        Assert.Equal("m", m.GetString());
        Assert.NotNull(n.GetObject());
        Assert.Equal(15, o.GetK().X);
        Assert.Equal(16, p.GetMixed1().I);
        Assert.Equal("sixteen", p.GetMixed1().S);
    }

    [Fact]
    public void Nested_in_partial_types_works()
    {
        Outer.Inner.Nested2 n = "x";
        Assert.Equal("x", n.GetString());
    }
}

public class ApiTests
{
    [Fact]
    public void Kind_Is_TryGet_and_Get_agree()
    {
        UAbc u = new B { _s = "b", _l = 2 };
        Assert.Equal(UAbc.CaseKind.B, u.Kind);
        Assert.True(u.IsB);
        Assert.False(u.IsA);
        Assert.False(u.IsC);
        Assert.True(u.TryGetB(out var b));
        Assert.Equal(2L, b._l);
        Assert.False(u.TryGetA(out _));
        Assert.Throws<InvalidOperationException>(() => u.GetA());
    }

    [Fact]
    public void Default_is_uninitialized()
    {
        UAbc u = default;
        Assert.True(u.IsUninitialized);
        Assert.Equal(UAbc.CaseKind.Uninitialized, u.Kind);
        Assert.Throws<InvalidOperationException>(() => u.Match(a => 0, b => 1, c => 2));
        Assert.Equal("Uninitialized", u.ToString());
    }

    [Fact]
    public void Match_and_Switch_pick_the_right_case()
    {
        UAbc a = new A { _i = 1, _s = "a" };
        UAbc b = new B { _s = "b", _l = 2 };
        UAbc c = new C { _o = "c", _i = 3 };
        Assert.Equal("A1a", a.Match(x => "A" + x._i + x._s, x => "B", x => "C"));
        Assert.Equal("B2b", b.Match(x => "A", x => "B" + x._l + x._s, x => "C"));
        Assert.Equal("C3c", c.Match(x => "A", x => "B", x => "C" + x._i + x._o));

        var seen = "";
        a.Switch(x => seen = "a", x => seen = "b", x => seen = "c");
        Assert.Equal("a", seen);
    }

    [Fact]
    public void Equality_is_by_case_and_value()
    {
        UAbc a1 = new A { _i = 1, _s = "a" };
        UAbc a2 = new A { _i = 1, _s = "a" };
        UAbc a3 = new A { _i = 2, _s = "a" };
        UAbc b = new B { _s = "a", _l = 1 };
        Assert.True(a1 == a2);
        Assert.True(a1.Equals(a2));
        Assert.True(a1.Equals((object)a2));
        Assert.Equal(a1.GetHashCode(), a2.GetHashCode());
        Assert.True(a1 != a3);
        Assert.True(a1 != b);
        Assert.True(default(UAbc) == default(UAbc));
        Assert.False(a1.Equals(null));
    }

    [Fact]
    public void ToString_names_the_case()
    {
        Assert.StartsWith("Double(", ((UValues)2.5).ToString());
        Assert.StartsWith("Color(", ((UValues)Color.Blue).ToString());
    }
}

public class LayoutTests
{
    [Fact]
    public void Flat_and_Boxed_behave_like_Overlap()
    {
        var a = new A { _i = 1, _s = "a" };
        var b = new B { _s = "b", _l = 2 };
        var c = new C { _o = "c", _i = 3 };
        UAbc o = b;
        UAbcFlat f = b;
        UAbcBoxed x = b;
        Assert.Equal(o.GetB()._l, f.GetB()._l);
        Assert.Equal(o.GetB()._l, x.GetB()._l);
        Assert.Equal("c", ((UAbcFlat)c).GetC()._o);
        Assert.Equal("c", ((UAbcBoxed)c).GetC()._o);
        Assert.Equal(1, ((UAbcFlat)a).GetA()._i);
        Assert.Equal(1, ((UAbcBoxed)a).GetA()._i);
        Assert.True((UAbcBoxed)b == (UAbcBoxed)b);
        Assert.True((UAbcBoxed)a != (UAbcBoxed)b);
        Assert.True(default(UAbcBoxed).IsUninitialized);
        Assert.True(default(UAbcFlat).IsUninitialized);
    }

    [Fact]
    public void Sizes_match_the_layout()
    {
        if (IntPtr.Size != 8)
        {
            return;
        }

        Assert.Equal(24, Unsafe.SizeOf<UAbc>());
        Assert.Equal(56, Unsafe.SizeOf<UAbcFlat>());
        Assert.Equal(IntPtr.Size, Unsafe.SizeOf<UAbcBoxed>());
    }
}

public class GcTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static UMixed[] Build(int count, List<WeakReference> weak)
    {
        var result = new UMixed[count];
        for (var i = 0; i < count; i++)
        {
            var text = new string('x', 5 + i % 7) + i;
            var holder = new object();
            weak.Add(new WeakReference(holder));
            result[i] = (i % 4) switch
            {
                0 => new Mixed1 { I = i, S = text },
                1 => new Mixed2((byte)i, holder, (short)i, i, i, text),
                2 => new ReadOnlyMixed(i, text),
                _ => new Rec(text, i),
            };
        }

        return result;
    }

    [Fact]
    public void References_inside_overlapped_unions_survive_collections_and_compaction()
    {
        var weak = new List<WeakReference>();
        var items = Build(2000, weak);

        for (var round = 0; round < 5; round++)
        {
            var junk = new List<byte[]>();
            for (var j = 0; j < 500; j++)
            {
                junk.Add(new byte[1000 + j]);
            }

            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.KeepAlive(junk);
        }

        for (var i = 0; i < items.Length; i++)
        {
            var text = new string('x', 5 + i % 7) + i;
            switch (i % 4)
            {
                case 0:
                    Assert.Equal(i, items[i].GetMixed1().I);
                    Assert.Equal(text, items[i].GetMixed1().S);
                    break;
                case 1:
                    var m = items[i].GetMixed2();
                    Assert.Equal(text, m.T);
                    Assert.True(weak[i].IsAlive);
                    Assert.Same(weak[i].Target, m.O);
                    Assert.Equal((long)i, m.L);
                    break;
                case 2:
                    Assert.Equal(i, items[i].GetReadOnlyMixed().A);
                    Assert.Equal(text, items[i].GetReadOnlyMixed().B);
                    break;
                default:
                    Assert.Equal(new Rec(text, i), items[i].GetRec());
                    break;
            }
        }

        GC.KeepAlive(items);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Hold(out UAbc union)
    {
        var o = new object();
        union = new C { _o = o, _i = 1 };
        return new WeakReference(o);
    }

    // The union is overwritten inside this frame, so nothing refers to the old object once it returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Overwrite()
    {
        var o = new object();
        UAbc union = new C { _o = o, _i = 1 };
        var weak = new WeakReference(o);
        union = new A { _i = 5, _s = "gone" };
        GC.KeepAlive(union);
        return weak;
    }

    [Fact]
    public void A_union_keeps_alive_what_it_holds()
    {
        var weak = Hold(out var union);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.True(weak.IsAlive);
        GC.KeepAlive(union);
    }

    [Fact]
    public void A_union_does_not_keep_alive_what_it_no_longer_holds()
    {
        var weak = Overwrite();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(weak.IsAlive);
    }
}

public class PointerTests
{
    [Fact]
    public unsafe void Structs_with_pointers_come_back_intact()
    {
        var x = 5;
        UPointers only = new OnlyPointers { P = &x, L = 7 };
        UPointers mixed = new WithPointer { P = &x, S = "ptr", N = 9 };
        UPointers number = 3;

        Assert.True(only.GetOnlyPointers().P == &x);
        Assert.Equal(7L, only.GetOnlyPointers().L);
        Assert.True(mixed.GetWithPointer().P == &x);
        Assert.Equal("ptr", mixed.GetWithPointer().S);
        Assert.Equal(9, mixed.GetWithPointer().N);
        Assert.Equal(3, number.GetInt32());
    }
}
