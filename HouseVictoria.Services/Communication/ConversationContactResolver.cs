namespace HouseVictoria.Services.Communication
{
    /// <summary>
    /// Parses <c>conv-{contactId}</c> and <c>conv-{contactId}-{suffix}</c> conversation ids.
    /// </summary>
    internal static class ConversationContactResolver
    {
        public static string ExtractContactId(string conversationId, IEnumerable<string>? knownContactIds = null)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return conversationId;

            if (!conversationId.StartsWith("conv-", StringComparison.OrdinalIgnoreCase))
                return conversationId;

            var payload = conversationId.Substring(5);

            if (knownContactIds != null)
            {
                foreach (var id in knownContactIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    if (payload.Equals(id, StringComparison.Ordinal) ||
                        payload.StartsWith(id + "-", StringComparison.Ordinal))
                    {
                        return id;
                    }
                }
            }

            if (Guid.TryParse(payload, out _))
                return payload;

            // conv-{guid}-{callSuffix}: contact id is the first 36-char GUID segment.
            if (payload.Length > 36)
            {
                var candidate = payload.Substring(0, 36);
                if (Guid.TryParse(candidate, out _))
                    return candidate;
            }

            // Legacy fallback (wrong for GUIDs — kept only when nothing else matches).
            var parts = conversationId.Split('-');
            return parts.Length >= 2 ? parts[1] : conversationId;
        }
    }
}
