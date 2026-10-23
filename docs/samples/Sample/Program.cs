using System;
using System.Collections.Generic;
using Lite.Union;

public struct A { public int _i; public string _s; }
public struct B { public string _s; public long _l; }
public struct C { public object _o; public int _i; }
public readonly record struct G(string Name);
public readonly record struct Money(decimal Amount, string Currency);
public class K { public int X; public override string ToString() => "K" + X; }

[Union<A, B, C>] public partial struct Union;
[Union<int, double, long>] public partial struct Num;
[Union<string, int, K>] public partial struct Mixed;
[Union<G, Money, (int, string), KeyValuePair<string, int>, int[]>] public partial struct Rich;
[Union<int[], long[], string>] public partial struct Arrs;

class P
{
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) Environment.ExitCode = 1; }

    static unsafe void Main()
    {
        T2.Run(); T4.Run(); T3.Run(); { Bcl bc = System.Threading.CancellationToken.None; Console.WriteLine("ok   Bcl " + bc.GetCancellationToken().CanBeCanceled); } Union a = new A { _i = 1, _s = "a" };
        Union b = new B { _s = "b", _l = 2 };
        Union c = new C { _o = "c", _i = 3 };
        Check(a.GetA()._i == 1 && a.GetA()._s == "a", "A roundtrip");
        Check(b.GetB()._s == "b" && b.GetB()._l == 2, "B roundtrip");
        Check((string)c.GetC()._o! == "c" && c.GetC()._i == 3, "C roundtrip");
        Check(a != b && a.Equals((Union)new A { _i = 1, _s = "a" }) && a.GetHashCode() == ((Union)new A { _i = 1, _s = "a" }).GetHashCode(), "equality");
        Check(!a.TryGetB(out _) && a.IsA && a.Kind == Union.CaseKind.A, "Is/TryGet");
        Check(default(Union).IsUninitialized, "default");
        Console.WriteLine("sizeof(Union) = " + System.Runtime.CompilerServices.Unsafe.SizeOf<Union>());

        Num n = 5; Check(n.Match(x => "i", y => "d", z => "l") == "i" && n.ToString() == "Int32(5)", "Num");
        Console.WriteLine("sizeof(Num) = " + System.Runtime.CompilerServices.Unsafe.SizeOf<Num>());

        Mixed m = "hi"; Mixed k = new K { X = 7 }; Mixed i = 3;
        Check(m.GetString() == "hi" && k.GetK().X == 7 && i.GetInt32() == 3 && m == (Mixed)"hi", "Mixed");
        Console.WriteLine("sizeof(Mixed) = " + System.Runtime.CompilerServices.Unsafe.SizeOf<Mixed>());

        Rich r1 = new G("g"); Rich r2 = new Money(12.5m, "EUR"); Rich r3 = (3, "t"); Rich r4 = new KeyValuePair<string, int>("k", 9); Rich r5 = new[] { 1, 2 };
        Check(r1.GetG().Name == "g" && r2.GetMoney() == new Money(12.5m, "EUR") && r3.GetValueTupleInt32String() == (3, "t")
            && r4.GetKeyValuePairStringInt32().Key == "k" && r4.GetKeyValuePairStringInt32().Value == 9 && r5.GetInt32Array()[1] == 2, "Rich");
        Console.WriteLine("sizeof(Rich) = " + System.Runtime.CompilerServices.Unsafe.SizeOf<Rich>());

        Arrs ar = new[] { 1 }; Check(ar.GetInt32Array()[0] == 1, "Arrs");
    }
}
[Union<KeyValuePair<string, string>, string, G>] public partial struct RefOnly;
static class T2 { public static void Run() {
  RefOnly r = new KeyValuePair<string, string>("k", "v"); RefOnly s = new G("g");
  Console.WriteLine((r.GetKeyValuePairStringString().Value == "v" && s.GetG().Name == "g" ? "ok   " : "FAIL ") + "RefOnly, sizeof = " + System.Runtime.CompilerServices.Unsafe.SizeOf<RefOnly>()); } }
[Union<System.Threading.CancellationToken, int>] public partial struct Bcl;
[Union<A, B, C>(Layout = UnionLayout.Flat)] public partial struct UFlat;
[Union<A, B, C>(Layout = UnionLayout.Boxed)] public partial struct UBoxed;
[Union<string, int, G>(Layout = UnionLayout.Boxed)] public partial struct MBoxed;
static class T3 { public static void Run() {
  UFlat f = new A { _i = 1, _s = "a" }; UBoxed b = new B { _s = "b", _l = 2 }; MBoxed m = 5; MBoxed s = "x";
  var ok = f.GetA()._s == "a" && b.GetB()._l == 2 && m.GetInt32() == 5 && s.GetString() == "x" && b != (UBoxed)new B { _s = "b", _l = 3 } && b == (UBoxed)new B { _s = "b", _l = 2 };
  Console.WriteLine((ok ? "ok   " : "FAIL ") + "layouts; sizeof flat/boxed/boxed2 = " + System.Runtime.CompilerServices.Unsafe.SizeOf<UFlat>() + "/" + System.Runtime.CompilerServices.Unsafe.SizeOf<UBoxed>() + "/" + System.Runtime.CompilerServices.Unsafe.SizeOf<MBoxed>()); } }
[Union<MA.Application, MA.Tenant, MA.Server, MA.Group>] public readonly partial struct MA2;
public static class MA { public readonly record struct Application; public readonly record struct Tenant; public readonly record struct Server; public readonly record struct Group(string GroupName); }
static class T4 { public static void Run() { MA2 g = new MA.Group("x"); MA2 s = new MA.Server(); Console.WriteLine("MA2 sizeof = " + System.Runtime.CompilerServices.Unsafe.SizeOf<MA2>() + " " + g.GetGroup().GroupName + " " + s.IsServer); } }
