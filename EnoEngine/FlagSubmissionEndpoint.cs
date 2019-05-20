using EnoCore;
using EnoCore.Models.Json;
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
        private static readonly EnoLogger Logger = new EnoLogger(nameof(EnoEngine));
        readonly CancellationToken Token;
        readonly TcpListener Listener = new TcpListener(IPAddress.IPv6Any, 1337);
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
            catch (ObjectDisposedException) { }
            catch (TaskCanceledException) { }
            catch (Exception e)
            {
                Logger.LogFatal(new EnoLogMessage()
                {
                    Module = nameof(FlagSubmission),
                    Function = nameof(Run),
                    Message = $"FlagSubmissionEndpoint failed: {EnoCoreUtils.FormatException(e)}"
                });
            }
            Logger.LogInfo(new EnoLogMessage()
            {
                Module = nameof(FlagSubmission),
                Function = nameof(Run),
                Message = "FlagSubmissionEndpoint finished"
            });
        }

        private static string FormatSubmissionResult(FlagSubmissionResult result)
        {
            switch (result)
            {
                case FlagSubmissionResult.Ok:
                    return "VALID: Flag accepted!";
                case FlagSubmissionResult.Invalid:
                    return "INVALID: You have submitted an invalid string!";
                case FlagSubmissionResult.Duplicate:
                    return "RESUBMIT: You have already sent this flag!";
                case FlagSubmissionResult.Own:
                    return "OWNFLAG: This flag belongs to you!";
                case FlagSubmissionResult.Old:
                    return "OLD: You have submitted an old flag!";
                case FlagSubmissionResult.UnknownError:
                    return "ERROR: An unexpected error occured :(";
                case FlagSubmissionResult.InvalidSenderError:
                    return "ILLEGAL: Your IP address does not belong to any team's subnet!";
                default:
                    return "ERROR: An even more unexpected rrror occured :(";
            }
        }

        public async Task HandleSubmissionClient(TcpClient client)
        {
            var attackerAddress = ((IPEndPoint) client.Client.RemoteEndPoint).Address.GetAddressBytes();
            var attackerPrefix = new byte[Program.Configuration.TeamSubnetBytesLength];
            Array.Copy(attackerAddress, attackerPrefix, Program.Configuration.TeamSubnetBytesLength);
            var attackerPrefixString = BitConverter.ToString(attackerPrefix);

            using (StreamReader reader = new StreamReader(client.GetStream()))
            {
                string line = await reader.ReadLineAsync();
                while (!Token.IsCancellationRequested && line != null)
                {
                    var endpoint = (IPEndPoint) client.Client.RemoteEndPoint;
                    var result = await Handler.HandleFlagSubmission(line, attackerPrefixString);
                    var resultArray = Encoding.ASCII.GetBytes(FormatSubmissionResult(result) + "\n");
                    await client.GetStream().WriteAsync(resultArray, 0, resultArray.Length);
                    line = await reader.ReadLineAsync();
                }
            }
        }
    }
}
