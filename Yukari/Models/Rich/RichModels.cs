using System;
using System.Collections.Generic;

namespace Yukari.Models.Rich;

// Intermediate rich-text AST models used to decouple description parsing (Markdown/HTML)
// from UI presentation (WinUI 3 RichTextBlock).

[Flags]
public enum InlineStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikethrough = 8,
}

public enum RichBlockKind
{
    Paragraph,
    Heading,
    Quote,
    Rule,
}

public abstract record RichInline;

public sealed record RichText(string Text) : RichInline;

public sealed record RichBreak : RichInline;

public sealed record RichStyled(InlineStyle Style, IReadOnlyList<RichInline> Children) : RichInline;

public sealed record RichLink(Uri Url, IReadOnlyList<RichInline> Children) : RichInline;

public sealed record RichBlock(
    RichBlockKind Kind,
    IReadOnlyList<RichInline> Inlines,
    int Indent = 0,
    int Level = 0,
    string? Marker = null
);
