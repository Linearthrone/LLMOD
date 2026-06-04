namespace HouseVictoria.Core.Models
{
    /// <summary>
    /// Controls which classes of information are injected into this persona's chat context.
    /// Companions default to user basics + their own memories only; the primary house persona
    /// may additionally see house journals.
    /// </summary>
    public class PersonaKnowledgeSharing
    {
        /// <summary>Global store: basics about the human user (name, preferences, etc.).</summary>
        public bool ShareUserBasics { get; set; } = true;

        /// <summary>This persona's own long-term chat memories.</summary>
        public bool ShareOwnMemories { get; set; } = true;

        /// <summary>Data bank entries from banks named for this persona.</summary>
        public bool ShareOwnDataBank { get; set; } = true;

        /// <summary>House-wide research journals (autonomy / primary persona work).</summary>
        public bool ShareHouseJournals { get; set; }

        /// <summary>Memories stored under other AI contact ids.</summary>
        public bool ShareOtherPersonaMemories { get; set; }

        /// <summary>Data banks not tied to this persona's name.</summary>
        public bool ShareSharedDataBanks { get; set; }

        /// <summary>
        /// When true, persisted sharing was written by the user; do not auto-reset on role change in the UI.
        /// </summary>
        public bool IsConfigured { get; set; }

        public static PersonaKnowledgeSharing CreateCompanionDefaults() => new()
        {
            ShareUserBasics = true,
            ShareOwnMemories = true,
            ShareOwnDataBank = true,
            ShareHouseJournals = false,
            ShareOtherPersonaMemories = false,
            ShareSharedDataBanks = false,
            IsConfigured = true
        };

        public static PersonaKnowledgeSharing CreatePrimaryDefaults() => new()
        {
            ShareUserBasics = true,
            ShareOwnMemories = true,
            ShareOwnDataBank = true,
            ShareHouseJournals = true,
            ShareOtherPersonaMemories = false,
            ShareSharedDataBanks = false,
            IsConfigured = true
        };

        /// <summary>
        /// Resolves effective sharing for a contact, migrating legacy personas that have no flags set.
        /// </summary>
        public static PersonaKnowledgeSharing Resolve(AIContact contact)
        {
            var s = contact.KnowledgeSharing;
            if (s == null)
                return contact.IsPrimaryAI || contact.Role == PersonaRole.Primary
                    ? CreatePrimaryDefaults()
                    : CreateCompanionDefaults();

            if (s.IsConfigured)
                return s;

            // Legacy record: all properties default to false in JSON — apply role-based defaults once.
            return contact.IsPrimaryAI || contact.Role == PersonaRole.Primary
                ? CreatePrimaryDefaults()
                : CreateCompanionDefaults();
        }

        public void CopyFrom(PersonaKnowledgeSharing other)
        {
            ShareUserBasics = other.ShareUserBasics;
            ShareOwnMemories = other.ShareOwnMemories;
            ShareOwnDataBank = other.ShareOwnDataBank;
            ShareHouseJournals = other.ShareHouseJournals;
            ShareOtherPersonaMemories = other.ShareOtherPersonaMemories;
            ShareSharedDataBanks = other.ShareSharedDataBanks;
            IsConfigured = other.IsConfigured;
        }

        public PersonaKnowledgeSharing Clone() => new()
        {
            ShareUserBasics = ShareUserBasics,
            ShareOwnMemories = ShareOwnMemories,
            ShareOwnDataBank = ShareOwnDataBank,
            ShareHouseJournals = ShareHouseJournals,
            ShareOtherPersonaMemories = ShareOtherPersonaMemories,
            ShareSharedDataBanks = ShareSharedDataBanks,
            IsConfigured = IsConfigured
        };
    }
}
