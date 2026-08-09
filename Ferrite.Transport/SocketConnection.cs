// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Ferrite.Transport;

public sealed class SocketConnection : ITransportConnection
{
    private static readonly int MinAllocBufferSize = 1024;
    // TDLib accepts MTProto packets up to (1 << 22) + 1024 bytes. Buffered
    // containers must fit in the receive pipe or the producer can pause while
    // the frame decoder is waiting for the rest of the same packet.
    private const long ReceivePauseWriterThreshold = (1 << 22) + (64 * 1024);
    private const long ReceiveResumeWriterThreshold = ReceivePauseWriterThreshold / 2;

    private readonly Socket _socket;
    private readonly SocketReceiver _receiver;
    private SocketSender? _sender;
    private readonly SocketSenderPool _socketSenderPool;

    public EndPoint? LocalEndPoint { get; }
    public EndPoint? RemoteEndPoint { get; }
    public CancellationToken ConnectionClosed { get; }

    private readonly IDuplexPipe _originalTransport;
    private readonly CancellationTokenSource _connectionClosedTokenSource = new CancellationTokenSource();

    private readonly object _shutdownLock = new object();
    private volatile bool _socketDisposed;
    private volatile Exception? _shutdownReason;
    private Task? _sendingTask;
    private Task? _receivingTask;
    private readonly TaskCompletionSource _waitForConnectionClosedTcs = new TaskCompletionSource();
    private bool _connectionClosed;
    private readonly bool _waitForData;
    public MemoryPool<byte> MemoryPool { get; }
    public IDuplexPipe Transport { get; }
    public IDuplexPipe Application { get; }

    internal SocketConnection(Socket socket,
                              MemoryPool<byte> memoryPool,
                              PipeScheduler transportScheduler,
                              SocketSenderPool socketSenderPool,
                              bool waitForData = true)
    {
        Debug.Assert(socket != null);
        Debug.Assert(memoryPool != null);

        _socket = socket;
        MemoryPool = memoryPool;
        _waitForData = waitForData;
        _socketSenderPool = socketSenderPool;

        LocalEndPoint = _socket.LocalEndPoint;
        RemoteEndPoint = _socket.RemoteEndPoint;

        ConnectionClosed = _connectionClosedTokenSource.Token;

        // On *nix platforms, Sockets already dispatches to the ThreadPool.
        // Yes, the IOQueues are still used for the PipeSchedulers. This is intentional.
        // https://github.com/aspnet/KestrelHttpServer/issues/2573
        var awaiterScheduler = OperatingSystem.IsWindows() ? transportScheduler : PipeScheduler.Inline;

        _receiver = new SocketReceiver(awaiterScheduler);

        PipeOptions receivePipeOptions = new(
            pauseWriterThreshold: ReceivePauseWriterThreshold,
            resumeWriterThreshold: ReceiveResumeWriterThreshold,
            useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(receivePipeOptions, PipeOptions.Default);

        // Set the transport and connection id
        Transport = _originalTransport = pair.Transport;
        Application = pair.Application;
    }

    public PipeWriter Input => Application.Output;

    public PipeReader Output => Application.Input;

    

    public void Start()
    {
        try
        {
            // Spawn send and receive logic
            _receivingTask = DoReceive();
            _sendingTask = DoSend();
        }
        catch (Exception)
        {
            
        }
    }

    public void Abort(Exception abortReason)
    {
        // Try to gracefully close the socket to match libuv behavior.
        Shutdown(abortReason);

        // Cancel ProcessSends loop after calling shutdown to ensure the correct _shutdownReason gets set.
        Output.CancelPendingRead();
    }

    // Only called after connection middleware is complete which means the ConnectionClosed token has fired.
    public async ValueTask DisposeAsync()
    {
        _originalTransport.Input.Complete();
        _originalTransport.Output.Complete();

        try
        {
            // Now wait for both to complete
            if (_receivingTask != null)
            {
                await _receivingTask;
            }

            if (_sendingTask != null)
            {
                await _sendingTask;
            }

        }
        catch (Exception)
        {
            
        }
        finally
        {
            _receiver.Dispose();
            _sender?.Dispose();
        }

        _connectionClosedTokenSource.Dispose();
    }

    private async Task DoReceive()
    {
        Exception? error = null;

        try
        {
            while (true)
            {
                if (_waitForData)
                {
                    // Wait for data before allocating a buffer.
                    await _receiver.WaitForDataAsync(_socket);
                }

                // Ensure we have some reasonable amount of buffer space
                var buffer = Input.GetMemory(MinAllocBufferSize);

                var bytesReceived = await _receiver.ReceiveAsync(_socket, buffer);

                if (bytesReceived == 0)
                {
                    break;
                }

                Input.Advance(bytesReceived);

                var flushTask = Input.FlushAsync();

                var paused = !flushTask.IsCompleted;

                var result = await flushTask;

                if (result.IsCompleted || result.IsCanceled)
                {
                    // Pipe consumer is shut down, do we stop writing
                    break;
                }
            }
        }
        catch (SocketException ex) when (IsConnectionResetError(ex.SocketErrorCode))
        {
            // This could be ignored if _shutdownReason is already set.
            error = ex;
        }
        catch (Exception ex)
            when ((ex is SocketException socketEx && IsConnectionAbortError(socketEx.SocketErrorCode)) ||
                   ex is ObjectDisposedException)
        {
            // This exception should always be ignored because _shutdownReason should be set.
            error = ex;
        }
        catch (Exception ex)
        {
            // This is unexpected.
            error = ex;
        }
        finally
        {
            // If Shutdown() has already bee called, assume that was the reason ProcessReceives() exited.
            Input.Complete(_shutdownReason ?? error);

            FireConnectionClosed();

            await _waitForConnectionClosedTcs.Task;
        }
    }

    private async Task DoSend()
    {
        Exception? shutdownReason = null;
        Exception? unexpectedError = null;

        try
        {
            while (true)
            {
                var result = await Output.ReadAsync();

                if (result.IsCanceled)
                {
                    break;
                }
                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    _sender = _socketSenderPool.Rent();
                    await _sender.SendAsync(_socket, buffer);
                    // We don't return to the pool if there was an exception, and
                    // we keep the _sender assigned so that we can dispose it in StartAsync.
                    _socketSenderPool.Return(_sender);
                    _sender = null;
                }

                Output.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (SocketException ex) when (IsConnectionResetError(ex.SocketErrorCode))
        {
            shutdownReason = ex;
        }
        catch (Exception ex)
            when ((ex is SocketException socketEx && IsConnectionAbortError(socketEx.SocketErrorCode)) ||
                   ex is ObjectDisposedException)
        {
            // This should always be ignored since Shutdown() must have already been called by Abort().
            shutdownReason = ex;
        }
        catch (Exception ex)
        {
            shutdownReason = ex;
            unexpectedError = ex;
        }
        finally
        {
            Shutdown(shutdownReason);

            // Complete the output after disposing the socket
            Output.Complete(unexpectedError);

            // Cancel any pending flushes so that the input loop is un-paused
            Input.CancelPendingFlush();
        }
    }

    private void FireConnectionClosed()
    {
        // Guard against scheduling this multiple times
        if (_connectionClosed)
        {
            return;
        }

        _connectionClosed = true;

        ThreadPool.UnsafeQueueUserWorkItem(state =>
        {
            state.CancelConnectionClosedToken();

            state._waitForConnectionClosedTcs.TrySetResult();
        },
        this,
        preferLocal: false);
    }

    private void Shutdown(Exception? shutdownReason)
    {
        lock (_shutdownLock)
        {
            if (_socketDisposed)
            {
                return;
            }

            // Make sure to close the connection only after the _aborted flag is set.
            // Without this, the RequestsCanBeAbortedMidRead test will sometimes fail when
            // a BadHttpRequestException is thrown instead of a TaskCanceledException.
            _socketDisposed = true;

            // shutdownReason should only be null if the output was completed gracefully, so no one should ever
            // ever observe the nondescript ConnectionAbortedException except for connection middleware attempting
            // to half close the connection which is currently unsupported.
            _shutdownReason = shutdownReason ?? new Exception("The Socket transport's send loop completed gracefully.");
            
            try
            {
                // Try to gracefully close the socket even for aborts to match libuv behavior.
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore any errors from Socket.Shutdown() since we're tearing down the connection anyway.
            }

            _socket.Dispose();
        }
    }

    private void CancelConnectionClosedToken()
    {
        try
        {
            _connectionClosedTokenSource.Cancel();
        }
        catch (Exception)
        {
            
        }
    }

    private static bool IsConnectionResetError(SocketError errorCode)
    {
        return errorCode == SocketError.ConnectionReset ||
               errorCode == SocketError.Shutdown ||
               (errorCode == SocketError.ConnectionAborted && OperatingSystem.IsWindows());
    }

    private static bool IsConnectionAbortError(SocketError errorCode)
    {
        // Calling Dispose after ReceiveAsync can cause an "InvalidArgument" error on *nix.
        return errorCode == SocketError.OperationAborted ||
               errorCode == SocketError.Interrupted ||
               (errorCode == SocketError.InvalidArgument && !OperatingSystem.IsWindows());
    }
}
