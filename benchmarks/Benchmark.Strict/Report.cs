using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Bench;

/// <summary>
/// Turns the BenchmarkDotNet json files of one or more runs into the markdown tables of BENCHMARKS.md.
/// Usage: --report --out result.md --selfcheck selfcheck.csv --unsupported unsupported.csv [--title text] dir [dir...]
/// With several directories (independent runs of the same benchmarks) the median of the medians is used and the
/// largest difference between runs is reported.
/// </summary>
internal static class Report
{
    private static readonly (string Id, string Display)[] Libs =
    {
        ("LiteOverlap", "Lite.Union Overlap"),
        ("LiteOverlapKind", "Lite.Union Overlap (switch по Kind)"),
        ("LiteFlat", "Lite.Union Flat"),
        ("LiteBoxed", "Lite.Union Boxed"),
        ("OneOf", "OneOf"),
        ("Dunet", "Dunet"),
        ("UnionTypes", "N.SourceGenerators.UnionTypes"),
        ("DotVariant", "dotVariant"),
        ("StructUnion", "StructUnion"),
        ("SumSharp", "SumSharp"),
        ("SumSharpObject", "SumSharp (OneObject)"),
        ("Reference", "рукописная структура"),

        // part B (.NET 11)
        ("Native", "union C# 15 (встроенный)"),
        ("NonboxingUnion", "NonboxingUnion"),
        ("StructUnionNative", "StructUnion (NativeUnion)"),
    };

    private static readonly (string Method, string Title)[] Operations =
    {
        ("CreateAndMatch", "создать и разобрать"),
        ("MatchPrebuilt", "разобрать готовые"),
        ("PassByValue", "передача по значению"),
        ("FillArray", "заполнить массив"),
        ("Equality", "равенство"),
    };

    private static readonly string[] Sets = { "Values", "Refs", "Mixed", "Big" };
    private static readonly int[] Sizes = { 3, 8 };

    private sealed record Sample(double MedianNs, double MeanNs, double StdDevNs, long AllocatedBytes);

    public static int Run(string[] args)
    {
        string? output = null, selfcheck = null, unsupported = null;
        var directories = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": output = args[++i]; break;
                case "--selfcheck": selfcheck = args[++i]; break;
                case "--unsupported": unsupported = args[++i]; break;
                default: directories.Add(args[i]); break;
            }
        }

        if (directories.Count == 0)
        {
            Console.WriteLine("usage: --report --out result.md [--selfcheck f.csv] [--unsupported f.csv] resultsDir [resultsDir...]");
            return 1;
        }

        // runs[r][(type, method)] -> sample
        var runs = directories.Select(Load).ToList();
        var keys = runs.SelectMany(r => r.Keys).Distinct().ToList();

        // One value per benchmark: the median of the medians of the runs.
        var merged = new Dictionary<(string Type, string Method), Sample>();
        double worstSpread = 0;
        string worstName = "";
        foreach (var key in keys)
        {
            var samples = runs.Where(r => r.ContainsKey(key)).Select(r => r[key]).ToList();
            var medians = samples.Select(s => s.MedianNs).OrderBy(x => x).ToList();
            var median = medians.Count % 2 == 1 ? medians[medians.Count / 2] : (medians[medians.Count / 2 - 1] + medians[medians.Count / 2]) / 2;
            merged[key] = new Sample(median, samples.Average(s => s.MeanNs), samples.Max(s => s.StdDevNs), samples.Max(s => s.AllocatedBytes));
            if (samples.Count > 1)
            {
                var spread = medians.Max() / medians.Min() - 1;
                if (spread > worstSpread)
                {
                    worstSpread = spread;
                    worstName = key.Type + "." + key.Method;
                }
            }
        }

        var equalityBroken = new HashSet<string>();
        var sizes = new Dictionary<string, int>();
        if (selfcheck is not null && File.Exists(selfcheck))
        {
            foreach (var line in File.ReadLines(selfcheck).Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length > 6)
                {
                    sizes[parts[0]] = int.Parse(parts[4], CultureInfo.InvariantCulture);
                    if (parts[5] == "True" && parts[6] != "True")
                    {
                        equalityBroken.Add(parts[0]);
                    }
                }
            }
        }

        var unsupportedRows = new Dictionary<string, string>();
        if (unsupported is not null && File.Exists(unsupported))
        {
            foreach (var line in File.ReadLines(unsupported).Skip(1))
            {
                var comma = line.IndexOf(',');
                unsupportedRows[line[..comma]] = line[(comma + 1)..].Trim('"');
            }
        }

        var md = new StringBuilder();
        md.Append($"Прогонов: {runs.Count}. ");
        md.Append(runs.Count > 1
            ? $"В таблицах медиана медиан. Наибольшее расхождение между прогонами: {worstSpread:P1} (`{worstName}`).\n\n"
            : "В таблицах медиана одного прогона.\n\n");

        AppendScorecard(md, merged);

        foreach (var set in Sets)
        {
            foreach (var n in Sizes)
            {
                AppendTable(md, merged, set, n, sizes, equalityBroken, unsupportedRows);
            }
        }

        if (unsupportedRows.Count > 0)
        {
            md.Append("### Сочетания, которые не работают\n\n| Сочетание | Причина |\n|---|---|\n");
            foreach (var (name, reason) in unsupportedRows)
            {
                md.Append($"| `{name}` | {reason} |\n");
            }

            md.Append('\n');
        }

        if (output is null)
        {
            Console.Write(md.ToString());
        }
        else
        {
            File.WriteAllText(output, md.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"written: {Path.GetFullPath(output)} ({md.Length} chars)");
        }

        return 0;
    }

    private static Dictionary<(string Type, string Method), Sample> Load(string directory)
    {
        var result = new Dictionary<(string, string), Sample>();
        foreach (var file in Directory.GetFiles(directory, "*-report-full.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var benchmark in document.RootElement.GetProperty("Benchmarks").EnumerateArray())
            {
                var statistics = benchmark.GetProperty("Statistics");
                var memory = benchmark.GetProperty("Memory");
                result[(benchmark.GetProperty("Type").GetString()!, benchmark.GetProperty("Method").GetString()!)] = new Sample(
                    statistics.GetProperty("Median").GetDouble(),
                    statistics.GetProperty("Mean").GetDouble(),
                    statistics.GetProperty("StandardDeviation").GetDouble(),
                    memory.GetProperty("BytesAllocatedPerOperation").GetInt64());
            }
        }

        return result;
    }

    private static string TypeName(string lib, string set, int n) => $"W_{lib}_{set}_{n}";

    private static string Ratio(double value, double baseline) => $"{value / baseline:0.00}×";

    private static void AppendTable(
        StringBuilder md,
        Dictionary<(string Type, string Method), Sample> data,
        string set,
        int n,
        Dictionary<string, int> sizes,
        HashSet<string> equalityBroken,
        Dictionary<string, string> unsupported)
    {
        md.Append($"### {SetTitle(set)}, {n} кейс{(n == 3 ? "а" : "ов")}\n\n");
        md.Append("Время в микросекундах на 1000 операций (медиана) и отношение к Lite.Union Overlap (меньше — быстрее). ");
        md.Append("Размер — байт на значение в массиве (для Dunet это ссылка, сам объект лежит в куче). «Аллок.» — байт на 1000 операций.\n\n");
        md.Append("| Библиотека | Размер |");
        foreach (var (_, title) in Operations)
        {
            md.Append($" {title} |");
        }

        md.Append(" Аллок.: создать / массив |\n|---|---:|");
        md.Append(string.Concat(Operations.Select(_ => "---:|")));
        md.Append("---:|\n");

        data.TryGetValue((TypeName("LiteOverlap", set, n), "CreateAndMatch"), out var anchor);
        var best = new Dictionary<string, double>();
        foreach (var (method, _) in Operations)
        {
            var times = Libs
                .Where(l => l.Id != "Reference" && data.TryGetValue((TypeName(l.Id, set, n), method), out _)
                    && !(method == "Equality" && equalityBroken.Contains($"{l.Id}_{set}_{n}")))
                .Select(l => data[(TypeName(l.Id, set, n), method)].MedianNs)
                .ToList();
            best[method] = times.Count == 0 ? double.NaN : times.Min();
        }

        foreach (var (id, display) in Libs)
        {
            var name = $"{id}_{set}_{n}";
            if (unsupported.ContainsKey(name))
            {
                md.Append($"| {display} | — |{string.Concat(Operations.Select(_ => " не работает |"))} — |\n");
                continue;
            }

            if (!data.ContainsKey((TypeName(id, set, n), "CreateAndMatch")))
            {
                continue; // the library has no adapter for this set (the hand-written reference exists for two sets only)
            }

            md.Append($"| {display} | {(sizes.TryGetValue(name, out var size) ? size.ToString(CultureInfo.InvariantCulture) : "?")} |");
            foreach (var (method, _) in Operations)
            {
                if (method == "Equality" && equalityBroken.Contains(name))
                {
                    md.Append(" не сравнивает по значению |");
                    continue;
                }

                var sample = data[(TypeName(id, set, n), method)];
                var microseconds = sample.MedianNs / 1000;
                var cell = $"{microseconds:0.00}";
                if (data.TryGetValue((TypeName("LiteOverlap", set, n), method), out var baseline))
                {
                    cell += $" ({Ratio(sample.MedianNs, baseline.MedianNs)})";
                }

                if (id != "Reference" && Math.Abs(sample.MedianNs - best[method]) < 1e-9)
                {
                    cell = $"**{cell}**";
                }

                md.Append($" {cell} |");
            }

            var create = data[(TypeName(id, set, n), "CreateAndMatch")].AllocatedBytes;
            var fill = data[(TypeName(id, set, n), "FillArray")].AllocatedBytes;
            md.Append($" {create:N0} / {fill:N0} |\n");
        }

        md.Append('\n');
    }

    /// <summary>One line per library and operation: the geometric mean of its time over the time of Lite Overlap, for every combination that both have.</summary>
    private static void AppendScorecard(StringBuilder md, Dictionary<(string Type, string Method), Sample> data)
    {
        md.Append("### Сводка\n\n");
        md.Append("Среднее геометрическое отношения времени библиотеки ко времени Lite.Union Overlap по всем сочетаниям (набор типов × число кейсов), где работают обе. Меньше 1 — библиотека быстрее Overlap, больше 1 — медленнее. В скобках число сочетаний.\n\n");
        md.Append("| Библиотека |");
        foreach (var (_, title) in Operations)
        {
            md.Append($" {title} |");
        }

        md.Append("\n|---|");
        md.Append(string.Concat(Operations.Select(_ => "---:|")));
        md.Append('\n');
        foreach (var (id, display) in Libs.Where(l => l.Id != "LiteOverlap"))
        {
            md.Append($"| {display} |");
            foreach (var (method, _) in Operations)
            {
                var logs = new List<double>();
                foreach (var set in Sets)
                {
                    foreach (var n in Sizes)
                    {
                        if (data.TryGetValue((TypeName(id, set, n), method), out var other)
                            && data.TryGetValue((TypeName("LiteOverlap", set, n), method), out var anchor)
                            && other.MedianNs > 0 && anchor.MedianNs > 0)
                        {
                            logs.Add(Math.Log(other.MedianNs / anchor.MedianNs));
                        }
                    }
                }

                md.Append(logs.Count == 0 ? " — |" : $" {Math.Exp(logs.Average()):0.00}× ({logs.Count}) |");
            }

            md.Append('\n');
        }

        md.Append('\n');
    }

    private static string SetTitle(string set) => set switch
    {
        "Values" => "Только значения (int, long, double, Guid, …)",
        "Refs" => "Только ссылки (string, классы, массивы)",
        "Mixed" => "Смешанные структуры (число и ссылка в одной структуре)",
        "Big" => "Большие структуры (64 байта на кейс)",
        _ => set,
    };
}
