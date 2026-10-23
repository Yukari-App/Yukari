using System.Collections.Generic;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Yukari.Models.Rich;
using Yukari.Services.UI;

namespace Yukari.Helpers.UI;

// Provides an attached property that binds a list of RichBlock instances,
// a localization resource key, or a markdown string to a WinUI 3 RichTextBlock,
// rendering them as native XAML text elements.
public static class RichTextHelper
{
    #region Attached Property
    // Typed as object on purpose: a DP typed as IReadOnlyList<ManagedRecord> is not a
    // valid WinRT type and tends to blow up in the XAML compiler / x:Bind.
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source",
        typeof(object),
        typeof(RichTextHelper),
        new PropertyMetadata(null, OnSourceChanged)
    );

    public static object? GetSource(DependencyObject o) => o.GetValue(SourceProperty);

    public static void SetSource(DependencyObject o, object? v) => o.SetValue(SourceProperty, v);

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBlock target)
            return;

        if (e.NewValue is IReadOnlyList<RichBlock> blocks)
        {
            Render(target, blocks);
            return;
        }

        if (e.NewValue is string text)
        {
            string? localized = null;
            try
            {
                localized = App.GetService<ILocalizationService>()?.GetString(text);
            }
            catch
            {
                // Fallback to text directly if text is not a valid resource key
            }

            var markdown = string.IsNullOrEmpty(localized) ? text : localized;
            Render(target, MarkdownParser.Parse(markdown));
            return;
        }

        Render(target, null);
    }
    #endregion

    #region Rendering
    private static void Render(RichTextBlock target, IReadOnlyList<RichBlock>? blocks)
    {
        target.Blocks.Clear();
        if (blocks is null)
            return;

        foreach (var block in blocks)
        {
            var p = new Paragraph { Margin = new Thickness(block.Indent * 16, 0, 0, 6) };

            switch (block.Kind)
            {
                case RichBlockKind.Heading:
                    p.FontWeight = FontWeights.SemiBold;
                    p.FontSize = block.Level <= 2 ? 18 : 16;
                    break;
                case RichBlockKind.Quote:
                    p.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    p.Margin = new Thickness(block.Indent * 16 + 12, 0, 0, 6);
                    break;
                case RichBlockKind.Rule:
                    p.Inlines.Add(new Run { Text = "────────────" });
                    target.Blocks.Add(p);
                    continue;
            }

            if (block.Marker is not null)
                p.Inlines.Add(new Run { Text = block.Marker + "\u00A0" });

            AddInlines(p.Inlines, block.Inlines);
            target.Blocks.Add(p);
        }
    }

    private static void AddInlines(InlineCollection target, IEnumerable<RichInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case RichText t:
                    target.Add(new Run { Text = t.Text });
                    break;
                case RichBreak:
                    target.Add(new LineBreak());
                    break;
                case RichStyled s:
                    var span = new Span();
                    if (s.Style.HasFlag(InlineStyle.Bold))
                        span.FontWeight = FontWeights.Bold;
                    if (s.Style.HasFlag(InlineStyle.Italic))
                        span.FontStyle = Windows.UI.Text.FontStyle.Italic;

                    var deco = Windows.UI.Text.TextDecorations.None;
                    if (s.Style.HasFlag(InlineStyle.Underline))
                        deco |= Windows.UI.Text.TextDecorations.Underline;
                    if (s.Style.HasFlag(InlineStyle.Strikethrough))
                        deco |= Windows.UI.Text.TextDecorations.Strikethrough;
                    span.TextDecorations = deco;

                    AddInlines(span.Inlines, s.Children);
                    target.Add(span);
                    break;
                case RichLink l:
                    var link = new Hyperlink { NavigateUri = l.Url };
                    AddInlines(link.Inlines, l.Children);
                    target.Add(link);
                    break;
            }
        }
    }
    #endregion
}
