using System.Collections.Generic;

namespace Yukari.Models.DTO;

public record LocalChapterScanResult(ChapterModel Chapter, IReadOnlyList<ChapterPageModel> Pages);
