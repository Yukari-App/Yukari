using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Yukari.Helpers;

namespace Yukari.Tests.Helpers;

public class EpubHelperTests
{
    [Fact]
    public void ExtractContent_ReadsTitleAndSpineOrder_WithXhtmlAndSvgWrappers()
    {
        // Arrange
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(
                archive,
                "META-INF/container.xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles>
                    <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
                  </rootfiles>
                </container>
                """
            );

            AddTextEntry(
                archive,
                "OEBPS/content.opf",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>Volume 01 - Beginning</dc:title>
                  </metadata>
                  <manifest>
                    <item id="p_cover" href="Text/cover.xhtml" media-type="application/xhtml+xml"/>
                    <item id="p_001" href="Text/page001.xhtml" media-type="application/xhtml+xml"/>
                    <item id="p_002" href="Images/002.png" media-type="image/png"/>
                    <item id="img_cover" href="Images/z_cover.jpg" media-type="image/jpeg"/>
                    <item id="img_001" href="Images/001.jpg" media-type="image/jpeg"/>
                  </manifest>
                  <spine>
                    <itemref idref="p_cover"/>
                    <itemref idref="p_001"/>
                    <itemref idref="p_002"/>
                  </spine>
                </package>
                """
            );

            AddTextEntry(
                archive,
                "OEBPS/Text/cover.xhtml",
                """
                <!DOCTYPE html>
                <html xmlns="http://www.w3.org/1999/xhtml" xmlns:xlink="http://www.w3.org/1999/xlink">
                  <body>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 1600">
                      <image width="1200" height="1600" xlink:href="../Images/z_cover.jpg"/>
                    </svg>
                  </body>
                </html>
                """
            );

            AddTextEntry(
                archive,
                "OEBPS/Text/page001.xhtml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <html xmlns="http://www.w3.org/1999/xhtml">
                  <body>
                    <img src="../Images/001.jpg" alt="Page 1"/>
                  </body>
                </html>
                """
            );

            AddTextEntry(archive, "OEBPS/Images/z_cover.jpg", "fake-image-bytes");
            AddTextEntry(archive, "OEBPS/Images/001.jpg", "fake-image-bytes");
            AddTextEntry(archive, "OEBPS/Images/002.png", "fake-image-bytes");
            AddTextEntry(archive, "OEBPS/Images/ignored_logo.jpg", "fake-image-bytes");
        }

        stream.Position = 0;
        using var readArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Act
        var (title, pages) = EpubHelper.ExtractContent(readArchive);

        // Assert
        title.Should().Be("Volume 01 - Beginning");
        pages
            .Should()
            .Equal("OEBPS/Images/z_cover.jpg", "OEBPS/Images/001.jpg", "OEBPS/Images/002.png");
    }

    [Fact]
    public void ExtractContent_ReturnsNullTitleAndEmptyPages_WhenNoOpfExists()
    {
        // Arrange
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(archive, "001.jpg", "fake-image-bytes");
            AddTextEntry(archive, "002.jpg", "fake-image-bytes");
        }

        stream.Position = 0;
        using var readArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Act
        var (title, pages) = EpubHelper.ExtractContent(readArchive);

        // Assert
        title.Should().BeNull();
        pages.Should().BeEmpty();
    }

    [Fact]
    public void ExtractContent_ExtractsImagesViaFallbackRegex_WhenWrapperHtmlIsNotStrictXml()
    {
        // Arrange
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(
                archive,
                "content.opf",
                """
                <package>
                  <manifest>
                    <item id="p1" href="page1.html" media-type="text/html"/>
                  </manifest>
                  <spine>
                    <itemref idref="p1"/>
                  </spine>
                </package>
                """
            );

            // Unclosed <meta> and <br> tags make strict XML parsing fail
            AddTextEntry(
                archive,
                "page1.html",
                """
                <html>
                  <head><meta charset="utf-8"></head>
                  <body>
                    <br>
                    <img src="img/page%201.webp">
                  </body>
                </html>
                """
            );

            AddTextEntry(archive, "img/page 1.webp", "fake-image-bytes");
        }

        stream.Position = 0;
        using var readArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Act
        var (title, pages) = EpubHelper.ExtractContent(readArchive);

        // Assert
        title.Should().BeNull();
        pages.Should().Equal("img/page 1.webp");
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
