using System.Linq;
using FluentAssertions;
using Yukari.Helpers;
using Yukari.Models.Rich;

namespace Yukari.Tests.Helpers;

public class MarkdownParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    public void Parse_ReturnsEmpty_WhenNothingRenderable(string? raw)
    {
        // Act
        var result = MarkdownParser.Parse(raw);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Markdown_BoldAndLink()
    {
        // Arrange
        var raw = "**bold** and [site](https://example.com)";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().ContainSingle();
        blocks[0]
            .Inlines[0]
            .Should()
            .BeOfType<RichStyled>()
            .Which.Style.Should()
            .Be(InlineStyle.Bold);
        blocks[0].Inlines.OfType<RichLink>().Single().Url.Host.Should().Be("example.com");
    }

    [Fact]
    public void Parse_Html_ParagraphsBreaksAndInlineTags()
    {
        // Arrange
        var raw = "<p>One</p><p>Two <b>bold</b><br>line</p>";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().HaveCount(2);
        blocks[1].Inlines.Should().Contain(i => i is RichStyled).And.Contain(i => i is RichBreak);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("<a href=\"file:///C:/secret\">x</a>")]
    public void Parse_DropsUnsafeLinkSchemes_KeepsText(string raw)
    {
        // Act
        var inlines = MarkdownParser.Parse(raw)[0].Inlines;

        // Assert
        inlines.Should().NotContain(i => i is RichLink);
        inlines.Should().Contain(i => i is RichText);
    }

    [Fact]
    public void Parse_PlainText_PreservesLineBreaks()
    {
        // Arrange
        var raw = "Line1\nLine2";

        // Act
        var inlines = MarkdownParser.Parse(raw)[0].Inlines;

        // Assert
        inlines.Should().HaveCount(3);
        inlines[1].Should().BeOfType<RichBreak>();
    }

    [Fact]
    public void Parse_Markdown_Headings()
    {
        // Arrange
        var raw = "# Heading 1\n\n## Heading 2";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().HaveCount(2);
        blocks[0].Kind.Should().Be(RichBlockKind.Heading);
        blocks[0].Level.Should().Be(1);
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("Heading 1");

        blocks[1].Kind.Should().Be(RichBlockKind.Heading);
        blocks[1].Level.Should().Be(2);
        blocks[1].Inlines.OfType<RichText>().Single().Text.Should().Be("Heading 2");
    }

    [Fact]
    public void Parse_Html_Headings()
    {
        // Arrange
        var raw = "<h3>Heading 3</h3>";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(RichBlockKind.Heading);
        blocks[0].Level.Should().Be(3);
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("Heading 3");
    }

    [Fact]
    public void Parse_Markdown_UnorderedList()
    {
        // Arrange
        var raw = "- Item A\n- Item B";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().HaveCount(2);
        blocks[0].Indent.Should().Be(1);
        blocks[0].Marker.Should().Be("•");
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("Item A");

        blocks[1].Indent.Should().Be(1);
        blocks[1].Marker.Should().Be("•");
        blocks[1].Inlines.OfType<RichText>().Single().Text.Should().Be("Item B");
    }

    [Fact]
    public void Parse_Html_UnorderedList()
    {
        // Arrange
        var raw = "<ul><li>a</li><li>b</li></ul>";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().HaveCount(2);
        blocks[0].Indent.Should().Be(1);
        blocks[0].Marker.Should().Be("•");
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("a");

        blocks[1].Indent.Should().Be(1);
        blocks[1].Marker.Should().Be("•");
        blocks[1].Inlines.OfType<RichText>().Single().Text.Should().Be("b");
    }

    [Fact]
    public void Parse_Markdown_OrderedList()
    {
        // Arrange
        var raw = "1. First\n2. Second";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().HaveCount(2);
        blocks[0].Indent.Should().Be(1);
        blocks[0].Marker.Should().Be("1.");
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("First");

        blocks[1].Indent.Should().Be(1);
        blocks[1].Marker.Should().Be("2.");
        blocks[1].Inlines.OfType<RichText>().Single().Text.Should().Be("Second");
    }

    [Fact]
    public void Parse_Markdown_Blockquote()
    {
        // Arrange
        var raw = "> Quoted message";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(RichBlockKind.Quote);
        blocks[0].Inlines.OfType<RichText>().Single().Text.Should().Be("Quoted message");
    }

    [Theory]
    [InlineData("---")]
    [InlineData("<hr>")]
    public void Parse_ThematicBreak_CreatesRuleBlock(string raw)
    {
        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(RichBlockKind.Rule);
    }

    [Fact]
    public void Parse_InlineStyles_ItalicUnderlineAndStrikethrough()
    {
        // Arrange
        var raw = "*italic* <u>underline</u> <s>strike</s> <del>del</del>";

        // Act
        var inlines = MarkdownParser.Parse(raw)[0].Inlines;

        // Assert
        var styles = inlines.OfType<RichStyled>().Select(s => s.Style).ToList();
        styles.Should().Contain(InlineStyle.Italic);
        styles.Should().Contain(InlineStyle.Underline);
        styles.Should().Contain(InlineStyle.Strikethrough);
    }

    [Theory]
    [InlineData("<b>text")]
    [InlineData("<i>text")]
    [InlineData("<b><i>text")]
    public void Parse_UnclosedHtmlTags_RecoversGracefullyWithoutThrowing(string raw)
    {
        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().ContainSingle();
        blocks[0].Inlines.Should().Contain(i => i is RichStyled);
    }

    [Fact]
    public void Parse_Markdown_UnderscoreEmphasis_ProducesItalicAndBold()
    {
        // Arrange
        var raw = "_italic text_ and __bold text__";

        // Act
        var inlines = MarkdownParser.Parse(raw)[0].Inlines;

        // Assert
        var styles = inlines.OfType<RichStyled>().Select(s => s.Style).ToList();
        styles.Should().Contain(InlineStyle.Italic);
        styles.Should().Contain(InlineStyle.Bold);
    }

    [Fact]
    public void Parse_Markdown_IntraWordUnderscore_PreservedAsPlainText()
    {
        // Arrange
        var raw = "chapter_01_final and local_comic_title";

        // Act
        var inlines = MarkdownParser.Parse(raw)[0].Inlines;

        // Assert
        inlines.Should().NotContain(i => i is RichStyled);
        inlines
            .OfType<RichText>()
            .Single()
            .Text.Should()
            .Be("chapter_01_final and local_comic_title");
    }

    [Fact]
    public void Parse_MarkdownImages_DroppedForPrivacy()
    {
        // Arrange
        var raw = "![Cover Image](https://example.com/cover.jpg)";

        // Act
        var blocks = MarkdownParser.Parse(raw);

        // Assert
        blocks.Should().BeEmpty();
    }
}
