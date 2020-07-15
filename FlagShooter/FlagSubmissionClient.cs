using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FlagShooter
{
    class FlagSubmissionClient
    {
        public static async Task<FlagSubmissionClient> Create(ChannelReader<byte[]> flagsReader, long teamId, string address = "localhost")
        {
            var client = new TcpClient();
            await client.ConnectAsync(address, 1338);
            await client.Client.SendAsync(Encoding.ASCII.GetBytes($"{teamId}\n"), SocketFlags.None);
            return new FlagSubmissionClient(flagsReader, client);
        }
        private readonly ChannelReader<byte[]> FlagsReader;
        private readonly TcpClient Client;
        private FlagSubmissionClient(ChannelReader<byte[]> flagsReader, TcpClient client)
        {
            FlagsReader = flagsReader;
            Client = client;
            Task.Run(Send);
            Task.Run(Receive);
        }

        private async Task Receive()
        {
            try
            {
                byte[] buf = new byte[2048];
                while (true)
                {
                    var _ = await Client.Client.ReceiveAsync(buf, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{nameof(Receive)} failed: {e.Message}");
            }
        }

        private async Task Send()
        {
            try
            {
                while (true)
                {
                    var flag = await FlagsReader.ReadAsync();
                    await Client.Client.SendAsync(flag, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{nameof(Send)} failed: {e.Message}");
            }
        }
    }
}
