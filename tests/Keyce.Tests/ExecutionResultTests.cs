using Keyce.Models;
using Xunit;

namespace Keyce.Tests;

public class ExecutionResultTests
{
    [Fact]
    public void ExitCodeZero_IsSuccess() =>
        Assert.True(new ExecutionResult { ExitCode = 0 }.Success);

    [Fact]
    public void NonZeroExitCode_IsNotSuccess() =>
        Assert.False(new ExecutionResult { ExitCode = 1 }.Success);

    [Fact]
    public void Cancelled_IsNotSuccess()
    {
        var r = ExecutionResult.Cancelled(TimeSpan.FromSeconds(2));
        Assert.True(r.WasCancelled);
        Assert.False(r.Success);
        Assert.Equal(TimeSpan.FromSeconds(2), r.Duration);
    }
}