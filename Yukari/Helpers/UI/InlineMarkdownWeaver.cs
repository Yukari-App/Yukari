using System;
using System.Text.RegularExpressions;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Yukari.Helpers.UI;

// Interprets a tiny Markdown subset (**bold** and [text](url)) inside localized
// strings, so translators can reorder/reformat sentences freely without
// depending on positional placeholders or per-Run x:Uid entries.
public static class InlineMarkdownWeaver
{
    private static readonly Regex LinkRegex = new(
        @"\[([^\]]+)\]\(([^)]+)\)",
        RegexOptions.Compiled
    );
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    public static void ApplyTo(RichTextBlock target, string markdown)
    {
        var paragraph = new Paragraph();
        ParseSegment(paragraph.Inlines, markdown);
        target.Blocks.Clear();
        target.Blocks.Add(paragraph);
    }

    private static void ParseSegment(InlineCollection inlines, string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            var linkMatch = LinkRegex.Match(text, pos);
            var boldMatch = BoldRegex.Match(text, pos);

            Match? next = (!linkMatch.Success, !boldMatch.Success) switch
            {
                (true, true) => null,
                (true, false) => boldMatch,
                (false, true) => linkMatch,
                (false, false) => linkMatch.Index <= boldMatch.Index ? linkMatch : boldMatch,
            };

            if (next == null)
            {
                inlines.Add(new Run { Text = text[pos..] });
                break;
            }

            if (next.Index > pos)
                inlines.Add(new Run { Text = text[pos..next.Index] });

            if (next == linkMatch)
            {
                var hyperlink = new Hyperlink { NavigateUri = new Uri(linkMatch.Groups[2].Value) };
                hyperlink.Inlines.Add(new Run { Text = linkMatch.Groups[1].Value });
                inlines.Add(hyperlink);
            }
            else
            {
                inlines.Add(
                    new Run { Text = boldMatch!.Groups[1].Value, FontWeight = FontWeights.Bold }
                );
            }

            pos = next.Index + next.Length;
        }
    }
}
