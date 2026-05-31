namespace HouseVictoria.Core.Events
{
    /// <summary>Which selection changed.</summary>
    public enum PersonaSelectionKind
    {
        /// <summary>The always-active primary persona changed.</summary>
        Primary = 0,
        /// <summary>The "last activated in chat" secondary persona changed.</summary>
        Secondary = 1
    }

    /// <summary>
    /// Published when the primary (always-active) or secondary (last activated in chat)
    /// persona selection changes. Subscribers (trays, avatars, windows) can refresh.
    /// </summary>
    public class PersonaChangedEvent
    {
        public PersonaSelectionKind Kind { get; init; }

        /// <summary>The id of the newly selected persona (may be empty if cleared).</summary>
        public string ContactId { get; init; } = string.Empty;

        /// <summary>The id of the previously selected persona (may be empty).</summary>
        public string? PreviousContactId { get; init; }
    }
}
