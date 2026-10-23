# Lite.Union

Discriminated unions for C# as a source generator. You list the types, and the generator creates a compact struct: the values of the cases overlap with each other, the references overlap with each other, and nothing is boxed. Cases have names (`IsCircle`, `TryGetCircle`, `GetCircle`), there are `Match`, `Switch` and equality, and on .NET 11 the struct becomes a real C# 15 union.

```csharp
using Lite.Union;

[Union<Circle, Rectangle, string>]
public partial struct Shape { }

Shape s = new Circle(2);

if (s.TryGetCircle(out var circle)) { /* ... */ }

var area = s.Match(
    circle => circle.Radius * circle.Radius * Math.PI,
    rectangle => rectangle.Width * rectangle.Height,
    text => 0);
```

---

## Why

Existing libraries store every case in a field of its own (OneOf: 56 bytes for three cases of 16 bytes each), and the C# 15 `union` keyword puts the value into an `object?` and boxes every value-type case. Lite.Union generates a struct for your particular set of types and lays it out so that:

- creating and taking apart value-type cases does not allocate;
- the struct is no larger than it has to be (24 bytes for three cases of 16 bytes: values, references and a tag);
- method names come from the types, not from indexes (`GetCircle`, not `AsT0`).

---

## Installation

```bash
dotnet add package Lite.Union
```

The package contains only an analyzer (the generator); it adds nothing to your application's assembly.

**Requirements**

| What | Minimum |
|---|---|
| Compiler | .NET SDK 8, 9, 10 or 11 (the generator is built against Roslyn 4.8). Verified: a project using the generator builds and runs, 10 of 10 checks, under SDK 8.0.426, 9.0.318, 10.0.401 and 11.0.100-rc.2; other patch versions were not tried |
| Language | C# 11 (the generic attribute `[Union<...>]`) |
| Target framework | `netstandard2.0`, `netstandard2.1`, .NET 8, 9, 10, 11 (the code is built **and run** on each of them) |

**NativeAOT and trimming.** The generated code uses no reflection at run time. Verified: a `PublishAot` publish and run on Linux x64 (.NET 10), 10 of 10 checks, no `IL…` warnings; a `TrimMode=full` publish on Windows, 10 of 10. Running an AOT binary on Windows was not verified (the machine had no Windows SDK).

**Linux.** All tests (the runtime tests on .NET 8, 9, 10, 11 and the generator tests) also pass on Linux x64 (WSL2).

The declaration `partial struct Shape;` with a semicolon needs C# 12; on C# 11 write `partial struct Shape { }`.

---

## Usage

The type must be a `partial struct` (or `readonly partial struct`) with two to sixteen cases. The struct can be nested in a `partial` class, struct, record or interface.

```csharp
[Union<int, string, Point>]
public readonly partial struct Value { }
```

For every case `T` (the name comes from the type: `Int32`, `String`, `Point`, `Int32Array`, `NullableInt32`, ...) the generator emits:

| Member | What it does |
|---|---|
| `FromT(T)` and the implicit conversion | create a value |
| `IsT` | whether the case is active |
| `TryGetT(out T)` | get the value if the case is active |
| `GetT()` | get the value or throw `InvalidOperationException` |
| `Kind`, `IsUninitialized` | which case is active; `default` is `Uninitialized` |
| `Match<TResult>(...)`, `Switch(...)` | take the union apart by case |
| `Equals`, `GetHashCode`, `==`, `!=`, `IEquatable<T>` | equality by case and value |

For cases of type `object`, interfaces and base types (`Enum`, `ValueType`) there is no implicit conversion (C# forbids it); create them with `FromT(...)`.

### C# 15

If the project has `System.Runtime.CompilerServices.UnionAttribute` (that is, .NET 11), the struct also becomes a language union: public constructors, `Value`, `HasValue` and `TryGetValue` appear. Then a `switch` with exhaustiveness checking works:

```csharp
var area = shape switch
{
    Circle c => c.Radius * c.Radius * Math.PI,
    Rectangle r => r.Width * r.Height,
    string => 0,
};
```

The compiler prefers `TryGetValue`, so the union is taken apart **without boxing**. The built-in C# 15 `union` keeps the value in an `object?` and boxes value-type cases; Lite.Union does not. On older platforms these members are not generated and everything else works the same.

---

## Memory layout

Set in the attribute:

```csharp
[Union<A, B, C>(Layout = UnionLayout.Overlap)]   // the default
[Union<A, B, C>(Layout = UnionLayout.Flat)]
[Union<A, B, C>(Layout = UnionLayout.Boxed)]
```

| Mode | How it is stored | Size for `A`, `B`, `C` of 16 bytes each | Allocations |
|---|---|---|---|
| `Overlap` | the values of all cases over each other, the references over each other, plus a tag | 24 bytes | none |
| `Flat` | a field of its own for every case, plus a tag | 56 bytes | none |
| `Boxed` | everything in one `object?`, **no tag** | 8 bytes | when a value-type case is created |

**Overlap.** Every case is split into values and references. The values (including whole unmanaged structs) go into one shared block, the references (`string`, classes, arrays, structs made only of references) into another shared block. A reference never shares bytes with a number: the CLR forbids that overlap, otherwise the garbage collector would take a number for an address. A struct that mixes numbers and references (`struct { int I; string S; }`) is taken apart field by field and put back together with `UnsafeAccessor` (.NET 8+).

**Boxed (no tag).** The struct holds a single `object?`, like the built-in C# 15 union. Which case is active is told by the type of the object itself, therefore:

- More specific cases are tested first: `Dog` before `Animal`, any type before `object`, the rest in declaration order. If you create an `Animal` case from a `Dog` object, `Kind` shows `Dog` (the built-in union behaves the same way).
- `null` means "no value": it is the same as `default`, and `IsString` is `false` for `null`.
- If two cases can be the same object (`int` and `int?` both box to `Int32`), that is error `LUNION006`, underlined on the second attribute argument.

### When a case cannot be overlapped

Such a case does not break the union; it is simply stored differently. This happens for: generic structs that mix numbers and references, structs with a `fixed` buffer or with inaccessible types inside, unions that hold references, BCL structs whose fields are not visible (see below), and any mixed struct on platforms without `UnsafeAccessor` (before .NET 8). The `Fallback` property chooses what to do with them (only for `Layout = Overlap`):

```csharp
[Union<A, B, C>]                                            // Fallback = UnionFallback.Field, the default
[Union<A, B, C>(Fallback = UnionFallback.Boxed)]
```

| `Fallback` | What happens | Allocations |
|---|---|---|
| `Field` (default) | each such case gets a field of its own; the other cases overlap as usual | none |
| `Boxed` | all such cases share one `object?`; the other cases overlap as usual | when a value-type such case is created |

The generator tells you where there is no overlap and why with an informational message `LUNION100` on the attribute argument, for example: "Case 'Holder' is stored in a field of its own instead of overlapping with the others: it is a generic struct that mixes values and references". It does not affect the build.

### Project-wide settings

The layout and the fallback can be set once for the whole project so you do not have to write them on every type:

```xml
<PropertyGroup>
  <LiteUnionLayout>Overlap</LiteUnionLayout>     <!-- Overlap | Flat | Boxed -->
  <LiteUnionFallback>Field</LiteUnionFallback>   <!-- Field | Boxed -->
</PropertyGroup>
```

Priority: **the attribute on the type**, then the project setting, then the default (`Overlap` and `Field`). An explicit `Layout = UnionLayout.Overlap` on a type beats the project setting; it is not treated as "nothing set". The names are case-insensitive. An unknown value gives warning `LUNION007` and the default applies. The settings are read from MSBuild properties, so they can be set in `Directory.Build.props` or on the command line (`-p:LiteUnionLayout=Flat`).

**BCL structs.** The compiler sees them through reference assemblies, and those hide private fields (`netstandard` hides all of them, .NET 5+ leaves placeholders). So such a struct looks empty, that is "unmanaged", although it may hold a reference (`CancellationToken`, `KeyValuePair<string, int>`). The generator runs inside the compiler process and asks the runtime it is running in for the real type (reflection over the fields, with the type arguments substituted for generics). If it holds no references (`Guid`, `TimeSpan`, `KeyValuePair<int, int>`), the struct overlaps as a value; otherwise, or if the type cannot be found, it goes to `Fallback`. There are no type lists in the generator. The assumption made here: whether a BCL struct holds references does not depend on the runtime that later runs the compiled code.

**Union inside a union.** A union can be a case of another union. If the inner one holds no references (all its cases are unmanaged and the layout is not `Boxed`), it overlaps as a value; otherwise it goes to `Fallback`.

**Pointers.** Structs with pointers are supported. If they mix pointers with references, the generated code is `unsafe` and the project needs `AllowUnsafeBlocks` (otherwise error `LUNION005` underlines the attribute argument).

---

## Which types can be cases

Any: `class`, `record class`, `struct`, `readonly struct`, `record struct`, `enum`, interfaces, delegates, arrays, tuples, `Nullable<T>`, generics. The union itself must be a plain `struct`, not a `record struct` and not a `class`.

---

## Diagnostics

| Id | When |
|---|---|
| `LUNION001` | the union, or a type it is nested in, is not `partial` |
| `LUNION002` | two cases produce the same name |
| `LUNION003` | the union is generic or nested in a generic type |
| `LUNION004` | the union is declared as a `record struct` |
| `LUNION005` | a case contains pointers and `AllowUnsafeBlocks` is off (the attribute argument is underlined) |
| `LUNION006` | in `Layout = Boxed` two cases cannot be told apart (`int` and `int?`); the second argument is underlined |
| `LUNION007` | unknown value of `LiteUnionLayout` or `LiteUnionFallback` in the project (a warning); the default applies |
| `LUNION100` | (information) a case is not overlapped and is stored in a field of its own or in an `object?`; the message gives the reason |

---

## Measurements

Measured on .NET 11 RC2, Intel Core i5-9300H, BenchmarkDotNet 0.15.8 in-process (it does not know `net11.0` yet), 1000 elements. Cases: `A { int; string }`, `B { string; long }`, `C { object; int }`, 16 bytes each. This is a preliminary, rough measurement; a strict comparison with all the layouts, type sets and other libraries is described in [docs/BENCHMARKS.md](docs/BENCHMARKS.md), and its results will appear there after the run.

| | Lite.Union | Built-in C# 15 `union` | OneOf | Dunet |
|---|---|---|---|---|
| Size of a value | 24 bytes | 8 bytes + a heap object | 56 bytes | a reference + an object |
| `switch` over prebuilt values | 1.72 µs | 1.75 µs | 2.55 µs | 6.20 µs |
| Create and take apart | 4.8 µs, 0 B | 6.9 µs, 32 KB | 4.9 µs, 0 B | 16.8 µs, 32 KB |
| Fill an array | 2.95 µs, 24 KB | 8.0 µs, 40 KB | 6.7 µs, 56 KB | 8.0 µs, 40 KB |

What these numbers do **not** say: they were taken on one computer with one set of types, and only the `Overlap` mode was measured. The `Flat` and `Boxed` modes (including `Boxed` without a tag), other sets of types (numbers only, classes only) and other libraries with a similar idea (StructUnion, NonboxingUnion and others) have not been compared yet.

---

## License

MIT. See [LICENSE](LICENSE).
