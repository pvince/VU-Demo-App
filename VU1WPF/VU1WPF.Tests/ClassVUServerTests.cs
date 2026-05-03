using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using KR_VU1_Server;

namespace VU1WPF.Tests;

public sealed class ClassVUServerTests
{
    [Fact]
    public async Task RefreshDialListAsync_PopulatesDialList_OnValidResponse()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"status\":\"ok\",\"data\":[{\"uid\":\"dial-1\",\"dial_name\":\"CPU Temp\",\"value\":42,\"backlight\":{\"red\":10,\"green\":20,\"blue\":30},\"image_file\":null}]}")
        }));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "api key", client);

        bool ok = await server.RefreshDialListAsync();

        Assert.True(ok);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("/api/v0/dial/list?key=", handler.LastRequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("api", handler.LastRequestUri.ToString(), StringComparison.Ordinal);
        Assert.Contains("key", handler.LastRequestUri.ToString(), StringComparison.Ordinal);

        List<DialInfo> firstRead = server.GetDialList();
        Assert.Single(firstRead);
        Assert.Equal("dial-1", firstRead[0].uid);
        Assert.Equal("CPU Temp", firstRead[0].dial_name);

        firstRead.Clear();

        List<DialInfo> secondRead = server.GetDialList();
        Assert.Single(secondRead);
    }

    [Fact]
    public async Task RefreshDialListAsync_ReturnsTrue_AndUsesEmptyList_WhenResponseDataIsNull()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\",\"data\":null}")
        }));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.RefreshDialListAsync();

        Assert.True(ok);
        Assert.Empty(server.GetDialList());
    }

    [Fact]
    public async Task RefreshDialListAsync_ReturnsFalse_OnBlankContent()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        }));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.RefreshDialListAsync();

        Assert.False(ok);
    }

    [Fact]
    public async Task RefreshDialListAsync_ReturnsFalse_OnMalformedJson()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{")
        }));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.RefreshDialListAsync();

        Assert.False(ok);
    }

    [Fact]
    public async Task RefreshDialListAsync_ReturnsFalse_OnNonSuccessStatusCode()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.RefreshDialListAsync();

        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateDialValueAsync_ReturnsFalse_WhenUidIsBlank()
    {
        using var client = new HttpClient(new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var server = new VU1_Server("localhost", 5340, "k", client);

        Assert.False(await server.UpdateDialValueAsync(string.Empty, 50));
        Assert.False(await server.UpdateDialValueAsync(null!, 50));
    }

    [Fact]
    public async Task UpdateDialValueAsync_ReturnsFalse_OnNonSuccessStatusCode()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialValueAsync("dial-1", 50);

        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateDialBacklightAsync_ConvertsRgbBytesToPercent_WhenValuesAreNotPercentages()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialBacklightAsync("dial-1", 255, 128, 64, values_as_percent: false);

        Assert.True(ok);
        Assert.NotNull(handler.LastRequestUri);
        string request = handler.LastRequestUri!.ToString();
        Assert.Contains("red=100", request, StringComparison.Ordinal);
        Assert.Contains("green=50", request, StringComparison.Ordinal);
        Assert.Contains("blue=25", request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDialBacklightAsync_UsesClampedPercentValues_WhenFlagIsSet()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var client = new HttpClient(handler);
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialBacklightAsync("dial-1", 150, -5, 25, values_as_percent: true);

        Assert.True(ok);
        Assert.NotNull(handler.LastRequestUri);
        string request = handler.LastRequestUri!.ToString();
        Assert.Contains("red=100", request, StringComparison.Ordinal);
        Assert.Contains("green=0", request, StringComparison.Ordinal);
        Assert.Contains("blue=25", request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDialNameAsync_ReturnsFalse_WhenDialNameIsBlank()
    {
        using var client = new HttpClient(new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var server = new VU1_Server("localhost", 5340, "k", client);

        Assert.False(await server.UpdateDialNameAsync("dial-1", string.Empty));
        Assert.False(await server.UpdateDialNameAsync("dial-1", "   "));
    }

    [Fact]
    public async Task UpdateDialBackgroundImageAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        using var client = new HttpClient(new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var server = new VU1_Server("localhost", 5340, "k", client);

        bool ok = await server.UpdateDialBackgroundImageAsync("dial-1", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.png"));

        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateDialBackgroundImageAsync_SendsMultipartContent_OnSuccess()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "VU1WPF.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string imagePath = Path.Combine(tempDirectory, "dial.png");
        await File.WriteAllBytesAsync(imagePath, new byte[] { 1, 2, 3, 4 });

        var handler = new CapturingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        try
        {
            using var client = new HttpClient(handler);
            var server = new VU1_Server("localhost", 5340, "master key", client);

            bool ok = await server.UpdateDialBackgroundImageAsync("dial-1", imagePath);

            Assert.True(ok);
            Assert.NotNull(handler.LastRequestUri);
            Assert.Contains("/api/v0/dial/dial-1/image/set", handler.LastRequestUri!.ToString(), StringComparison.Ordinal);
            Assert.Equal("key", handler.LastFormFieldName);
            Assert.Equal("master key", handler.LastFormFieldValue);
            Assert.Equal("imgfile", handler.LastFileFieldName);
            Assert.Equal("dial.png", handler.LastFileName);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateDialBackgroundImageAsync_ReturnsFalse_OnCanceledRequest()
    {
        var handler = new CapturingHandler((_, cancellationToken) => Task.FromCanceled<HttpResponseMessage>(cancellationToken));

        string tempDirectory = Path.Combine(Path.GetTempPath(), "VU1WPF.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string imagePath = Path.Combine(tempDirectory, "dial.png");
        await File.WriteAllBytesAsync(imagePath, new byte[] { 9, 8, 7 });

        try
        {
            using var client = new HttpClient(handler);
            var server = new VU1_Server("localhost", 5340, "k", client);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            bool ok = await server.UpdateDialBackgroundImageAsync("dial-1", imagePath, cts.Token);

            Assert.False(ok);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Uri? LastRequestUri { get; private set; }

        public string? LastFormFieldName { get; private set; }

        public string? LastFormFieldValue { get; private set; }

        public string? LastFileFieldName { get; private set; }

        public string? LastFileName { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (HttpContent part in multipart)
                {
                    ContentDispositionHeaderValue? disposition = part.Headers.ContentDisposition;
                    if (disposition == null)
                    {
                        continue;
                    }

                    string fieldName = TrimQuotes(disposition.Name);
                    string fileName = TrimQuotes(disposition.FileNameStar ?? disposition.FileName);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        LastFileFieldName = fieldName;
                        LastFileName = fileName;
                        continue;
                    }

                    LastFormFieldName = fieldName;
                    LastFormFieldValue = await part.ReadAsStringAsync().ConfigureAwait(false);
                }
            }

            return await _responseFactory(request, cancellationToken).ConfigureAwait(false);
        }

        private static string TrimQuotes(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim('"');
        }
    }
}