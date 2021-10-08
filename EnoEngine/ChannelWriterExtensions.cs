namespace EnoEngine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    public static class ChannelWriterExtensions
    {
        public static void TrySendOrClose<T>(this ChannelWriter<T> writer, T data)
        {
            if (!writer.TryWrite(data))
            {
                writer.TryComplete();
            }
        }
    }
}
