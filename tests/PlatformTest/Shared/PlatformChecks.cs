using System;
using System.Collections.Generic;
using System.Threading;
using Lite.Union;

#if NS20
namespace Lite.Union.Test.Ns20;
#else
namespace Lite.Union.Test.Ns21;
#endif

public class K { public int X; }
public struct Plain { public int X; public long Y; }
public struct Mixed { public int I; public string S; }
public struct Nested { public Mixed Inner; public long L; public string T; }
public readonly record struct Rec(string Name, int Id);

[Union<int, string, Plain>] public partial struct UPlain { }
[Union<Mixed, Nested, Rec, int>] public partial struct UMixed { }
[Union<string, K, int[], object>] public partial struct URefs { }
[Union<Mixed, Rec, string>(Layout = UnionLayout.Flat)] public partial struct UFlat { }
[Union<Mixed, Rec, string>(Layout = UnionLayout.Boxed)] public partial struct UBoxed { }
[Union<(int, string), KeyValuePair<string, int>, int>] public partial struct UGeneric { }
[Union<Guid, TimeSpan, KeyValuePair<int, int>, CancellationToken, string>] public partial struct UBcl { }
public struct HoldsPair { public KeyValuePair<string, int> Kv; public int X; }
[Union<HoldsPair, int>] public partial struct UHolds { }

public static class PlatformChecks
{
    public static (int Count, List<string> Failures) Run()
    {
        var count = 0;
        var failures = new List<string>();
        void Check(bool ok, string what) { count++; if (!ok) failures.Add(what); }

        UPlain p1 = 5; UPlain p2 = "s"; UPlain p3 = new Plain { X = 1, Y = 2 };
        Check(p1.GetInt32() == 5 && p2.GetString() == "s" && p3.GetPlain().Y == 2, "plain");
        Check(p1.Match(i => "i", s => "s", x => "p") == "i" && p1.Kind == UPlain.CaseKind.Int32, "plain match/kind");
        Check(p1 == (UPlain)5 && p1 != p2 && default(UPlain).IsUninitialized, "plain equality/default");

        UMixed m1 = new Mixed { I = 7, S = "seven" };
        UMixed m2 = new Nested { Inner = new Mixed { I = 1, S = "in" }, L = 2, T = "t" };
        UMixed m3 = new Rec("n", 3);
        Check(m1.GetMixed().I == 7 && m1.GetMixed().S == "seven", "mixed roundtrip");
        Check(m2.GetNested().Inner.S == "in" && m2.GetNested().L == 2 && m2.GetNested().T == "t", "nested roundtrip");
        Check(m3.GetRec() == new Rec("n", 3), "record struct roundtrip");

        var arr = new[] { 1, 2 }; var o = new object();
        URefs r1 = "x"; URefs r2 = new K { X = 4 }; URefs r3 = arr; var r4 = URefs.FromObject(o);
        Check(r1.GetString() == "x" && r2.GetK().X == 4 && ReferenceEquals(r3.GetInt32Array(), arr) && ReferenceEquals(r4.GetObject(), o), "refs");

        UFlat f = new Mixed { I = 1, S = "flat" }; UBoxed b = new Mixed { I = 2, S = "boxed" };
        Check(f.GetMixed().S == "flat" && b.GetMixed().S == "boxed" && b == (UBoxed)new Mixed { I = 2, S = "boxed" }, "flat/boxed");

        UGeneric g1 = (1, "x"); UGeneric g2 = new KeyValuePair<string, int>("k", 2);
        Check(g1.GetValueTupleInt32String() == (1, "x") && g2.GetKeyValuePairStringInt32().Value == 2, "generic");

        var guid = Guid.NewGuid();
        UBcl bg = guid; UBcl bt = TimeSpan.FromSeconds(3); UBcl bk = new KeyValuePair<int, int>(1, 2); UBcl bc = CancellationToken.None; UBcl bs = "s";
        Check(bg.GetGuid() == guid && bt.GetTimeSpan() == TimeSpan.FromSeconds(3) && bk.GetKeyValuePairInt32Int32().Value == 2 && bc.GetCancellationToken() == CancellationToken.None && bs.GetString() == "s", "BCL structs");
        UHolds h = new HoldsPair { Kv = new KeyValuePair<string, int>("k", 5), X = 6 };
        Check(h.GetHoldsPair().Kv.Key == "k" && h.GetHoldsPair().Kv.Value == 5 && h.GetHoldsPair().X == 6, "struct holding a BCL struct");

        // references must survive a collection
        var items = new UMixed[200];
        for (var i = 0; i < items.Length; i++) items[i] = new Mixed { I = i, S = new string('x', i % 9) + i };
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        var intact = true;
        for (var i = 0; i < items.Length; i++) intact &= items[i].GetMixed().I == i && items[i].GetMixed().S == new string('x', i % 9) + i;
        Check(intact, "references survive GC");

        return (count, failures);
    }
}
