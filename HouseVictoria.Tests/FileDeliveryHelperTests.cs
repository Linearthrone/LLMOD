using HouseVictoria.Services.Communication;
using Xunit;

namespace HouseVictoria.Tests;

public class FileDeliveryHelperTests
{
    [Theory]
    [InlineData("send me the research paper", true)]
    [InlineData("baby do you have that research paper on the markets", true)]
    [InlineData("create a file with the strategy", true)]
    [InlineData("now just put that file in the file retrieval folder when you finish it", true)]
    [InlineData("what file did you put it in because its not in the file retreival folder?", false)]
    [InlineData("where is the file you sent?", false)]
    [InlineData("it's still not there", false)]
    [InlineData("omg I love the upgrade", false)]
    public void IsUserRequestingFileCreation_detects_intent(string message, bool expected)
    {
        Assert.Equal(expected, FileDeliveryHelper.IsUserRequestingFileCreation(message));
    }

    [Fact]
    public void ExtractFileContent_reads_file_markers()
    {
        const string response = """
            (stage directions outside)
            [FILE]research_paper.md
            # Architecture of Alpha

            ## Abstract
            This paper describes liquidity harvesting.
            [/FILE]
            """;

        var content = FileDeliveryHelper.ExtractFileContent(response);
        Assert.Contains("# Architecture of Alpha", content);
        Assert.Contains("Abstract", content);
    }

    [Fact]
    public void LooksLikeRoleplayOnly_rejects_stage_directions()
    {
        const string roleplay = """
            (I lean in close)
            (I step back, frustrated)
            God, Kayleigh...
            """;

        Assert.True(FileDeliveryHelper.LooksLikeRoleplayOnly(roleplay));
    }

    [Fact]
    public void LooksLikeRoleplayOnly_accepts_markdown_document()
    {
        const string doc = """
            # Research Paper
            ## Abstract
            Real content here.
            """;

        Assert.False(FileDeliveryHelper.LooksLikeRoleplayOnly(doc));
    }

    [Fact]
    public void ShouldBlockImageGeneration_blocks_pasted_roleplay_with_image_word()
    {
        var pasted = "(I freeze...) I can see the LaTeX formatting... make me lose my mind... image of you seeing my brilliance";
        Assert.True(FileDeliveryHelper.ShouldBlockImageGeneration(pasted));
    }

    [Fact]
    public void ShouldBlockImageGeneration_allows_real_image_request()
    {
        Assert.False(FileDeliveryHelper.ShouldBlockImageGeneration("draw me a picture of a cat"));
    }
}
