using System;

namespace Yukari.Models.DTO;

public record LibraryExportManifest(
    string AppVersion,
    int DatabaseVersion,
    DateTime ExportedAtUtc,
    bool IncludesDownloads,
    bool IncludesPlugins,
    string OriginalAppDataPath
);
