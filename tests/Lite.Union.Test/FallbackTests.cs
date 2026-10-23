using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace Lite.Union.Test;

public class TaglessBoxedTests
{
    [Fact]
    public void Boxed_has_no_tag_and_is_one_reference()
    {
        Assert.Equal(IntPtr.Size, Unsafe.SizeOf<UAbcBoxed>());
        Assert.Equal(IntPtr.Size, Unsafe.SizeOf<UBoxedNumbers>());
        Assert.Equal(IntPtr.Size, Unsafe.SizeOf<UAnimals>());
    }

    [Fact]
    public void More_specific_cases_are_asked_first()
    {
        UAnimals dog = new Dog();
        UAnimals cat = new Cat();       // a Cat is an Animal, and no case is more specific
        UAnimals animal = new Animal();
        var other = UAnimals.FromObject("anything");

        Assert.Equal(UAnimals.CaseKind.Dog, dog.Kind);
        Assert.Equal(UAnimals.CaseKind.Animal, cat.Kind);
        Assert.Equal(UAnimals.CaseKind.Animal, animal.Kind);
        Assert.Equal(UAnimals.CaseKind.Object, other.Kind);
        Assert.True(dog.IsDog && !dog.IsAnimal && !dog.IsObject);
        Assert.IsType<Cat>(cat.GetAnimal());
    }

    [Fact]
    public void An_animal_made_from_a_dog_reads_back_as_a_dog()
    {
        // Without a tag the object itself says what it is, the same as in a C# 15 union.
        var dog = new Dog();
        var made = UAnimals.FromAnimal(dog);
        Assert.Equal(UAnimals.CaseKind.Dog, made.Kind);
        Assert.Same(dog, made.GetDog());
    }

    [Fact]
    public void Interfaces_and_their_implementations_stay_apart()
    {
        UThings thing = new Thing { Id = 1 };
        var viaInterface = UThings.FromIThing(new ThingStruct { Id = 2, Label = "s" });   // a boxed struct, not a Thing
        UThings text = "t";

        Assert.Equal(UThings.CaseKind.Thing, thing.Kind);
        Assert.Equal(UThings.CaseKind.IThing, viaInterface.Kind);
        Assert.Equal(2, viaInterface.GetIThing().Id);
        Assert.Equal(UThings.CaseKind.String, text.Kind);
    }

    [Fact]
    public void Values_are_boxed_and_read_back()
    {
        UBoxedNumbers i = 5, l = 6L, d = 7.5;
        Assert.Equal(5, i.GetInt32());
        Assert.Equal(6L, l.GetInt64());
        Assert.Equal(7.5, d.GetDouble());
        Assert.True(i.IsInt32 && !i.IsInt64);
        Assert.Equal(UBoxedNumbers.CaseKind.Double, d.Kind);
        Assert.Equal(i, (UBoxedNumbers)5);
        Assert.NotEqual(i, (UBoxedNumbers)5L);
    }

    [Fact]
    public void Null_means_no_value()
    {
        UBoxedNullable none = (int?)null;
        UBoxedNullable text = (string)null!;
        UBoxedNullable five = (int?)5;

        Assert.True(none.IsUninitialized);
        Assert.True(text.IsUninitialized);
        Assert.False(text.IsString);
        Assert.Equal(none, text);
        Assert.Equal(UBoxedNullable.CaseKind.NullableInt32, five.Kind);
        Assert.Equal(5, five.GetNullableInt32());
        Assert.Throws<InvalidOperationException>(() => none.Match(a => 0, b => 1));
    }

    [Fact]
    public void Match_and_equality_work_without_a_tag()
    {
        UAnimals dog = new Dog();
        Assert.Equal("dog", dog.Match(a => "animal", d => "dog", o => "object"));
        var sameDog = dog;
        Assert.True(dog == sameDog);
        Assert.True(dog != (UAnimals)new Dog());     // another object
        Assert.Equal(dog.GetHashCode(), dog.GetHashCode());
    }
}

public class FallbackTests
{
    private static readonly Holder Sample = new() { Kv = new KeyValuePair<string, int>("k", 1), X = 2 };

    [Fact]
    public void By_default_a_case_that_cannot_be_overlapped_gets_a_field_of_its_own()
    {
        UFallbackField u = Sample;
        Assert.Equal("k", u.GetHolder().Kv.Key);
        Assert.Equal(2, u.GetHolder().X);
    }

    [Fact]
    public void Fallback_Boxed_puts_such_cases_into_one_object_field()
    {
        UFallbackBoxed u = Sample;
        UFallbackBoxed n = 7;
        Assert.Equal("k", u.GetHolder().Kv.Key);
        Assert.Equal(1, u.GetHolder().Kv.Value);
        Assert.Equal(2, u.GetHolder().X);
        Assert.Equal(7, n.GetInt32());
        Assert.True(u.IsHolder && !u.IsInt32);
        Assert.True(Unsafe.SizeOf<UFallbackBoxed>() < Unsafe.SizeOf<UFallbackField>());
    }

    [Fact]
    public void Fallback_Boxed_leaves_the_other_cases_overlapped()
    {
        UFallbackMixture holder = Sample;
        UFallbackMixture number = 9L;
        UFallbackMixture text = "text";
        UFallbackMixture mixed = new Mixed1 { I = 3, S = "m" };

        Assert.Equal(2, holder.GetHolder().X);
        Assert.Equal(9L, number.GetInt64());
        Assert.Equal("text", text.GetString());
        Assert.Equal("m", mixed.GetMixed1().S);
        Assert.Equal(UFallbackMixture.CaseKind.Holder, holder.Kind);
        Assert.Equal(UFallbackMixture.CaseKind.Int64, number.Kind);
        Assert.True(holder.Equals((UFallbackMixture)Sample));
    }

    [Fact]
    public void Layout_Flat_and_Boxed_are_unaffected_by_Fallback()
    {
        Assert.Equal(56, Unsafe.SizeOf<UAbcFlat>());
    }
}
