using EnoCore.Models;
using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnoCore.Utils
{
    public class CheckerResultMessageJsonConverter : JsonConverter<CheckerResult>
    {
        private static readonly byte[] InternalErrorBytes = Encoding.ASCII.GetBytes("INTERNAL_ERROR");
        private static readonly byte[] OkBytes = Encoding.ASCII.GetBytes("OK");
        private static readonly byte[] MumbleBytes = Encoding.ASCII.GetBytes("MUMBLE");
        private static readonly byte[] OfflineBytes = Encoding.ASCII.GetBytes("OFFLINE");
        public override CheckerResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.ValueSpan.SequenceEqual(InternalErrorBytes))
            {
                reader.Read();
                if(reader.ValueSpan.SequenceEqual(InternalErrorBytes))
                {
                    return CheckerResult.InternalError;
                }
                else if (reader.ValueSpan.SequenceEqual(OkBytes))
                {
                    return CheckerResult.Ok;
                }
                else if (reader.ValueSpan.SequenceEqual(MumbleBytes))
                {
                    return CheckerResult.Mumble;
                }
                else if (reader.ValueSpan.SequenceEqual(OfflineBytes))
                {
                    return CheckerResult.Offline;
                }
            }
            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, CheckerResult value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            if (value == CheckerResult.InternalError)
            {
                writer.WriteString("result", InternalErrorBytes);
            }
            else if (value == CheckerResult.Ok)
            {
                writer.WriteString("result", OkBytes);
            }
            else if (value == CheckerResult.Mumble)
            {
                writer.WriteString("result", MumbleBytes);
            }
            else if (value == CheckerResult.Offline)
            {
                writer.WriteString("result", OfflineBytes);
            }
            else
            {
                throw new JsonException();
            }
            writer.WriteEndObject();
        }
    }
}