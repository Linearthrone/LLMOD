using HouseVictoria.Services.Memory;
using Xunit;

namespace HouseVictoria.Tests;

public class EmbeddingHelperTests
{
    [Fact]
    public void CreatePseudoEmbedding_IsDeterministic()
    {
        var a = EmbeddingHelper.CreatePseudoEmbedding("hello", 64);
        var b = EmbeddingHelper.CreatePseudoEmbedding("hello", 64);
        Assert.Equal(64, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void CreatePseudoEmbedding_DifferentText_DifferentVector()
    {
        var a = EmbeddingHelper.CreatePseudoEmbedding("a", 32);
        var b = EmbeddingHelper.CreatePseudoEmbedding("b", 32);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task CreateEmbeddingAsync_WithoutConfig_UsesPseudoDimensions()
    {
        var v = await EmbeddingHelper.CreateEmbeddingAsync("x", null, 16);
        Assert.Equal(16, v.Length);
    }
}
