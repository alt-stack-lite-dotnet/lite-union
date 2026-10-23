using Xunit;

namespace Lite.Union.Test;

public class RareShapeTests
{
    [Fact]
    public void A_union_can_be_a_case_of_another_union()
    {
        UInner inner = new Mixed1 { I = 3, S = "in" };
        UOuter a = inner;
        UOuter b = "text";
        UOuter c = 7;

        Assert.True(a.IsUInner);
        Assert.Equal(3, a.GetUInner().GetMixed1().I);
        Assert.Equal("in", a.GetUInner().GetMixed1().S);
        Assert.Equal("text", b.GetString());
        Assert.Equal(7, c.GetInt32());
        Assert.True(a == (UOuter)inner);
        Assert.False(a == b);
    }

    [Fact]
    public void Unions_nest_more_than_one_level()
    {
        UInner inner = new Rec("r", 4);
        UOuter middle = inner;
        UOuterOuter outer = middle;
        Assert.Equal(new Rec("r", 4), outer.GetUOuter().GetUInner().GetRec());
        UOuterOuter other = inner;
        Assert.Equal(new Rec("r", 4), other.GetUInner().GetRec());
        UOuterOuter number = 5L;
        Assert.Equal(5L, number.GetInt64());
    }

    [Fact]
    public void A_struct_with_explicit_layout_and_a_reference_survives()
    {
        var value = new ExplicitWithReference { S = "s", L = 0x4000000000000000L };
        UExplicit u = value;
        var back = u.GetExplicitWithReference();
        Assert.Equal("s", back.S);
        Assert.Equal(0x4000000000000000L, back.L);
        Assert.Equal(value.D, back.D);
    }

    [Fact]
    public void A_nullable_struct_that_mixes_values_and_references_survives()
    {
        UNullableMixed some = (Mixed1?)new Mixed1 { I = 1, S = "x" };
        UNullableMixed none = (Mixed1?)null;
        Assert.True(some.GetNullableMixed1()!.Value.I == 1 && some.GetNullableMixed1()!.Value.S == "x");
        Assert.True(none.IsNullableMixed1);
        Assert.Null(none.GetNullableMixed1());
    }

    [Fact]
    public void Tuples_with_names_and_structs_holding_them_survive()
    {
        UHolders2 a = new MixedNullableHolder { Maybe = new Mixed1 { I = 2, S = "m" }, X = 3 };
        UHolders2 b = new TupleHolder { Pair = (4, "four"), L = 5 };
        UHolders2 c = (6, "six");
        Assert.Equal("m", a.GetMixedNullableHolder().Maybe!.Value.S);
        Assert.Equal("four", b.GetTupleHolder().Pair.Name);
        Assert.Equal(5L, b.GetTupleHolder().L);
        Assert.Equal(6, c.GetValueTupleInt32String().Item1);
    }

    [Fact]
    public void A_struct_of_two_references_overlaps_with_other_references()
    {
        URefPair a = new SelfRefs { A = "a", B = "b" };
        URefPair b = "single";
        Assert.Equal("b", a.GetSelfRefs().B);
        Assert.Equal("single", b.GetString());
    }
}

public class NestedValueUnionTests
{
    [Fact]
    public void A_union_of_plain_numbers_nested_in_a_union_overlaps_as_a_value()
    {
        UInnerNumbers inner = 5L;
        UOuterNumbers a = inner;
        UOuterNumbers b = 2.5;
        UOuterNumbers c = "s";
        Assert.Equal(5L, a.GetUInnerNumbers().GetInt64());
        Assert.Equal(2.5, b.GetDouble());
        Assert.Equal("s", c.GetString());
    }
}
