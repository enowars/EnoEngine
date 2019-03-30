using EnoCore;
using EnoEngine.Game;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnoEngine.FlagSubmission
{
    class FlagSubmissionEndpoint
    {
        private static readonly ILogger Logger = EnoCoreUtils.Loggers.CreateLogger<FlagSubmissionEndpoint>();
        readonly CancellationToken Token;
        readonly TcpListener Listener = new TcpListener(IPAddress.Any, 1337);
        readonly IFlagSubmissionHandler Handler;

        public FlagSubmissionEndpoint(IFlagSubmissionHandler handler, CancellationToken token)
        {
            Token = token;
            Token.Register(() => Listener.Stop());
            Handler = handler;
        }

        public async Task Run()
        {
            try
            {
                Listener.Start();
                while (!Token.IsCancellationRequested)
                {
                    var client = await Listener.AcceptTcpClientAsync();
                    var clientTask = HandleSubmissionClient(client);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogError($"FlagSubmissionEndpoint failed: {e.Message}\n{e.StackTrace}");
            }
            Logger.LogDebug("FlagSubmissionEndpoint finished");
        }

        private static string FormatSubmissionResult(FlagSubmissionResult result)
        {
            switch (result)
            {
                case FlagSubmissionResult.Ok:
                    return "Ok";
                case FlagSubmissionResult.Invalid:
                    return "You have submitted an invalid string!";
                case FlagSubmissionResult.Duplicate:
                    return "You have already sent this flag!";
                case FlagSubmissionResult.Own:
                    return "This flag belongs to you!";
                case FlagSubmissionResult.Old:
                    return "You have submitted an old flag!";
                case FlagSubmissionResult.UnknownError:
                    return "An unexpected error occured :(";
                case FlagSubmissionResult.InvalidSenderError:
                    return "Your IP address does not belong to any team's subnet!";
                default:
                    return "An even more unexpected rrror occured :(";
            }
        }

        public async Task HandleSubmissionClient(TcpClient client)
        {
            using (StreamReader reader = new StreamReader(client.GetStream()))
            {
                string line = await reader.ReadLineAsync();
                while (!Token.IsCancellationRequested && line != null)
                {
                    Logger.LogTrace($"FlagSubmissionEndpoint received {line}");
                    var endpoint = (IPEndPoint) client.Client.RemoteEndPoint;
                    var result = (await Handler.HandleFlagSubmission(line, endpoint.Address.ToString()));
                    var resultArray = Encoding.ASCII.GetBytes(FormatSubmissionResult(result) + "\n");
                    await client.GetStream().WriteAsync(resultArray, 0, resultArray.Length);
                    line = await reader.ReadLineAsync();
                }
            }
        }
    }
}
