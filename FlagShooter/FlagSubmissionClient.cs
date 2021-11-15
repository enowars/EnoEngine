namespace FlagShooter;

internal class FlagSubmissionClient
{
    private readonly ChannelReader<byte[]> flagsReader;
    private readonly TcpClient client;

    private FlagSubmissionClient(ChannelReader<byte[]> flagsReader, TcpClient client)
    {
        this.flagsReader = flagsReader;
        this.client = client;
        Task.Run(this.Send);
        Task.Run(this.Receive);
    }

    public static async Task<FlagSubmissionClient> Create(ChannelReader<byte[]> flagsReader, long teamId, string address = "localhost")
    {
        var client = new TcpClient();
        await client.ConnectAsync(address, 1338);
        await client.Client.SendAsync(Encoding.ASCII.GetBytes($"{teamId}\n"), SocketFlags.None);
        return new FlagSubmissionClient(flagsReader, client);
    }

    private async Task Receive()
    {
        StreamReader reader = new StreamReader(this.client.GetStream(), Encoding.ASCII);
        try
        {
            byte[] buf = new byte[2048];
            while (true)
            {
                string? result = await reader.ReadLineAsync();
                if (result == null || result == string.Empty)
                {
                    throw new Exception($"result empty (connected={this.client.Client.Connected})");
                }

                throw new NotImplementedException("TODO adapt new reponses");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"{nameof(this.Receive)} failed: {e.Message}");
        }
    }

    private async Task Send()
    {
        try
        {
            while (true)
            {
                var flag = await this.flagsReader.ReadAsync();
                await this.client.Client.SendAsync(flag, SocketFlags.None);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"{nameof(this.Send)} failed: {e.Message}");
        }
    }
}
