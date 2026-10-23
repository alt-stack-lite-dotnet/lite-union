using System.Collections.Generic;
using Xunit;

namespace Lite.Union.Test;

// The same checks compiled for netstandard2.0 and netstandard2.1 and run here, on whatever runtime hosts the tests.
// Reference assemblies of netstandard hide the private fields of BCL structs; a union built on that wrong picture
// compiles but fails to load. Only running the code catches it.
public class NetStandardTests
{
    private static void Assert_NoFailures((int Count, List<string> Failures) result)
    {
        Assert.True(result.Count > 0);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Unions_compiled_for_netstandard20_work() => Assert_NoFailures(Ns20.PlatformChecks.Run());

    [Fact]
    public void Unions_compiled_for_netstandard21_work() => Assert_NoFailures(Ns21.PlatformChecks.Run());
}
