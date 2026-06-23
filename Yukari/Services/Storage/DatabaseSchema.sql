-- Current schema: Version 4
-- Last updated: Migration_004
-- To understand how we got here, read Migrations/ in order.

-- Comics definition

CREATE TABLE Comics (
    Id TEXT NOT NULL,
    Source TEXT NOT NULL,
    ComicUrl TEXT,
    Title TEXT NOT NULL,
    Author TEXT,
    Description TEXT,
    Status TEXT NOT NULL DEFAULT 'unknown',
    Tags TEXT,
    Year INTEGER,
    CoverImageUrl TEXT,
    Langs TEXT,
    IsAvailable INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (Id, Source)
);

-- ComicUserData definition

CREATE TABLE ComicUserData (
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    LastSelectedLang TEXT,
    LastReadAt TEXT,
    PRIMARY KEY (ComicId, Source)
);

-- ComicReadingProgress definition

CREATE TABLE ComicReadingProgress (
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    Language TEXT NOT NULL,
    LastChapterId TEXT,
    PRIMARY KEY (ComicId, Source, Language)
);

CREATE INDEX IX_ComicReadingProgress_ComicId_Source
    ON ComicReadingProgress (ComicId, Source);

-- Chapters definition

CREATE TABLE Chapters (
    Id TEXT NOT NULL,
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    Title TEXT,
    Number TEXT,
    Volume TEXT,
    Language TEXT NOT NULL,
    Groups TEXT,
    LastUpdate TEXT,
    Pages INTEGER,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsAvailable INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (Id, ComicId, Source),
    FOREIGN KEY (ComicId, Source) REFERENCES Comics(Id, Source) ON DELETE CASCADE
);

CREATE INDEX IX_Chapters_ComicId_Source
    ON Chapters (ComicId, Source);

-- ChapterUserData definition

CREATE TABLE ChapterUserData (
    Id TEXT NOT NULL,
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    LastPageRead INTEGER,
    IsDownloaded INTEGER,
    IsRead INTEGER,
    PRIMARY KEY (Id, ComicId, Source)
);

CREATE INDEX IX_ChapterUserData_ComicId_Source
    ON ChapterUserData (ComicId, Source);

-- ChapterPages definition

CREATE TABLE ChapterPages (
    Number INTEGER NOT NULL,
    ChapterId TEXT NOT NULL,
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    ImageUrl TEXT NOT NULL,
    PRIMARY KEY (ChapterId, ComicId, Source, Number),
    FOREIGN KEY (ChapterId, ComicId, Source)
        REFERENCES Chapters(Id, ComicId, Source) ON DELETE CASCADE
);

CREATE INDEX IX_ChapterPages_Chapter
    ON ChapterPages (ChapterId, ComicId, Source);

-- Collections definition

CREATE TABLE Collections (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL UNIQUE
);

-- ComicCollections definition

CREATE TABLE ComicCollections (
    ComicId TEXT NOT NULL,
    Source TEXT NOT NULL,
    CollectionId INTEGER NOT NULL,
    PRIMARY KEY (ComicId, Source, CollectionId),
    FOREIGN KEY (ComicId, Source) REFERENCES Comics(Id, Source) ON DELETE CASCADE,
    FOREIGN KEY (CollectionId) REFERENCES Collections(Id) ON DELETE CASCADE
);

-- ComicSources definition

CREATE TABLE ComicSources (
    Name TEXT PRIMARY KEY,
    Version TEXT NOT NULL,
    ReleasesPage TEXT,
    LogoUrl TEXT,
    Description TEXT,
    DllPath TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    PendingRemoval INTEGER NOT NULL DEFAULT 0,
    PendingUpdatePath TEXT    
);