namespace Bench;

/// <summary>What the self-check found out about one adapter.</summary>
public readonly record struct CheckResult(bool MatchOk, bool EqualityOk, int Size, string? Error);

public static class Checks
{
    /// <summary>
    /// Before anything is timed: every case must come out of Match with the number the set says it should, so that no
    /// library is measured doing something different from the others. Equality is checked separately: a union that does not
    /// compare by value is reported, and its equality is left out of the tables.
    /// </summary>
    public static CheckResult Verify<TUnion, TAdapter>() where TAdapter : struct, IAdapter<TUnion>
    {
        string? error = null;
        var equality = true;
        try
        {
            for (var i = 0; i < TAdapter.Cases; i++)
            {
                var got = TAdapter.Match(TAdapter.Create(i));
                var want = TAdapter.Expected(i);
                if (got != want)
                {
                    error = $"case {i}: Match gave {got}, expected {want}";
                    break;
                }
            }

            for (var i = 0; i < TAdapter.Cases; i++)
            {
                if (!TAdapter.Equal(TAdapter.Create(i), TAdapter.Create(i)))
                {
                    equality = false;
                }

                for (var j = 0; j < TAdapter.Cases; j++)
                {
                    if (i != j && TAdapter.Equal(TAdapter.Create(i), TAdapter.Create(j)))
                    {
                        equality = false;
                    }
                }
            }
        }
        catch (System.Exception exception)
        {
            error = exception.GetType().Name + ": " + exception.Message;
        }

        return new CheckResult(error is null, equality, TAdapter.ByteSize, error);
    }
}
