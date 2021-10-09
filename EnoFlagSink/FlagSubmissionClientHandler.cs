namespace EnoFlagSink
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using EnoCore;
    using EnoCore.Models;
    using EnoCore.Models.Database;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using static EnoFlagSink.FlagSubmissionEndpoint;

    public class FlagSubmissionClientHandler
    {
        private readonly byte[] flagSigningKeyBytes;
        private readonly FlagEncoding flagEncoding;
        private readonly long teamId;
        private readonly Socket socket;
        private readonly Pipe inputPipe;
        private readonly Channel<(string Input, FlagSubmissionResult Result)> feedbackChannel;
        private readonly Channel<(string FlagString, Flag Flag, ChannelWriter<(string, FlagSubmissionResult)> ResultWriter)> teamChannel;
        private readonly TeamFlagSubmissionStatistic teamFlagSubmissionStatistic;
        private readonly ILogger<FlagSubmissionClientHandler> logger;
        private readonly CancellationToken token;

        private FlagSubmissionClientHandler(
            IServiceProvider serviceProvider,
            byte[] flagSigningKeyBytes,
            FlagEncoding flagEncoding,
            long teamId,
            Channel<(string FlagString, Flag Flag, ChannelWriter<(string Input, FlagSubmissionResult Result)> FeedbackChannelWriter)> teamChannel,
            TeamFlagSubmissionStatistic teamFlagSubmissionStatistic,
            Socket socket,
            Pipe inputPipe,
            CancellationToken token)
        {
            this.flagSigningKeyBytes = flagSigningKeyBytes;
            this.flagEncoding = flagEncoding;
            this.teamId = teamId;
            this.socket = socket;
            this.inputPipe = inputPipe;
            this.feedbackChannel = Channel.CreateBounded<(string Input, FlagSubmissionResult Result)>(
                new BoundedChannelOptions(10000)
                {
                    SingleReader = true,
                    SingleWriter = false,
                });
            this.teamChannel = teamChannel;
            this.teamFlagSubmissionStatistic = teamFlagSubmissionStatistic;
            this.logger = serviceProvider.GetRequiredService<ILogger<FlagSubmissionClientHandler>>();
            this.token = token;
        }

        public static async Task<FlagSubmissionClientHandler> HandleDevConnection(
            IServiceProvider serviceProvider,
            byte[] flagSigningKeyBytes,
            FlagEncoding flagEncoding,
            ImmutableDictionary<long, Channel<(string FlagString, Flag Flag, ChannelWriter<(string Input, FlagSubmissionResult Result)> FeedbackChannelWriter)>> teamChannels,
            ImmutableDictionary<long, TeamFlagSubmissionStatistic> teamFlagSubmissionStatistics,
            Socket socket,
            CancellationToken token)
        {
            var inputPipe = new Pipe();
            var t1 = Task.Run(() => ReadFromSocket(socket, inputPipe.Writer, token));
            long readTeamId = 0;
            if (!await EnoFlagSinkUtils.ReadLine(
                inputPipe.Reader,
                async (ros) =>
                {
                    if (!long.TryParse(ros.ToString(), out readTeamId))
                    {
                        socket.Close();
                        await inputPipe.Reader.CompleteAsync();
                        throw new InvalidOperationException();
                    }
                },
                token))
            {
                socket.Close();
                throw new InvalidOperationException("DebugEndpoint received bad teamid");
            }

            var handler = new FlagSubmissionClientHandler(
                serviceProvider,
                flagSigningKeyBytes,
                flagEncoding,
                readTeamId,
                teamChannels[readTeamId],
                teamFlagSubmissionStatistics[readTeamId],
                socket,
                inputPipe,
                token);
            var t2 = Task.Run(handler.ReadFromFeedbackChannel);
            var t3 = Task.Run(handler.ReadFromInputPipe);
            return handler;
        }

        public static FlagSubmissionClientHandler HandleProdConnection(
            IServiceProvider serviceProvider,
            byte[] flagSigningKeyBytes,
            FlagEncoding flagEncoding,
            long teamId,
            Channel<(string FlagString, Flag Flag, ChannelWriter<(string Input, FlagSubmissionResult Result)> FeedbackChannelWriter)> teamChannel,
            TeamFlagSubmissionStatistic teamFlagSubmissionStatistic,
            Socket socket,
            CancellationToken token)
        {
            var inputPipe = new Pipe();
            Task.Run(() => ReadFromSocket(socket, inputPipe.Writer, token));
            var handler = new FlagSubmissionClientHandler(
                serviceProvider,
                flagSigningKeyBytes,
                flagEncoding,
                teamId,
                teamChannel,
                teamFlagSubmissionStatistic,
                socket,
                inputPipe,
                token);
            Task.Run(handler.ReadFromFeedbackChannel);
            Task.Run(handler.ReadFromInputPipe);
            return handler;
        }

        /// <summary>
        /// Reads from the tcp socket into the input pipe until the handler is cancelled, or the connection is closed.
        /// </summary>
        private static async void ReadFromSocket(Socket socket, PipeWriter inputPipeWriter, CancellationToken token)
        {
            const int minimumBufferSize = 512;
            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter.
                Memory<byte> memory = inputPipeWriter.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, token);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read from the Socket.
                    inputPipeWriter.Advance(bytesRead);
                }
                catch
                {
                    break;
                }

                // Make the data available to the PipeReader.
                FlushResult result = await inputPipeWriter.FlushAsync(token);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // By completing PipeWriter, tell the PipeReader that there's no more data coming.
            await inputPipeWriter.CompleteAsync();
        }

        /// <summary>
        /// Parses the input pipe for submitted flags, and enqueues valid flags into the team's channel.
        /// </summary>
        /// <remarks>
        /// Invalid flag submissions responses are enqueued without involving the team's channel.
        /// When no more input can be read from the input pipe the connection was closed, so the pipe reader and feedback channel writer are closed.
        /// </remarks>
        private async void ReadFromInputPipe()
        {
            while (true)
            {
                if (!await EnoFlagSinkUtils.ReadLine(
                    this.inputPipe.Reader,
                    async (line) =>
                    {
                        var flag = Flag.Parse(line, this.flagSigningKeyBytes, this.flagEncoding, this.logger);
                        if (flag == null)
                        {
                            await this.feedbackChannel.Writer.WriteAsync((line.ToString(), FlagSubmissionResult.Invalid), this.token);
                        }
                        else if (flag.OwnerId == this.teamId)
                        {
                            await this.feedbackChannel.Writer.WriteAsync((line.ToString(), FlagSubmissionResult.Own), this.token);
                        }
                        else
                        {
                            await this.teamChannel.Writer.WriteAsync((line.ToString(), flag, this.feedbackChannel.Writer), this.token);
                        }
                    },
                    this.token))
                {
                    await this.feedbackChannel.Writer.WriteAsync((string.Empty, FlagSubmissionResult.SpamError), this.token);
                    break;
                }
            }

            // Mark the PipeReader and channel as complete.
            await this.inputPipe.Reader.CompleteAsync();
            this.feedbackChannel.Writer.Complete();
        }

        /// <summary>
        /// Reads submission results from the feedback channel and sends them to the client.
        /// </summary>
        /// <remarks>
        /// TODO Statistics are being tracked here, so we are actually losing statistics if clients disconnect while flags are on the wire.
        /// </remarks>
        private async void ReadFromFeedbackChannel()
        {
            while (true)
            {
                (var input, var result) = await this.feedbackChannel.Reader.ReadAsync(this.token);
                switch (result)
                {
                case FlagSubmissionResult.Ok:
                    Interlocked.Increment(ref this.teamFlagSubmissionStatistic.OkFlags);
                    break;
                case FlagSubmissionResult.Duplicate:
                    Interlocked.Increment(ref this.teamFlagSubmissionStatistic.DuplicateFlags);
                    break;
                case FlagSubmissionResult.Invalid:
                    Interlocked.Increment(ref this.teamFlagSubmissionStatistic.InvalidFlags);
                    break;
                case FlagSubmissionResult.Old:
                    Interlocked.Increment(ref this.teamFlagSubmissionStatistic.OldFlags);
                    break;
                case FlagSubmissionResult.Own:
                    Interlocked.Increment(ref this.teamFlagSubmissionStatistic.OwnFlags);
                    break;
                }

                var itemBytes = Encoding.ASCII.GetBytes(result.ToUserFriendlyString()); // TODO don't serialize every time
                await this.socket.SendAsync(itemBytes, SocketFlags.None, this.token);
                if (result == FlagSubmissionResult.SpamError)
                {
                    // Wait for the send to have a chance to complete
                    // https://blog.netherlabs.nl/articles/2009/01/18/the-ultimate-so_linger-page-or-why-is-my-tcp-not-reliable
                    await Task.Delay(1000, this.token);
                    this.socket.Close();
                    break;
                }
            }
        }
    }
}
