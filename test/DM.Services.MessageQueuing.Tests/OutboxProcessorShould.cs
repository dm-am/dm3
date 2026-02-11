using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.MessageQueuing.Outbox;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Services.MessageQueuing.Tests;

public class OutboxProcessorShould
{
    private readonly Mock<IServiceProvider> serviceProvider;
    private readonly Mock<ILogger<OutboxProcessor>> logger;
    private readonly IOptions<OutboxConfiguration> options;

    public OutboxProcessorShould()
    {
        logger = new Mock<ILogger<OutboxProcessor>>();
        serviceProvider = new Mock<IServiceProvider>();
        options = Options.Create(new OutboxConfiguration());
    }

    [Fact]
    public void InheritFromBackgroundService()
    {
        var processor = new OutboxProcessor(serviceProvider.Object, logger.Object, options);

        processor.Should().BeAssignableTo<BackgroundService>();
    }

    [Fact]
    public async Task StartWithoutThrowingException()
    {
        var processor = new OutboxProcessor(serviceProvider.Object, logger.Object, options);
        var cts = new CancellationTokenSource();

        var act = async () =>
        {
            var task = processor.StartAsync(cts.Token);
            await Task.Delay(50);
            cts.Cancel();
            await Task.Delay(50);
            await processor.StopAsync(CancellationToken.None);
        };

        await act.Should().NotThrowAsync<Exception>();
    }

    [Fact]
    public async Task StopGracefully()
    {
        var processor = new OutboxProcessor(serviceProvider.Object, logger.Object, options);
        var cts = new CancellationTokenSource();

        var task = processor.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var act = async () => await processor.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleCancellationToken()
    {
        var processor = new OutboxProcessor(serviceProvider.Object, logger.Object, options);
        var cts = new CancellationTokenSource();

        var task = processor.StartAsync(cts.Token);
        await Task.Delay(50);

        cts.Cancel();
        await Task.Delay(100);

        var act = async () => await processor.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
