namespace EnoCore.Logging
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class FileQueue : IDisposable
    {
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private readonly CancellationToken cancelToken;

        private StreamWriter writer = default!;
        private readonly string filename;
        private FileSystemWatcher watcher;
        private volatile bool reopenRequested;

        public FileQueue(string filename, CancellationToken cancelToken)
        {
            this.filename = filename;
            this.cancelToken = cancelToken;

            OpenWriter();

            var dir = Path.GetDirectoryName(filename)!;
            var name = Path.GetFileName(filename);
            watcher = new FileSystemWatcher(dir, name)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            watcher.Created += (s, e) => reopenRequested = true;
            watcher.Renamed += (s, e) => reopenRequested = true;
            watcher.EnableRaisingEvents = true;
            this.cancelToken = cancelToken;
            Task.Run(this.WriterTask, cancelToken);
        }

        public void Dispose()
        {
            this.writer.Dispose();
            watcher?.Dispose();
            writer?.Dispose();
        }

        public void Enqueue(string data)
        {
            this.cancelToken.ThrowIfCancellationRequested();
            this.queue.Enqueue(data);
        }

        private void OpenWriter()
        {
            writer?.Dispose();
            var fs = new FileStream(
                filename,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.Asynchronous);
            writer = new StreamWriter(fs, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        private async Task WriterTask()
        {
            int i = 0;
            while (!this.cancelToken.IsCancellationRequested)
            {
                if (reopenRequested)
                {
                    OpenWriter();
                    reopenRequested = false;
                }
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
