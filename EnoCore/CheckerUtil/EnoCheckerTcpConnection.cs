namespace EnoCore.CheckerUtil
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Checker;
    using Microsoft.Extensions.Logging;

    public sealed class EnoCheckerTcpConnection : IDisposable
    {
        private readonly Pipe pipe = new();
        private readonly TcpClient client;
        private readonly Task fillPipeTask;

        private EnoCheckerTcpConnection(TcpClient client, CancellationToken token)
        {
            this.client = client;
            this.fillPipeTask = this.FillPipeAsync(token);
        }

        /// <summary>
        /// Establish a EnoCheckerTcpConnection.
        /// </summary>
        /// <param name="address">The destination address.</param>
        /// <param name="port">The destination port.</param>
        /// <param name="logger">A logger for error logging.</param>
        /// <param name="token">A CancellationToken to abort the task, and the internal receiving task.</param>
        /// <param name="errorMessage">An optional error message which is put into all exceptions.</param>
        /// <returns>A task representing the action.</returns>
        public static async Task<EnoCheckerTcpConnection> Connect(string address, int port, ILogger logger, CancellationToken token, string errorMessage = "Could not establish TCP connection")
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(address, port, token);
                return new EnoCheckerTcpConnection(client, token);
            }
            catch (Exception e)
            {
                logger.LogWarning(e.ToFancyString());
                throw new OfflineException(errorMessage);
            }
        }

        /// <summary>
        /// Send bytes through the TCP connection.
        /// </summary>
        /// <param name="buffer">The buffer to be sent.</param>
        /// <param name="logger">A logger for error logging.</param>
        /// <param name="token">A CancellationToken to abort the task.</param>
        /// <param name="errorMessage">An optional error message which is put into all exceptions.</param>
        /// <returns>A task representing the action.</returns>
        public async Task SendAsync(
            ReadOnlyMemory<byte> buffer,
            ILogger logger,
            CancellationToken token,
            string errorMessage = "Connection error")
        {
            try
            {
                var sent = await this.client.Client.SendAsync(buffer, SocketFlags.None, token);
                if (sent != buffer.Length)
                {
                    throw new InvalidOperationException("This should not happen on any modern operating system");
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.ToFancyString());
                throw new OfflineException(errorMessage);
            }
        }

        /// <summary>
        /// Read from the TCP connection until the buffer is full.
        /// </summary>
        /// <param name="destinationBuffer">The destination buffer.</param>
        /// <param name="logger">A logger for error logging.</param>
        /// <param name="token">A CancellationToken to abort the task.</param>
        /// <param name="errorMessage">An optional error message which is put into all exceptions.</param>
        /// <returns>A task representing the action.</returns>
        public async Task ReceiveAll(
            Memory<byte> destinationBuffer,
            ILogger logger,
            CancellationToken token,
            string errorMessage = "Connection error")
        {
            try
            {
                long i = 0;
                while (i < destinationBuffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    ReadResult result = await this.pipe.Reader.ReadAsync(token);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    // Copy buffer to destination buffer
                    var chunkSize = Math.Min(destinationBuffer.Length - i, result.Buffer.Length);
                    var chunk = buffer.Slice(0, chunkSize);
                    chunk.CopyTo(destinationBuffer[(int)i..].Span);

                    // Proclaim chunk consumed and nothing else examined
                    this.pipe.Reader.AdvanceTo(chunk.End, chunk.End);
                    i += chunkSize;

                    if (result.IsCompleted && i < destinationBuffer.Length - 1)
                    {
                        throw new Exception("Pipe completed before all bytes were read");
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.ToFancyString());
                throw new OfflineException(errorMessage);
            }
        }

        /// <summary>
        /// Read from the TCP connection until one of the delimiters is read.
        /// Raises the appropriate OfflineException if the connection breaks.
        /// </summary>
        /// <param name="delimiter">The delimiter.</param>
        /// <param name="logger">A logger for error logging.</param>
        /// <param name="token">A CancellationToken to abort the task.</param>
        /// <param name="errorMessage">An optional error message which is put into all exceptions.</param>
        /// <returns>byte[] containing the received bytes, including the found delimiter.</returns>
        public async Task<byte[]> ReceiveUntilAsync(
            ReadOnlyMemory<byte> delimiter,
            ILogger logger,
            CancellationToken token,
            string errorMessage = "Connection error")
        {
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    ReadResult result = await this.pipe.Reader.ReadAsync(token);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    if (new SequenceReader<byte>(buffer).TryReadTo(out ReadOnlySequence<byte> sequence, delimiter.Span))
                    {
                        var returnBuffer = sequence.ToArray();

                        // Proclaim portion consumed and nothing else examined
                        this.pipe.Reader.AdvanceTo(buffer.GetPosition(sequence.Length + delimiter.Length, buffer.Start));
                        return returnBuffer;
                    }
                    else
                    {
                        // Proclaim everything examined
                        this.pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                        if (result.IsCompleted)
                        {
                            throw new Exception("Pipe completed without delimiter");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e.ToFancyString());
                throw new OfflineException(errorMessage);
            }
        }

        public void Dispose()
        {
            this.client.Dispose();
            this.pipe.Writer.Complete();
            this.pipe.Reader.Complete();
        }

        private async Task FillPipeAsync(CancellationToken token)
        {
            const int minimumBufferSize = 4096;
            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter.
                Memory<byte> memory = this.pipe.Writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await this.client.Client.ReceiveAsync(memory, SocketFlags.None, token);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read from the Socket.
                    this.pipe.Writer.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                // Make the data available to the PipeReader.
                FlushResult result = await this.pipe.Writer.FlushAsync(token);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // By completing PipeWriter, tell the PipeReader that there's no more data coming.
            await this.pipe.Writer.CompleteAsync();
        }
    }
}
