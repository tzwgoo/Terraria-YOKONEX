using System;
using System.Collections.Generic;

namespace TerrariaYokonex.Core.Models
{
    public sealed class TerrariaEventRecord
    {
        public string EventKey { get; set; } = "";

        public string DisplayText { get; set; } = "";

        public string MatchValue { get; set; } = "";

        public int Amount { get; set; }

        public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

        public List<string> MatchCandidates { get; set; } = new List<string>();

        public bool Matches(string filter)
        {
            string normalizedFilter = Normalize(filter);
            if (string.IsNullOrWhiteSpace(normalizedFilter))
            {
                return true;
            }

            foreach (string candidate in MatchCandidates)
            {
                string normalizedCandidate = Normalize(candidate);
                if (normalizedCandidate.Contains(normalizedFilter, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static TerrariaEventRecord Create(
            string eventKey,
            string displayText,
            string matchValue,
            int amount,
            params string[] extraCandidates)
        {
            TerrariaEventRecord record = new TerrariaEventRecord
            {
                EventKey = eventKey ?? string.Empty,
                DisplayText = displayText ?? string.Empty,
                MatchValue = matchValue ?? string.Empty,
                Amount = amount,
                OccurredAt = DateTimeOffset.UtcNow,
            };

            HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            record.TryAddCandidate(candidates, record.EventKey);
            record.TryAddCandidate(candidates, record.DisplayText);
            record.TryAddCandidate(candidates, record.MatchValue);

            if (extraCandidates != null)
            {
                foreach (string extraCandidate in extraCandidates)
                {
                    record.TryAddCandidate(candidates, extraCandidate);
                }
            }

            record.MatchCandidates.AddRange(candidates);
            return record;
        }

        private void TryAddCandidate(HashSet<string> candidates, string value)
        {
            string normalized = Normalize(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
            }
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
