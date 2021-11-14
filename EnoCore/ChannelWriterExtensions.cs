namespace EnoCore;

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
