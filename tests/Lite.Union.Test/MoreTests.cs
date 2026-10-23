using System;
using System.Collections.Generic;
using Lite.Union.Test.Nesting;
using Xunit;

namespace Lite.Union.Test;

public class ShapeTests
{
    [Fact]
    public void Struct_holding_a_generic_struct_is_kept_as_it_is()
    {
        UHolders h = new Holder { Kv = new KeyValuePair<string, int>("k", 1), X = 2 };
        var back = h.GetHolder();
        Assert.Equal("k", back.Kv.Key);
        Assert.Equal(1, back.Kv.Value);
        Assert.Equal(2, back.X);
    }

    [Fact]
    public void Struct_holding_an_unmanaged_generic_struct_is_split()
    {
        UHolders h = new HolderWithTuple { Pair = (3, 4), S = "s" };
        var back = h.GetHolderWithTuple();
        Assert.Equal((3, 4), back.Pair);
        Assert.Equal("s", back.S);
    }

    [Fact]
    public void Big_struct_with_many_fields_comes_back_intact()
    {
        var big = new Big
        {
            A1 = 1, A2 = 2, A3 = 3, A4 = 4, A5 = 5, A6 = 6, A7 = 7, A8 = 8,
            S1 = "s1", S2 = "s2", S3 = "s3", L1 = 100, L2 = 200, O1 = "o", B1 = 9, D1 = 1.5,
        };
        UBig u = big;
        var back = u.GetBig();
        Assert.Equal(big.A1, back.A1); Assert.Equal(big.A8, back.A8);
        Assert.Equal("s1", back.S1); Assert.Equal("s2", back.S2); Assert.Equal("s3", back.S3);
        Assert.Equal(100L, back.L1); Assert.Equal(200L, back.L2);
        Assert.Equal("o", back.O1); Assert.Equal((byte)9, back.B1); Assert.Equal(1.5, back.D1);
    }

    [Fact]
    public void Nullable_references_and_values_survive()
    {
        UBig withValues = new NullableRefs { S = "s", O = 5, N = 7 };
        UBig withNulls = new NullableRefs { S = null, O = null, N = null };
        Assert.Equal("s", withValues.GetNullableRefs().S);
        Assert.Equal(5, withValues.GetNullableRefs().O);
        Assert.Equal(7, withValues.GetNullableRefs().N);
        Assert.Null(withNulls.GetNullableRefs().S);
        Assert.Null(withNulls.GetNullableRefs().O);
        Assert.Null(withNulls.GetNullableRefs().N);
    }

    [Fact]
    public void Enums_of_every_size_come_back_intact()
    {
        UEnums a = BigEnum.Huge;
        UEnums b = Flags.A | Flags.B;
        UEnums c = Color.Blue;
        Assert.Equal(BigEnum.Huge, a.GetBigEnum());
        Assert.Equal(Flags.A | Flags.B, b.GetFlags());
        Assert.Equal(Color.Blue, c.GetColor());
    }

    [Fact]
    public void Interface_delegate_and_base_type_cases_use_factories()
    {
        var thing = new Thing { Id = 4 };
        Calc calc = x => x * 2;
        Action action = () => { };
        Delegate del = calc;

        var i = UInterfaces.FromIThing(thing);
        var c = (UInterfaces)calc;
        var a = (UInterfaces)action;
        var d = UInterfaces.FromDelegate(del);
        var e = UInterfaces.FromEnum(Color.Green);
        var v = UInterfaces.FromValueType(5);

        Assert.Same(thing, i.GetIThing());
        Assert.Equal(6, c.GetCalc()(3));
        Assert.Same(action, a.GetAction());
        Assert.Same(del, d.GetDelegate());
        Assert.Equal(Color.Green, e.GetEnum());
        Assert.Equal(5, v.GetValueType());
        Assert.Equal(UInterfaces.CaseKind.IThing, i.Kind);
    }

    [Fact]
    public void Implementers_of_the_same_interface_stay_distinct()
    {
        UImplementers s = new ThingStruct { Id = 1, Label = "l" };
        UImplementers c = new Thing { Id = 2 };
        Assert.True(s.IsThingStruct);
        Assert.True(c.IsThing);
        Assert.Equal("l", s.GetThingStruct().Label);
        Assert.Equal(2, c.GetThing().Id);
    }

    [Fact]
    public void Array_cases_get_distinct_names()
    {
        UArrays a = new[] { 1 };
        UArrays b = new[] { 2L };
        UArrays c = new[] { "x" };
        UArrays d = new[] { new[] { 3 } };
        Assert.Equal(1, a.GetInt32Array()[0]);
        Assert.Equal(2L, b.GetInt64Array()[0]);
        Assert.Equal("x", c.GetStringArray()[0]);
        Assert.Equal(3, d.GetInt32ArrayArray()[0][0]);
    }

    [Fact]
    public void Boxed_and_Flat_handle_mixed_structs()
    {
        UBoxedMixed b = new Mixed1 { I = 1, S = "boxed" };
        UFlatMixed f = new Mixed1 { I = 2, S = "flat" };
        UBoxedMixed br = new Rec("r", 3);
        UFlatMixed fr = new Rec("r", 4);
        UBoxedMixed bs = "str";
        UFlatMixed fs = "str";

        Assert.Equal("boxed", b.GetMixed1().S);
        Assert.Equal("flat", f.GetMixed1().S);
        Assert.Equal(new Rec("r", 3), br.GetRec());
        Assert.Equal(new Rec("r", 4), fr.GetRec());
        Assert.Equal("str", bs.GetString());
        Assert.Equal("str", fs.GetString());
        Assert.True(br == (UBoxedMixed)new Rec("r", 3));
        Assert.True(fr != (UFlatMixed)new Rec("r", 5));
    }

    [Fact]
    public void Boxed_values_are_independent_copies()
    {
        var m = new Mixed1 { I = 1, S = "a" };
        UBoxedMixed u = m;
        m.I = 99;
        Assert.Equal(1, u.GetMixed1().I);
    }

    private static readonly UMutable Static = 5;

    [Fact]
    public void A_non_readonly_union_works_through_readonly_references()
    {
        Assert.True(Static.IsInt32);
        Assert.Equal(5, Static.GetInt32());
        Assert.Equal(Static, (UMutable)5);
        Assert.NotEqual(Static, (UMutable)"s");
        Check(in Static);

        static void Check(in UMutable value) => Assert.True(value.TryGetInt32(out var n) && n == 5);
    }

    [Fact]
    public void Unions_nested_in_classes_structs_and_records_work()
    {
        ClassHost.StructHost.InStruct a = 1;
        RecordHost.InRecord b = "two";
        RecordStructHost.InRecordStruct c = 3;
        Assert.Equal(1, a.GetInt32());
        Assert.Equal("two", b.GetString());
        Assert.Equal(3, c.GetInt32());
    }
}

public class EqualityEdgeTests
{
    [Fact]
    public void Same_bits_in_different_cases_are_not_equal()
    {
        UMutable i = 0;
        UMutable s = (string)null!;
        Assert.NotEqual(i, s);
        Assert.False(i == s);
    }

    [Fact]
    public void Null_references_compare_equal_within_a_case()
    {
        UMutable a = (string)null!;
        UMutable b = (string)null!;
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_cases_rarely_share_a_hash()
    {
        UValues a = 1.0;
        UValues b = Color.Red;
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Unions_work_as_dictionary_keys_and_in_sets()
    {
        var set = new HashSet<UAbc>
        {
            new A { _i = 1, _s = "a" },
            new A { _i = 1, _s = "a" },
            new B { _s = "b", _l = 2 },
            default,
        };
        Assert.Equal(3, set.Count);
        Assert.Contains((UAbc)new B { _s = "b", _l = 2 }, set);
    }

    [Fact]
    public void Uninitialized_members_are_consistent()
    {
        UMutable u = default;
        Assert.False(u.IsInt32);
        Assert.False(u.TryGetInt32(out _));
        Assert.Throws<InvalidOperationException>(() => u.GetInt32());
        Assert.Throws<InvalidOperationException>(() => u.Switch(x => { }, x => { }));
        Assert.Equal(default, u);
        Assert.Equal(0, u.GetHashCode());
    }
}
