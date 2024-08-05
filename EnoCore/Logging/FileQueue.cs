namespace EnoCore.Logging
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class FileQueue : IDisposable
    {
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private readonly StreamWriter writer;
        private readonly CancellationToken cancelToken;

        public FileQueue(string filename, CancellationToken cancelToken)
        {
            this.writer = new StreamWriter(new GZipStream(new FileStream(filename, FileMode::Append), CompressionMode.Compress));
            this.cancelToken = cancelToken;
            Task.Run(this.WriterTask, cancelToken);
        }

        public void Dispose()
        {
            this.writer.Dispose();
        }

        public void Enqueue(string data)
        {
            this.cancelToken.ThrowIfCancellationRequested();
            this.queue.Enqueue(data);
        }

        private async Task WriterTask()
        {
            int i = 0;
            while (!this.cancelToken.IsCancellationRequested)
            {
                try
                {
                    if (this.queue.TryDequeue(out var data))
                    {
                        await this.writer.WriteAsync(data);
                        i += 1;
                        if (i == 50)
                        {
                            await this.writer.FlushAsync();
                            i = 0;
                        }
                    }
                    else
                    {
                        await this.writer.FlushAsync();
                        await Task.Delay(100);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToFancyStringWithCaller());
                }
            }

            await this.writer.FlushAsync();
            this.writer.Close();
        }
    }
}
