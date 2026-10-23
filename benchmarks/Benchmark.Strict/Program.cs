using System;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Running;
using Bench;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selfcheck")
        {
            return SelfCheck(args.Length > 1 ? args[1] : "selfcheck.csv");
        }

        if (args.Length > 0 && args[0] == "--report")
        {
            return Report.Run(args[1..]);
        }

        if (args.Length > 1 && args[0] == "--filters")
        {
            return Filters(args[1], args.Length > 2 ? args[2] : "*");
        }

        BenchmarkSwitcher.FromTypes(WorkloadTypes.Loadable()).Run(args);
        return 0;
    }

    /// <summary>
    /// Runs every adapter once without timing and prints what it found: sizes, correctness, equality. A union that cannot be
    /// loaded by the CLR or gives a wrong result is reported, written to the csv, and left out of the timed run.
    /// </summary>
    private static int SelfCheck(string csvPath)
    {
        var csv = new StringBuilder("name,lib,set,cases,size,match_ok,equality_ok,error\n");
        var wrong = 0;
        Console.WriteLine($"{"library",-18} {"set",-7} {"cases",5} {"size",5}  match  equality");
        foreach (var (name, lib, set, cases, verify) in Registry.All)
        {
            CheckResult result;
            try
            {
                result = verify();
            }
            catch (Exception exception)
            {
                result = new CheckResult(false, false, 0, exception.GetType().Name + ": " + Short(exception.Message));
            }

            Console.WriteLine($"{lib,-18} {set,-7} {cases,5} {result.Size,5}  {(result.MatchOk ? "ok   " : "FAIL ")}  {(result.EqualityOk ? "ok" : "no")}{(result.Error is null ? "" : "   " + result.Error)}");
            csv.Append($"{name},{lib},{set},{cases},{result.Size},{result.MatchOk},{result.EqualityOk},\"{(result.Error ?? "").Replace("\"", "'")}\"\n");
            if (!result.MatchOk)
            {
                wrong++;
            }
        }

        File.WriteAllText(csvPath, csv.ToString());
        Console.WriteLine(wrong == 0 ? "all adapters agree" : $"{wrong} adapters do not work (see the csv)");
        return 0;
    }

    private static string Short(string message) => message.Length > 140 ? message[..140] + "..." : message;

    /// <summary>
    /// Prints a --filter argument for the adapters that passed the self-check and whose name matches the given pattern,
    /// so that a shard of the run never meets a union that cannot work.
    /// </summary>
    private static int Filters(string csvPath, string pattern)
    {
        var ok = File.ReadLines(csvPath).Skip(1)
            .Select(line => line.Split(','))
            .Where(parts => parts.Length > 6 && parts[5] == "True" && Glob(parts[0], pattern))
            .Select(parts => "*W_" + parts[0] + "*");
        Console.WriteLine(string.Join(" ", ok));
        return 0;
    }

    private static bool Glob(string text, string pattern)
        => pattern == "*" || System.Text.RegularExpressions.Regex.IsMatch(text, "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$");
}
