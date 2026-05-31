using HouseVictoria.Core.Events;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Single source of truth for which persona is <b>primary</b> (always active) and which is
    /// <b>secondary</b> (the one last activated in a chat conversation).
    /// <para>Replaces the ad-hoc <c>contacts.FirstOrDefault(c => c.IsPrimaryAI)</c> logic that was
    /// duplicated across services.</para>
    /// </summary>
    public interface IPersonaContext
    {
        /// <summary>Loads persisted selections and runs one-time migration from the legacy <c>IsPrimaryAI</c> flag.</summary>
        Task InitializeAsync();

        /// <summary>The always-active persona id (empty if none selected).</summary>
        string? PrimaryId { get; }

        /// <summary>The persona id last activated in chat (empty if none).</summary>
        string? SecondaryId { get; }

        /// <summary>Resolves the always-active primary persona (falls back to first available).</summary>
        Task<AIContact?> GetPrimaryAsync();

        /// <summary>Resolves the secondary persona (last activated in chat), or null if none.</summary>
        Task<AIContact?> GetSecondaryAsync();

        /// <summary>Resolves the "current focus": secondary if set, otherwise primary, otherwise first.</summary>
        Task<AIContact?> GetActiveAsync();

        /// <summary>
        /// Resolves a contact for a background feature: the explicit <paramref name="preferredId"/> if it
        /// exists, otherwise the primary, otherwise the first available contact.
        /// </summary>
        Task<AIContact?> ResolveAsync(string? preferredId);

        /// <summary>Promotes a persona to primary (atomically demotes any prior primary) and persists the choice.</summary>
        Task SetPrimaryAsync(string contactId);

        /// <summary>Records the persona last activated in chat and persists the choice.</summary>
        Task SetSecondaryAsync(string contactId);

        event EventHandler<PersonaChangedEvent>? PrimaryChanged;
        event EventHandler<PersonaChangedEvent>? SecondaryChanged;
    }
}
