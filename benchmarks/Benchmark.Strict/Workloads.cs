using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
#if BENCH_INPROCESS
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
#endif

namespace Bench;

/// <summary>The settings every benchmark runs with: separate process, two launches, no concurrent GC.</summary>
public sealed class StrictConfig : ManualConfig
{
    public StrictConfig()
    {
#if BENCH_INPROCESS
        // Part B (.NET 11): BenchmarkDotNet cannot start a separate process for it, so the benchmarks run in this one.
        AddJob(Job.Default
            .WithLaunchCount(1)
            .WithWarmupCount(4)
            .WithIterationCount(15)
            .WithIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100))
            .WithToolchain(InProcessNoEmitToolchain.Instance));
#else
        AddJob(Job.Default
            .WithLaunchCount(2)
            .WithWarmupCount(3)
            .WithIterationCount(10)
            .WithIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100))
            .WithGcServer(false)
            .WithGcConcurrent(false));
#endif
        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(JsonExporter.Full);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
    }
}

/// <summary>
/// The operations measured for one union library over one set of types. A concrete class per combination is generated
/// (W_{library}_{set}_{cases}); this base class holds the code, so every library runs exactly the same loops.
/// </summary>
[Config(typeof(StrictConfig))]
public abstract class Workloads<TUnion, TAdapter> where TAdapter : struct, IAdapter<TUnion>
{
    private const int N = 1000;

    private TUnion[] _items = null!;
    private TUnion[] _other = null!;

    [GlobalSetup]
    public void Setup()
    {
        _items = new TUnion[N];
        _other = new TUnion[N];
        for (var i = 0; i < N; i++)
        {
            _items[i] = TAdapter.Create(i % TAdapter.Cases);
            _other[i] = TAdapter.Create(i % TAdapter.Cases);
        }
    }

    /// <summary>Make a union from a value and take it apart again, 1000 times.</summary>
    [Benchmark]
    public long CreateAndMatch()
    {
        long sum = 0;
        for (var i = 0; i < N; i++)
        {
            sum += TAdapter.Match(TAdapter.Create(i % TAdapter.Cases));
        }

        return sum;
    }

    /// <summary>Take 1000 prebuilt unions apart.</summary>
    [Benchmark]
    public long MatchPrebuilt()
    {
        long sum = 0;
        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            sum += TAdapter.Match(items[i]);
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Consume(TUnion value) => value is null ? 0 : 1;

    /// <summary>Pass 1000 unions by value to a method that is not inlined: what copying a union costs.</summary>
    [Benchmark]
    public int PassByValue()
    {
        var sum = 0;
        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            sum += Consume(items[i]);
        }

        return sum;
    }

    /// <summary>Make an array of 1000 unions.</summary>
    [Benchmark]
    public TUnion[] FillArray()
    {
        var array = new TUnion[N];
        for (var i = 0; i < N; i++)
        {
            array[i] = TAdapter.Create(i % TAdapter.Cases);
        }

        return array;
    }

    /// <summary>Compare two arrays of 1000 unions element by element with the default equality of the type.</summary>
    [Benchmark]
    public int Equality()
    {
        var equal = 0;
        for (var i = 0; i < N; i++)
        {
            if (TAdapter.Equal(_items[i], _other[i]))
            {
                equal++;
            }
        }

        return equal;
    }
}
