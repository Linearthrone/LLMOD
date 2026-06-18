using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Persona
{
    /// <summary>
    /// Ensures every LLM call carries an explicit persona identity in the system prompt.
    /// </summary>
    public static class PersonaPromptComposer
    {
        public static AIContact WithIdentity(AIContact contact, bool operationalMode = false)
        {
            var merged = OperationalStyleComposer.MergeSystemPrompt(contact, operationalMode);

            return new AIContact
            {
                Id = contact.Id,
                Name = contact.Name,
                ModelName = contact.ModelName,
                SystemPrompt = merged,
                Description = contact.Description,
                AvatarUrl = contact.AvatarUrl,
                PersonalityTraits = contact.PersonalityTraits,
                ServerEndpoint = contact.ServerEndpoint,
                MCPServerEndpoint = contact.MCPServerEndpoint,
                AdditionalServers = contact.AdditionalServers,
                IsLoaded = contact.IsLoaded,
                CreatedAt = contact.CreatedAt,
                LastUsedAt = contact.LastUsedAt,
                IsPrimaryAI = contact.IsPrimaryAI,
                Role = contact.Role,
                DataPath = contact.DataPath,
                PiperVoiceId = contact.PiperVoiceId,
                AvatarModelPath = contact.AvatarModelPath,
                AvatarVoiceSpeed = contact.AvatarVoiceSpeed,
                AvatarVoicePitch = contact.AvatarVoicePitch,
                Temperature = contact.Temperature,
                TopP = contact.TopP,
                TopK = contact.TopK,
                RepeatPenalty = contact.RepeatPenalty,
                MaxTokens = contact.MaxTokens,
                ContextLength = contact.ContextLength,
                KnowledgeSharing = contact.KnowledgeSharing?.Clone() ?? new PersonaKnowledgeSharing()
            };
        }

        public static bool IsPrimaryPersona(AIContact contact) =>
            contact.IsPrimaryAI || contact.Role == PersonaRole.Primary;
    }
}
