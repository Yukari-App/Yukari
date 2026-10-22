using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Yukari.Helpers;

public static partial class EpubHelper
{
    private static readonly XmlReaderSettings SafeXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
    };

    private static readonly string[] WrapperExtensions =
    [
        ".xhtml",
        ".html",
        ".htm",
        ".xml",
        ".svg",
    ];

    public static (string? Title, IReadOnlyList<string> PageEntries) ExtractContent(
        ZipArchive archive
    )
    {
        var entryLookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var normalized = NormalizeZipPath(entry.FullName);
            entryLookup.TryAdd(normalized, entry);
        }

        var opfEntry = FindOpfEntry(archive, entryLookup);
        if (opfEntry == null)
            return (null, Array.Empty<string>());

        var opfDoc = TryLoadXml(opfEntry);
        if (opfDoc == null)
            return (null, Array.Empty<string>());

        var title = ExtractTitle(opfDoc);
        var manifest = ExtractManifest(opfDoc);
        var spineIdRefs = ExtractSpineIdRefs(opfDoc);

        if (manifest.Count == 0 || spineIdRefs.Count == 0)
            return (title, Array.Empty<string>());

        var opfDir = GetDirectoryPath(NormalizeZipPath(opfEntry.FullName));
        var pages = new List<string>();
        var seenPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var idRef in spineIdRefs)
        {
            if (!manifest.TryGetValue(idRef, out var item))
                continue;

            var itemPath = ResolveRelativePath(opfDir, item.Href);
            if (!entryLookup.TryGetValue(itemPath, out var itemEntry))
                continue;

            if (LocalComicHelper.IsPageImageFile(itemEntry.Name))
            {
                if (seenPages.Add(itemEntry.FullName))
                    pages.Add(itemEntry.FullName);
            }
            else if (IsWrapperDocument(itemEntry.Name, item.MediaType))
            {
                var wrapperDir = GetDirectoryPath(itemPath);
                foreach (var rawImgRef in ExtractImageReferences(itemEntry))
                {
                    var imgPath = ResolveRelativePath(wrapperDir, rawImgRef);
                    if (
                        entryLookup.TryGetValue(imgPath, out var imgEntry)
                        && LocalComicHelper.IsPageImageFile(imgEntry.Name)
                        && seenPages.Add(imgEntry.FullName)
                    )
                    {
                        pages.Add(imgEntry.FullName);
                    }
                }
            }
        }

        return (title, pages);
    }

    private static ZipArchiveEntry? FindOpfEntry(
        ZipArchive archive,
        IReadOnlyDictionary<string, ZipArchiveEntry> entryLookup
    )
    {
        if (entryLookup.TryGetValue("META-INF/container.xml", out var containerEntry))
        {
            var containerDoc = TryLoadXml(containerEntry);
            var rootfilePath = containerDoc
                ?.Descendants()
                .FirstOrDefault(e =>
                    e.Name.LocalName.Equals("rootfile", StringComparison.OrdinalIgnoreCase)
                )
                ?.Attribute("full-path")
                ?.Value;

            if (!string.IsNullOrWhiteSpace(rootfilePath))
            {
                var normalized = ResolveRelativePath(string.Empty, rootfilePath);
                if (entryLookup.TryGetValue(normalized, out var opfFromContainer))
                    return opfFromContainer;
            }
        }

        return archive.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".opf", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string? ExtractTitle(XDocument opfDoc)
    {
        var metadata = opfDoc
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase)
            );

        var searchRoot = (XContainer?)metadata ?? opfDoc;
        var titleValue = searchRoot
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(e.Value)
            )
            ?.Value.Trim();

        return string.IsNullOrWhiteSpace(titleValue) ? null : titleValue;
    }

    private static Dictionary<string, (string Href, string? MediaType)> ExtractManifest(
        XDocument opfDoc
    )
    {
        var manifest = new Dictionary<string, (string Href, string? MediaType)>(
            StringComparer.Ordinal
        );

        var manifestElement = opfDoc
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName.Equals("manifest", StringComparison.OrdinalIgnoreCase)
            );

        if (manifestElement == null)
            return manifest;

        foreach (
            var item in manifestElement
                .Elements()
                .Where(e => e.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase))
        )
        {
            var id = item.Attribute("id")?.Value;
            var href = item.Attribute("href")?.Value;
            var mediaType = item.Attribute("media-type")?.Value;

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(href))
                manifest[id] = (href, mediaType);
        }

        return manifest;
    }

    private static List<string> ExtractSpineIdRefs(XDocument opfDoc)
    {
        var spineElement = opfDoc
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName.Equals("spine", StringComparison.OrdinalIgnoreCase)
            );

        if (spineElement == null)
            return new List<string>();

        return spineElement
            .Elements()
            .Where(e => e.Name.LocalName.Equals("itemref", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("idref")?.Value)
            .Where(idRef => !string.IsNullOrWhiteSpace(idRef))
            .Select(idRef => idRef!)
            .ToList();
    }

    private static IEnumerable<string> ExtractImageReferences(ZipArchiveEntry wrapperEntry)
    {
        var doc = TryLoadXml(wrapperEntry);
        if (doc != null)
        {
            var refs = new List<string>();
            foreach (var element in doc.Descendants())
            {
                var localName = element.Name.LocalName;
                if (localName.Equals("img", StringComparison.OrdinalIgnoreCase))
                {
                    var src = element
                        .Attributes()
                        .FirstOrDefault(a =>
                            a.Name.LocalName.Equals("src", StringComparison.OrdinalIgnoreCase)
                        )
                        ?.Value;
                    if (!string.IsNullOrWhiteSpace(src))
                        refs.Add(src);
                }
                else if (localName.Equals("image", StringComparison.OrdinalIgnoreCase))
                {
                    var href = element
                        .Attributes()
                        .FirstOrDefault(a =>
                            a.Name.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase)
                        )
                        ?.Value;
                    if (!string.IsNullOrWhiteSpace(href))
                        refs.Add(href);
                }
            }
            return refs;
        }

        return ExtractImageReferencesFallback(wrapperEntry);
    }

    private static IEnumerable<string> ExtractImageReferencesFallback(ZipArchiveEntry wrapperEntry)
    {
        try
        {
            using var stream = wrapperEntry.Open();
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            var results = new List<string>();
            foreach (Match match in ImgOrSvgImageRegex().Matches(content))
            {
                var value = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(value))
                    results.Add(value);
            }
            return results;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static XDocument? TryLoadXml(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            using var xmlReader = XmlReader.Create(stream, SafeXmlSettings);
            return XDocument.Load(xmlReader);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsWrapperDocument(string fileName, string? mediaType)
    {
        if (
            mediaType != null
            && (
                mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return true;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return Array.IndexOf(WrapperExtensions, ext) >= 0;
    }

    internal static string ResolveRelativePath(string baseDir, string rawHref)
    {
        var queryOrFragmentIndex = rawHref.AsSpan().IndexOfAny('#', '?');
        var withoutFragment = queryOrFragmentIndex >= 0 ? rawHref[..queryOrFragmentIndex] : rawHref;
        var unescaped = Uri.UnescapeDataString(withoutFragment).Replace('\\', '/');

        var combined =
            unescaped.StartsWith('/') ? unescaped.TrimStart('/')
            : string.IsNullOrEmpty(baseDir) ? unescaped
            : $"{baseDir}/{unescaped}";

        var parts = combined.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(part);
            }
        }

        return string.Join('/', stack);
    }

    private static string NormalizeZipPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static string GetDirectoryPath(string normalizedPath)
    {
        var lastSlash = normalizedPath.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalizedPath[..lastSlash];
    }

    [GeneratedRegex(
        @"<(?:img|image)\b[^>]*?\s(?:src|href|xlink:href)\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ImgOrSvgImageRegex();
}
