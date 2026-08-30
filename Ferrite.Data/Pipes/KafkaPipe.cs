// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Net;
using System.Threading.Channels;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Ferrite.Data.Pipes;

public sealed class KafkaPipe : IMessagePipe, IAsyncDisposable
{
    private readonly IProducer<Null, byte[]> _producer;
    private readonly IAdminClient _adminClient;
    private IConsumer<Ignore, byte[]>? _consumer;
    private string? _channel;
    private Task? _consumeTask;
    private readonly CancellationTokenSource _consumeCts = new();
    private readonly Channel<byte[]> _consumed = Channel.CreateUnbounded<byte[]>();

    public KafkaPipe(string config)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config,
            ClientId = Dns.GetHostName()
        };
        _producer = new ProducerBuilder<Null, byte[]>(producerConfig).Build();
        _adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = config }).Build();
        Configuration = config;
    }

    private string Configuration { get; }

    public async ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken = default)
    {
        return await _consumed.Reader.ReadAsync(cancellationToken);
    }

    public async ValueTask<bool> SubscribeAsync(string channel)
    {
        if (_channel != null)
        {
            throw new InvalidOperationException("The pipe is already subscribed.");
        }
        await CreateChannelAsync(channel);
        var ready = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = Configuration,
            GroupId = $"ferrite-node-{channel}",
            AutoOffsetReset = AutoOffsetReset.Latest,
        };
        _consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
            .SetPartitionsAssignedHandler((consumer, partitions) =>
            {
                List<TopicPartitionOffset> committed = consumer.Committed(
                    partitions, TimeSpan.FromSeconds(10));
                return committed.Select(offset => offset.Offset == Offset.Unset
                    ? new TopicPartitionOffset(offset.TopicPartition, Offset.End)
                    : offset);
            })
            .Build();
        _channel = channel;
        _consumeTask = Task.Factory.StartNew(() => Consume(channel, ready),
            CancellationToken.None, TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return true;
    }

    private void Consume(string channel, TaskCompletionSource ready)
    {
        IConsumer<Ignore, byte[]> consumer = _consumer ??
            throw new InvalidOperationException("Kafka consumer was not initialized");
        try
        {
            consumer.Subscribe(channel);
            while (!_consumeCts.IsCancellationRequested)
            {
                try
                {
                    ConsumeResult<Ignore, byte[]>? consumeResult = consumer.Consume(
                        TimeSpan.FromMilliseconds(100));
                    if (consumer.Assignment.Count > 0)
                    {
                        ready.TrySetResult();
                    }
                    if (consumeResult != null)
                    {
                        _consumed.Writer.TryWrite(consumeResult.Message.Value);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code is
                           ErrorCode.UnknownTopicOrPart or
                           ErrorCode.LeaderNotAvailable)
                {
                    Thread.Sleep(100);
                }
            }
        }
        catch (OperationCanceledException) when (_consumeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            _consumed.Writer.TryComplete(ex);
        }
        finally
        {
            consumer.Close();
            ready.TrySetCanceled();
            _consumed.Writer.TryComplete();
        }
    }

    private async ValueTask<bool> CreateChannelAsync(string channel)
    {
        try
        {
            await _adminClient.CreateTopicsAsync([
                new TopicSpecification
                {
                    Name = channel,
                    ReplicationFactor = 1,
                    NumPartitions = 1,
                }
            ]);
        }
        catch (CreateTopicsException e) when (e.Results.All(result =>
                   result.Error.Code == ErrorCode.TopicAlreadyExists))
        {
        }
        return true;
    }

    public async ValueTask<bool> UnSubscribeAsync()
    {
        if (_channel != null)
        {
            _consumeCts.Cancel();
            if (_consumeTask != null)
            {
                await _consumeTask;
            }
            _channel = null;
        }
        return true;
    }

    public async ValueTask<bool> WriteMessageAsync(string channel, byte[] message)
    {
        await _producer.ProduceAsync(channel, new Message<Null, byte[]> { Value = message });
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await UnSubscribeAsync();
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _consumer?.Dispose();
        _adminClient.Dispose();
        _consumeCts.Dispose();
    }
}
