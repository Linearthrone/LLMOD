using HouseVictoria.Core.Events;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Default <see cref="IPersonaContext"/> implementation.
    /// <para>Holds the canonical primary/secondary persona selections, persists them to the
    /// key/value store, mirrors them onto <see cref="AppConfig"/> (so existing services that read
    /// the config singleton stay consistent), and keeps the legacy <c>IsPrimaryAI</c> flag in sync.</para>
    /// </summary>
    public sealed class PersonaContextService : IPersonaContext
    {
        private const string StateKey = "PersonaContextState";

        private readonly IPersistenceService _persistence;
        private readonly AppConfig _config;
        private readonly IEventAggregator? _events;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private string _primaryId = string.Empty;
        private string _secondaryId = string.Empty;
        private bool _initialized;

        public PersonaContextService(IPersistenceService persistence, AppConfig config, IEventAggregator? events = null)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events;
        }

        public string? PrimaryId => string.IsNullOrEmpty(_primaryId) ? null : _primaryId;
        public string? SecondaryId => string.IsNullOrEmpty(_secondaryId) ? null : _secondaryId;

        public event EventHandler<PersonaChangedEvent>? PrimaryChanged;
        public event EventHandler<PersonaChangedEvent>? SecondaryChanged;

        public async Task InitializeAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_initialized)
                    return;

                // 1) Prefer the persisted DB state, then any value already on AppConfig (app.config).
                PersonaContextState? state = null;
                try { state = await _persistence.GetAsync<PersonaContextState>(StateKey).ConfigureAwait(false); }
                catch { /* fall through to migration */ }

                _primaryId = state?.PrimaryId ?? string.Empty;
                _secondaryId = state?.SecondaryId ?? string.Empty;

                if (string.IsNullOrEmpty(_primaryId) && !string.IsNullOrEmpty(_config.PrimaryAiContactId))
                    _primaryId = _config.PrimaryAiContactId;
                if (string.IsNullOrEmpty(_secondaryId) && !string.IsNullOrEmpty(_config.SecondaryAiContactId))
                    _secondaryId = _config.SecondaryAiContactId;

                var contacts = await SafeGetAllAsync().ConfigureAwait(false);

                // 2) Migrate from the legacy IsPrimaryAI flag if we still have no primary.
                if (string.IsNullOrEmpty(_primaryId) || FindById(contacts, _primaryId) == null)
                {
                    var legacy = contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
                    _primaryId = legacy?.Id ?? string.Empty;
                }

                _config.PrimaryAiContactId = _primaryId;
                _config.SecondaryAiContactId = _secondaryId;

                // 3) Reconcile the legacy flag with the resolved primary so old call sites agree.
                if (!string.IsNullOrEmpty(_primaryId))
                    await SyncPrimaryFlagAsync(contacts, _primaryId).ConfigureAwait(false);

                await PersistStateAsync().ConfigureAwait(false);
                _initialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<AIContact?> GetPrimaryAsync()
        {
            var contacts = await SafeGetAllAsync().ConfigureAwait(false);
            if (contacts.Count == 0)
                return null;

            return FindById(contacts, _primaryId)
                ?? contacts.Values.FirstOrDefault(c => c.IsPrimaryAI)
                ?? contacts.Values.FirstOrDefault();
        }

        public async Task<AIContact?> GetSecondaryAsync()
        {
            if (string.IsNullOrEmpty(_secondaryId))
                return null;
            var contacts = await SafeGetAllAsync().ConfigureAwait(false);
            return FindById(contacts, _secondaryId);
        }

        public async Task<AIContact?> GetActiveAsync()
        {
            var contacts = await SafeGetAllAsync().ConfigureAwait(false);
            if (contacts.Count == 0)
                return null;

            return FindById(contacts, _secondaryId)
                ?? FindById(contacts, _primaryId)
                ?? contacts.Values.FirstOrDefault(c => c.IsPrimaryAI)
                ?? contacts.Values.FirstOrDefault();
        }

        public async Task<AIContact?> ResolveAsync(string? preferredId)
        {
            var contacts = await SafeGetAllAsync().ConfigureAwait(false);
            if (contacts.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredId))
            {
                var preferred = FindById(contacts, preferredId);
                if (preferred != null)
                    return preferred;
            }

            return FindById(contacts, _primaryId)
                ?? contacts.Values.FirstOrDefault(c => c.IsPrimaryAI)
                ?? contacts.Values.FirstOrDefault();
        }

        public async Task SetPrimaryAsync(string contactId)
        {
            if (string.IsNullOrWhiteSpace(contactId))
                return;

            await _lock.WaitAsync().ConfigureAwait(false);
            string previous;
            try
            {
                previous = _primaryId;
                if (string.Equals(previous, contactId, StringComparison.Ordinal))
                    return;

                _primaryId = contactId;
                _config.PrimaryAiContactId = contactId;

                var contacts = await SafeGetAllAsync().ConfigureAwait(false);
                await SyncPrimaryFlagAsync(contacts, contactId).ConfigureAwait(false);
                await PersistStateAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            RaiseChanged(PersonaSelectionKind.Primary, contactId, previous, PrimaryChanged);
        }

        public async Task SetSecondaryAsync(string contactId)
        {
            if (string.IsNullOrWhiteSpace(contactId))
                return;

            await _lock.WaitAsync().ConfigureAwait(false);
            string previous;
            try
            {
                previous = _secondaryId;
                if (string.Equals(previous, contactId, StringComparison.Ordinal))
                    return;

                _secondaryId = contactId;
                _config.SecondaryAiContactId = contactId;
                await PersistStateAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }

            RaiseChanged(PersonaSelectionKind.Secondary, contactId, previous, SecondaryChanged);
        }

        private async Task SyncPrimaryFlagAsync(Dictionary<string, AIContact> contacts, string primaryId)
        {
            foreach (var kvp in contacts)
            {
                var contact = kvp.Value;
                var shouldBePrimary = string.Equals(contact.Id, primaryId, StringComparison.Ordinal);
                var roleNeedsUpdate = shouldBePrimary
                    ? contact.Role != PersonaRole.Primary
                    : contact.Role == PersonaRole.Primary;

                if (contact.IsPrimaryAI == shouldBePrimary && !roleNeedsUpdate)
                    continue;

                contact.IsPrimaryAI = shouldBePrimary;
                if (shouldBePrimary)
                    contact.Role = PersonaRole.Primary;
                else if (contact.Role == PersonaRole.Primary)
                    contact.Role = PersonaRole.None;

                try { await _persistence.SetAsync(kvp.Key, contact).ConfigureAwait(false); }
                catch { /* best effort flag sync */ }
            }
        }

        private async Task PersistStateAsync()
        {
            try
            {
                await _persistence.SetAsync(StateKey, new PersonaContextState
                {
                    PrimaryId = _primaryId,
                    SecondaryId = _secondaryId
                }).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }

        private async Task<Dictionary<string, AIContact>> SafeGetAllAsync()
        {
            try { return await _persistence.GetAllAsync<AIContact>().ConfigureAwait(false); }
            catch { return new Dictionary<string, AIContact>(); }
        }

        private static AIContact? FindById(Dictionary<string, AIContact> contacts, string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return contacts.Values.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));
        }

        private void RaiseChanged(PersonaSelectionKind kind, string contactId, string? previous, EventHandler<PersonaChangedEvent>? handler)
        {
            var evt = new PersonaChangedEvent
            {
                Kind = kind,
                ContactId = contactId,
                PreviousContactId = previous
            };
            handler?.Invoke(this, evt);
            try { _events?.Publish(evt); } catch { /* non-fatal */ }
        }
    }
}
