namespace EnoFlagSink
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO.Pipelines;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class EnoFlagSinkUtil
    {
        private const int MaxLineLength = 200;

        public static async Task<bool> ReadLines(
            PipeReader pipeReader,
            Func<ReadOnlySequence<byte>, Task<bool>> handler,
            CancellationToken token)
        {
            bool running = true;
            while (running)
            {
                ReadResult result = await pipeReader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;
                while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    running = await handler(line);
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    break;
                }

                // TryReadLine has returned false, so the remaining buffer does not contain a \n.
                // If the length is longer than a flag, somebody is sending bullshit!
                // This is not the strict upper limit though, because the buffer might be bigger,
                // and thus yield strings longer than this check.
                if (buffer.Length > MaxLineLength)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
        {
            // Look for a EOL in the buffer.
            SequencePosition? position = buffer.PositionOf((byte)'\n');

            if (position == null)
            {
                line = default;
                return false;
            }

            // Skip the line + the \n.
            line = buffer.Slice(0, position.Value);
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
            return true;
        }
    }
}
