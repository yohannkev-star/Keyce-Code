using Keyce.Services;
using Xunit;

namespace Keyce.Tests;

public class RunnerTests
{
    [Fact]
    public async Task Detector_FindsDotnet()
    {
        IToolchainDetector detector = new ToolchainDetector();
        var tools = await detector.DetectAsync();
        Assert.Contains(tools, t => t.Name == "dotnet");
    }

    [Fact]
    public void NewRunner_IsNotRunning()
    {
        IRunnerService runner = new ProcessRunner();
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public void Stop_WhenNothingRunning_DoesNotThrow()
    {
        IRunnerService runner = new ProcessRunner();
        runner.Stop();
    }
}