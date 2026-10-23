using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Yukari.Models.Rich;

namespace Yukari.Helpers;

public static partial class DescriptionParser
{
    // Plain AutoLinks only: no advanced extensions (tables etc.), so plain-text
    // descriptions are mangled as little as possible.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .Build();

    public static IReadOnlyList<RichBlock> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var document = Markdown.Parse(NormalizeHtmlBlocks(raw), Pipeline);
        var blocks = new List<RichBlock>();
        foreach (var block in document)
            AddBlock(block, blocks, indent: 0, quote: false);
        return blocks;
    }

    #region Blocks

    private static void AddBlock(
        Block block,
        List<RichBlock> output,
        int indent,
        bool quote,
        string? marker = null
    )
    {
        switch (block)
        {
            case HeadingBlock h:
                output.Add(new(RichBlockKind.Heading, ParseInlines(h.Inline), indent, h.Level));
                break;

            case ParagraphBlock p:
                var inlines = ParseInlines(p.Inline);
                if (inlines.Count > 0)
                    output.Add(
                        new(
                            quote ? RichBlockKind.Quote : RichBlockKind.Paragraph,
                            inlines,
                            indent,
                            Marker: marker
                        )
                    );
                break;

            case ListBlock list:
                var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    string? itemMarker = list.IsOrdered ? $"{number++}." : "•";
                    foreach (var child in item)
                    {
                        AddBlock(child, output, indent + 1, quote, itemMarker);
                        itemMarker = null; // Only the first block of an item receives the marker.
                    }
                }
                break;

            case QuoteBlock q:
                foreach (var child in q)
                    AddBlock(child, output, indent, quote: true);
                break;

            case ThematicBreakBlock:
                output.Add(new(RichBlockKind.Rule, []));
                break;

            case LeafBlock leaf: // Code blocks and leftover HTML blocks: degrade to plain text.
                var text = WebUtility.HtmlDecode(TagRegex().Replace(leaf.Lines.ToString(), "")).Trim();
                if (text.Length > 0)
                    output.Add(new(RichBlockKind.Paragraph, [new RichText(text)], indent));
                break;
        }
    }

    #endregion

    #region Inlines

    private sealed class Frame(string tag, InlineStyle style, Uri? url)
    {
        public string Tag { get; } = tag; // Empty string indicates opened by Markdown; otherwise opened by an HTML tag.
        public InlineStyle Style { get; } = style;
        public Uri? Url { get; } = url;
        public List<RichInline> Children { get; } = [];
    }

    private static List<RichInline> ParseInlines(ContainerInline? container)
    {
        var stack = new Stack<Frame>();
        stack.Push(new Frame("", InlineStyle.None, null));

        if (container is not null)
            Walk(container, stack);

        while (stack.Count > 1) // Closes any unclosed HTML tags left on the stack.
            Close(stack);

        return stack.Peek().Children;
    }

    private static void Walk(ContainerInline container, Stack<Frame> stack)
    {
        foreach (var inline in container)
        {
            var children = stack.Peek().Children;
            switch (inline)
            {
                case LiteralInline lit:
                    children.Add(new RichText(lit.Content.ToString()));
                    break;
                case HtmlEntityInline entity:
                    children.Add(new RichText(entity.Transcoded.ToString()));
                    break;
                case CodeInline code:
                    children.Add(new RichText(code.Content));
                    break;
                case LineBreakInline:
                    children.Add(new RichBreak());
                    break;
                case HtmlInline html:
                    HandleTag(html.Tag, stack);
                    break;
                case AutolinkInline auto:
                    var autoUri = SafeUri(auto.Url);
                    children.Add(
                        autoUri is null
                            ? new RichText(auto.Url)
                            : new RichLink(autoUri, [new RichText(auto.Url)])
                    );
                    break;
                case LinkInline { IsImage: true }:
                    break; // Images are intentionally dropped for privacy and performance.
                case LinkInline link:
                    var url = InsideLink(stack) ? null : SafeUri(link.Url);
                    WalkNested(link, stack, new Frame("", InlineStyle.None, url));
                    break;
                case EmphasisInline em:
                    var style = em.DelimiterCount >= 2 ? InlineStyle.Bold : InlineStyle.Italic;
                    WalkNested(em, stack, new Frame("", style, null));
                    break;
                case ContainerInline nested:
                    Walk(nested, stack);
                    break;
            }
        }
    }

    private static void WalkNested(ContainerInline container, Stack<Frame> stack, Frame frame)
    {
        var depth = stack.Count;
        stack.Push(frame);
        Walk(container, stack);
        while (stack.Count > depth) // Closes HTML tags left open inside this Markdown node.
            Close(stack);
    }

    private static void Close(Stack<Frame> stack)
    {
        var frame = stack.Pop();
        var parent = stack.Peek().Children;

        if (frame.Url is not null)
            parent.Add(new RichLink(frame.Url, frame.Children));
        else if (frame.Style != InlineStyle.None)
            parent.Add(new RichStyled(frame.Style, frame.Children));
        else
            parent.AddRange(frame.Children);
    }

    private static void HandleTag(string tag, Stack<Frame> stack)
    {
        var m = TagRegex().Match(tag);
        if (!m.Success)
            return;

        var closing = m.Groups[1].Success;
        var name = m.Groups[2].Value.ToLowerInvariant();

        if (closing)
        {
            if (!stack.Any(f => f.Tag == name))
                return;
            Frame top;
            do
            {
                top = stack.Peek();
                Close(stack);
            } while (top.Tag != name);
            return;
        }

        switch (name)
        {
            case "br":
                stack.Peek().Children.Add(new RichBreak());
                break;
            case "b" or "strong":
                stack.Push(new Frame(name, InlineStyle.Bold, null));
                break;
            case "i" or "em":
                stack.Push(new Frame(name, InlineStyle.Italic, null));
                break;
            case "u":
                stack.Push(new Frame(name, InlineStyle.Underline, null));
                break;
            case "s" or "del" or "strike":
                stack.Push(new Frame(name, InlineStyle.Strikethrough, null));
                break;
            case "a":
                var href = HrefRegex().Match(m.Groups[3].Value).Groups[1].Value;
                stack.Push(
                    new Frame(name, InlineStyle.None, InsideLink(stack) ? null : SafeUri(href))
                );
                break;
            // Any other tag is dropped while preserving its inner text content.
        }
    }

    private static bool InsideLink(Stack<Frame> stack) => stack.Any(f => f.Url is not null);

    private static Uri? SafeUri(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
            ? uri
            : null;

    #endregion

    #region HTML Normalization

    // Markdig doesn't parse markdown inside HTML blocks (<p>, <ul>...), so block-level
    // tags are converted to their markdown equivalents before parsing. Inline tags
    // (<b>, <a>, <br> mid-paragraph) are handled by the walker above.
    private static string NormalizeHtmlBlocks(string raw)
    {
        if (!BlockTagRegex().IsMatch(raw))
            return raw;

        var s = raw.Replace("\r\n", "\n");
        s = BrRegex().Replace(s, "\n");
        s = HrRegex().Replace(s, "\n\n---\n\n");
        s = HeadingTagRegex()
            .Replace(
                s,
                m =>
                    m.Groups[1].Success
                        ? "\n\n"
                        : $"\n\n{new string('#', m.Groups[2].Value[0] - '0')} "
            );
        s = ListItemOpenRegex().Replace(s, "\n- ");
        s = ListItemCloseRegex().Replace(s, "\n");
        s = ContainerTagRegex().Replace(s, "\n\n");
        return ManyNewlinesRegex().Replace(s, "\n\n");
    }

    #endregion

    #region Regexes

    [GeneratedRegex(
        @"<\s*/?\s*(?:p|div|ul|ol|li|br|hr|h[1-6]|blockquote)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex BlockTagRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();

    [GeneratedRegex(@"<hr\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HrRegex();

    [GeneratedRegex(@"<(/)?h([1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingTagRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemOpenRegex();

    [GeneratedRegex(@"</li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemCloseRegex();

    [GeneratedRegex(@"</?(?:p|div|ul|ol|blockquote)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ContainerTagRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlinesRegex();

    [GeneratedRegex(@"^<\s*(/)?\s*([a-zA-Z][a-zA-Z0-9]*)([^>]*)>$", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    #endregion
}
