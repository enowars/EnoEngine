﻿namespace EnoFlagSink;

public class FlagSubmissionClientHandler
{
    private static readonly byte[] ProdWelcomeBanner = Encoding.UTF8.GetBytes(@"Welcome to the EnoEngine's EnoFlagSink™!
Please submit one flag per line. Responses are NOT guaranteed to be in chronological order.

".Replace("\r", string.Empty));

    private static readonly byte[] DevWelcomeBanner = Encoding.UTF8.GetBytes(@"Welcome to the EnoEngine's EnoFlagSink™!
Please submit your team id first, and then one flag per line. Responses are NOT guaranteed to be in chronological order.

".Replace("\r", string.Empty));

    private readonly byte[] flagSigningKeyBytes;
    private readonly FlagEncoding flagEncoding;
    private readonly long teamId;
    private readonly Socket socket;
    private readonly Pipe inputPipe;
    private readonly Channel<(byte[] Input, FlagSubmissionResult Result)> feedbackChannel;
    private readonly Channel<FlagSubmissionRequest> teamChannel;
    private readonly TeamFlagSubmissionStatistic teamFlagSubmissionStatistic;
    private readonly ILogger<FlagSubmissionClientHandler> logger;
    private readonly CancellationToken token;

    private FlagSubmissionClientHandler(
        IServiceProvider serviceProvider,
        byte[] flagSigningKeyBytes,
        FlagEncoding flagEncoding,
        long teamId,
        Channel<FlagSubmissionRequest> teamChannel,
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
        this.feedbackChannel = Channel.CreateBounded<(byte[] Input, FlagSubmissionResult Result)>(
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
        Dictionary<long, Channel<FlagSubmissionRequest>> teamChannels,
        Dictionary<long, TeamFlagSubmissionStatistic> teamFlagSubmissionStatistics,
        Socket socket,
        CancellationToken token)
    {
        await socket.SendAsync(DevWelcomeBanner, SocketFlags.None, token);
        var inputPipe = new Pipe();
        var t1 = Task.Run(() => ReadFromSocket(socket, inputPipe.Writer, token));
        long readTeamId = 0;
        var result = await EnoFlagSinkUtil.ReadLines(
            inputPipe.Reader,
            async (line) =>
            {
                var lineString = EncodingExtensions.GetString(Encoding.ASCII, line);
                if (!long.TryParse(lineString, out readTeamId))
                {
                    socket.Close();
                    await inputPipe.Reader.CompleteAsync();
                    throw new InvalidOperationException($"{lineString} was no valid teamId");
                }

                return false;
            },
            token);
        if (result != EnoFlagSinkUtil.ReadLinesResult.Success)
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

    public static async Task<FlagSubmissionClientHandler> HandleProdConnection(
        IServiceProvider serviceProvider,
        byte[] flagSigningKeyBytes,
        FlagEncoding flagEncoding,
        long teamId,
        Channel<FlagSubmissionRequest> teamChannel,
        TeamFlagSubmissionStatistic teamFlagSubmissionStatistic,
        Socket socket,
        CancellationToken token)
    {
        await socket.SendAsync(ProdWelcomeBanner, SocketFlags.None, token);
        var inputPipe = new Pipe();
        var readFromSocketTask = Task.Run(() => ReadFromSocket(socket, inputPipe.Writer, token));
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
        var readFromFeedbackChannelTask = Task.Run(handler.ReadFromFeedbackChannel);
        var readFromInputPipeTask = Task.Run(handler.ReadFromInputPipe);
        return handler;
    }

    /// <summary>
    /// Reads from the tcp socket into the input pipe until the handler is cancelled, or the connection is closed.
    /// </summary>
    private static async Task ReadFromSocket(Socket socket, PipeWriter inputPipeWriter, CancellationToken token)
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
    private async Task ReadFromInputPipe()
    {
        try
        {
            while (true)
            {
                var result = await EnoFlagSinkUtil.ReadLines(
                    this.inputPipe.Reader,
                    async (line) =>
                    {
                        var flag = Flag.Parse(line, this.flagSigningKeyBytes, this.flagEncoding, this.logger);
                        if (flag == null)
                        {
                            await this.feedbackChannel.Writer.WriteAsync((line.ToArray(), FlagSubmissionResult.Invalid), this.token);
                        }
                        else if (flag.OwnerId == this.teamId)
                        {
                            await this.feedbackChannel.Writer.WriteAsync((line.ToArray(), FlagSubmissionResult.Own), this.token);
                        }
                        else
                        {
                            await this.teamChannel.Writer.WriteAsync(
                                new FlagSubmissionRequest(
                                    line.ToArray(), flag, this.teamId, this.feedbackChannel.Writer),
                                this.token);
                        }

                        return true;
                    },
                    this.token);
                if (result == EnoFlagSinkUtil.ReadLinesResult.TooLong)
                {
                    await this.feedbackChannel.Writer.WriteAsync((new byte[0], FlagSubmissionResult.Error), this.token);
                    break;
                }
                else if (result == EnoFlagSinkUtil.ReadLinesResult.PipeComplete)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            this.logger.LogError($"{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // Mark the PipeReader and channel as complete.
            await this.inputPipe.Reader.CompleteAsync();
            this.feedbackChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Reads submission results from the feedback channel and sends them to the client.
    /// </summary>
    /// <remarks>
    /// TODO Implement while (await channelReader.WaitToReadAsync()) while (channelReader.TryRead(out T item)) pattern.
    /// TODO Statistics are being tracked here, so we are actually losing statistics if clients disconnect while flags are on the wire.
    /// </remarks>
    private async Task ReadFromFeedbackChannel()
    {
        try
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

                var response = input.Concat(result.ToFeedbackBytes()).ToArray();
                await this.socket.SendAsync(response, SocketFlags.None, this.token);
                if (result == FlagSubmissionResult.Error)
                {
                    this.socket.Close();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            if (e is not ChannelClosedException && e is not SocketException)
            {
                this.logger.LogError($"{e.Message}\n{e.StackTrace}");
            }
        }
        finally
        {
            this.socket.Close();
        }
    }
}
