using Microsoft.UI.Xaml.Controls;
using Yukari.Helpers.UI;
using Yukari.Services.UI;

namespace Yukari.Views.Pages;

public sealed partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();

        var lclService = App.GetService<ILocalizationService>();
        InlineMarkdownWeaver.ApplyTo(ComicSourcesHelp1, lclService.GetString("ComicSourcesHelp1"));
        InlineMarkdownWeaver.ApplyTo(ComicSourcesHelp2, lclService.GetString("ComicSourcesHelp2"));
        InlineMarkdownWeaver.ApplyTo(ComicSourcesHelp3, lclService.GetString("ComicSourcesHelp3"));
        InlineMarkdownWeaver.ApplyTo(ComicSourcesHelp4, lclService.GetString("ComicSourcesHelp4"));

        InlineMarkdownWeaver.ApplyTo(OnlineComicsHelp1, lclService.GetString("OnlineComicsHelp1"));
        InlineMarkdownWeaver.ApplyTo(OnlineComicsHelp2, lclService.GetString("OnlineComicsHelp2"));
        InlineMarkdownWeaver.ApplyTo(OnlineComicsHelp3, lclService.GetString("OnlineComicsHelp3"));

        InlineMarkdownWeaver.ApplyTo(LocalComicsHelp1, lclService.GetString("LocalComicsHelp1"));
        InlineMarkdownWeaver.ApplyTo(LocalComicsHelp2, lclService.GetString("LocalComicsHelp2"));
        InlineMarkdownWeaver.ApplyTo(LocalComicsHelp3, lclService.GetString("LocalComicsHelp3"));

        InlineMarkdownWeaver.ApplyTo(ReaderHelp1, lclService.GetString("ReaderHelp1"));
        InlineMarkdownWeaver.ApplyTo(ReaderHelp2, lclService.GetString("ReaderHelp2"));
        InlineMarkdownWeaver.ApplyTo(ReaderHelp3, lclService.GetString("ReaderHelp3"));

        InlineMarkdownWeaver.ApplyTo(DownloadsHelp1, lclService.GetString("DownloadsHelp1"));
        InlineMarkdownWeaver.ApplyTo(DownloadsHelp2, lclService.GetString("DownloadsHelp2"));
        InlineMarkdownWeaver.ApplyTo(DownloadsHelp3, lclService.GetString("DownloadsHelp3"));

        InlineMarkdownWeaver.ApplyTo(SupportHelp1, lclService.GetString("SupportHelp1"));
    }
}
