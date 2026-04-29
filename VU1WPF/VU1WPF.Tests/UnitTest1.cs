using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KR_VU1_ConfigurationManager;
using KR_VU1_Server;
using VU1WPF;

namespace VU1WPF.Tests;

public class DialComputationTests
{
    [Theory]
    [InlineData(0f, 100f, 40f, 40)]
    [InlineData(50f, 50f, 200f, 0)]
    [InlineData(0f, 100f, 0f, 0)]
    [InlineData(0f, 100f, 100f, 100)]
    [InlineData(10f, 110f, 60f, 50)]
    public void ComputeDialValue_ReturnsExpectedPercent(float min, float max, float sensorValue, int expected)
    {
        int actual = DialComputationEngine.ComputeDialValuePercent(min, max, sensorValue);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveThresholdColor_ReturnsFirstThresholdGreaterOrEqualToValue()
    {
        var thresholds = new List<ClassDialThreshold>
        {
            new() { Threshold = 25, BacklightRed = 1, BacklightGreen = 2, BacklightBlue = 3 },
            new() { Threshold = 50, BacklightRed = 4, BacklightGreen = 5, BacklightBlue = 6 },
            new() { Threshold = 75, BacklightRed = 7, BacklightGreen = 8, BacklightBlue = 9 },
        };

        var match = DialComputationEngine.ResolveThresholdColor(thresholds, 49);

        Assert.NotNull(match);
        Assert.Equal(50, match!.Threshold);
        Assert.Equal(4, match.BacklightRed);
        Assert.Equal(5, match.BacklightGreen);
        Assert.Equal(6, match.BacklightBlue);
    }
}

public class DialUpdateOrchestratorTests
{
    [Fact]
    public void TryRun_ReturnsFalse_WhileUpdateInProgress()
    {
        var orchestrator = new DialUpdateOrchestrator();
        var hold = new TaskCompletionSource<bool>();

        bool first = orchestrator.TryRun(() => hold.Task);
        bool second = orchestrator.TryRun(() => Task.CompletedTask);

        Assert.True(first);
        Assert.False(second);
        hold.SetResult(true);
    }

    [Fact]
    public async Task TryRun_ReturnsTrue_AfterPreviousRunCompletes()
    {
        var orchestrator = new DialUpdateOrchestrator();

        bool first = orchestrator.TryRun(async () => await Task.Delay(30));
        Assert.True(first);

        await Task.Delay(100);

        bool second = orchestrator.TryRun(() => Task.CompletedTask);
        Assert.True(second);
    }

    [Fact]
    public void TryRun_DoesNotBlockCaller_WhenWorkIsSlow()
    {
        var orchestrator = new DialUpdateOrchestrator();
        var hold = new TaskCompletionSource<bool>();
        var sw = Stopwatch.StartNew();

        bool started = orchestrator.TryRun(() => hold.Task);
        sw.Stop();

        Assert.True(started);
        Assert.True(sw.ElapsedMilliseconds < 50);
        hold.SetResult(true);
    }
}

public class AsyncDialServerClientTests
{
    [Fact]
    public async Task UpdateDialValueAsync_UsesAsyncCallFlow_AndClampsValue()
    {
        var handler = new RecordingHandler(async ct =>
        {
            await Task.Delay(80, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        var task = server.UpdateDialValueAsync("uid-1", 200);
        Assert.False(task.IsCompletedSuccessfully);

        bool ok = await task;

        Assert.True(ok);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("dial/uid-1/set", handler.LastRequestUri!.ToString());
        Assert.Contains("value=100", handler.LastRequestUri.ToString());
    }

    [Fact]
    public async Task UpdateDialValueAsync_Cancellation_DoesNotDeadlockOrchestrator()
    {
        var handler = new RecordingHandler(async ct =>
        {
            await Task.Delay(5000, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);
        var orchestrator = new DialUpdateOrchestrator();
        using var cts = new CancellationTokenSource(60);

        bool firstStarted = orchestrator.TryRun(() => server.UpdateDialValueAsync("uid-1", 50, cts.Token));
        Assert.True(firstStarted);

        await Task.Delay(150);

        bool secondStarted = orchestrator.TryRun(() => Task.CompletedTask);
        Assert.True(secondStarted);
    }

    [Fact]
    public async Task UpdateDialBacklightAsync_ReturnsFalse_OnNonSuccessStatusCode()
    {
        var handler = new RecordingHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialBacklightAsync("uid-1", 255, 10, 0);

        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateDialNameAsync_ReturnsTrue_OnSuccessStatusCode()
    {
        var handler = new RecordingHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialNameAsync("uid-1", "CPU Temp");

        Assert.True(ok);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("dial/uid-1/name", handler.LastRequestUri!.ToString());
    }
}

public class DebouncedConfigSaverTests
{
    [Fact]
    public async Task RequestSave_CoalescesRapidRequests()
    {
        int saveCount = 0;
        var saver = new DebouncedConfigSaver(
            () =>
            {
                Interlocked.Increment(ref saveCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(80));

        saver.RequestSave();
        saver.RequestSave();
        saver.RequestSave();

        await saver.FlushAsync();

        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task RequestSave_ReturnsImmediately_WhenSaveActionIsSlow()
    {
        var hold = new TaskCompletionSource<bool>();
        var saver = new DebouncedConfigSaver(
            () => hold.Task,
            TimeSpan.FromMilliseconds(20));

        var sw = Stopwatch.StartNew();
        saver.RequestSave();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50);
        hold.SetResult(true);
        await saver.FlushAsync();
    }

    [Fact]
    public async Task RequestSave_AfterError_StillAllowsSubsequentSave()
    {
        int saveCount = 0;
        bool failFirst = true;
        var saver = new DebouncedConfigSaver(
            () =>
            {
                Interlocked.Increment(ref saveCount);
                if (failFirst)
                {
                    failFirst = false;
                    throw new InvalidOperationException("boom");
                }

                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(20));

        saver.RequestSave();
        await saver.FlushAsync();
        saver.RequestSave();
        await saver.FlushAsync();

        Assert.Equal(2, saveCount);
    }
}

public class ResponsivenessIntegrationTests
{
    [Fact]
    public async Task SlowServerUpdate_DoesNotBlockDebouncedSaveRequests()
    {
        var handler = new RecordingHandler(async ct =>
        {
            await Task.Delay(180, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);
        var orchestrator = new DialUpdateOrchestrator();

        int saveCount = 0;
        var saver = new DebouncedConfigSaver(
            () =>
            {
                Interlocked.Increment(ref saveCount);
                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(40));

        bool started = orchestrator.TryRun(() => server.UpdateDialValueAsync("uid-1", 50));
        Assert.True(started);

        var sw = Stopwatch.StartNew();
        saver.RequestSave();
        saver.RequestSave();
        saver.RequestSave();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 50);
        await Task.Delay(240);
        await saver.FlushAsync();
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public async Task OrchestratorRemainsStable_WhileSaveDebounceIsActive()
    {
        var hold = new TaskCompletionSource<bool>();
        var orchestrator = new DialUpdateOrchestrator();
        var saver = new DebouncedConfigSaver(
            async () => await Task.Delay(60),
            TimeSpan.FromMilliseconds(30));

        bool first = orchestrator.TryRun(() => hold.Task);
        Assert.True(first);

        saver.RequestSave();
        saver.RequestSave();
        await Task.Delay(80);
        await saver.FlushAsync();

        hold.SetResult(true);
        await Task.Delay(40);

        bool second = orchestrator.TryRun(() => Task.CompletedTask);
        Assert.True(second);
    }
}

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _responseFactory;

    public RecordingHandler(Func<CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public Uri? LastRequestUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return await _responseFactory(cancellationToken);
    }
}
