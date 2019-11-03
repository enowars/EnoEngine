using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnoCore.Logging
{
    public class FileQueue
    {
        private readonly ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
        private readonly StreamWriter Writer;
        private readonly CancellationToken CancelToken;

        public FileQueue(string filename, CancellationToken cancelToken)
        {
            Writer = new StreamWriter(filename);
            CancelToken = cancelToken;
            Task.Run(WriterTask);
        }

        public void Enqueue(string data)
        {
            CancelToken.ThrowIfCancellationRequested();
            Queue.Enqueue(data);
        }

        private async Task WriterTask()
        {
            int i = 0;
            while (!CancelToken.IsCancellationRequested)
            {
                try
                {
                    if (Queue.TryDequeue(out var data))
                    {
                        await Writer.WriteAsync(data);
                        i += 1;
                        if (i == 50)
                        {
                            await Writer.FlushAsync();
                            i = 0;
                        }
                    }
                    else
                    {
                        await Writer.FlushAsync();
                        await Task.Delay(100);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToFancyString());
                }
            }
            await Writer.FlushAsync();
            Writer.Close();
        }
    }
}
