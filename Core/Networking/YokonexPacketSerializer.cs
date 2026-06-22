using System.Collections.Generic;
using System.IO;
using TerrariaYokonex.Core.Models;

namespace TerrariaYokonex.Core.Networking
{
    public static class YokonexPacketSerializer
    {
        public static void WriteEvent(BinaryWriter writer, TerrariaEventRecord eventRecord)
        {
            writer.Write(eventRecord.EventKey ?? string.Empty);
            writer.Write(eventRecord.DisplayText ?? string.Empty);
            writer.Write(eventRecord.MatchValue ?? string.Empty);
            writer.Write(eventRecord.Amount);
            writer.Write(eventRecord.MatchCandidates.Count);
            foreach (string candidate in eventRecord.MatchCandidates)
            {
                writer.Write(candidate ?? string.Empty);
            }
        }

        public static TerrariaEventRecord ReadEvent(BinaryReader reader)
        {
            TerrariaEventRecord eventRecord = new TerrariaEventRecord
            {
                EventKey = reader.ReadString(),
                DisplayText = reader.ReadString(),
                MatchValue = reader.ReadString(),
                Amount = reader.ReadInt32(),
            };

            int candidateCount = reader.ReadInt32();
            List<string> candidates = new List<string>(candidateCount);
            for (int index = 0; index < candidateCount; index++)
            {
                candidates.Add(reader.ReadString());
            }

            eventRecord.MatchCandidates = candidates;
            return eventRecord;
        }
    }
}
