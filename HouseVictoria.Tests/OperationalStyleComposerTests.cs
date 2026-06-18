using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persona;
using Xunit;

namespace HouseVictoria.Tests;

public class OperationalStyleComposerTests
{
    [Fact]
    public void ActionIntegrityInstructions_preserves_personality_guidance()
    {
        var text = OperationalStyleComposer.ActionIntegrityInstructions("Victoria");
        Assert.Contains("Stay fully in character", text);
        Assert.Contains("NEVER claim you sent", text);
        Assert.DoesNotContain("No roleplay", text);
    }

    [Fact]
    public void MergeSystemPrompt_keeps_persona_and_adds_action_integrity()
    {
        var contact = new AIContact
        {
            Name = "Victoria",
            SystemPrompt = "You are devoted, intense, and speak with warmth."
        };

        var merged = OperationalStyleComposer.MergeSystemPrompt(contact, actionIntegrityMode: true);
        Assert.Contains("Stay in character as Victoria", merged);
        Assert.Contains("devoted, intense", merged);
        Assert.Contains("ACTION INTEGRITY", merged);
    }

    [Fact]
    public void MergeSystemPrompt_without_action_integrity_is_unchanged_style()
    {
        var contact = new AIContact
        {
            Name = "Victoria",
            SystemPrompt = "Personality block."
        };

        var merged = OperationalStyleComposer.MergeSystemPrompt(contact, actionIntegrityMode: false);
        Assert.Contains("Stay in character as Victoria", merged);
        Assert.DoesNotContain("ACTION INTEGRITY", merged);
    }
}
