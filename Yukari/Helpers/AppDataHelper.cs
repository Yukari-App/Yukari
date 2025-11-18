using System;
using System.IO;
using Yukari.Models;

namespace Yukari.Helpers
{
    public static class AppDataHelper
    {
        private static readonly string _appDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Yukari");

        public static string GetAppDataPath() =>
            EnsureDirectory(_appDataPath);

        public static string GetDataPath() => 
            EnsureDirectory(Path.Combine(GetAppDataPath(), "Data"));

        public static string GetPluginsPath() => 
            EnsureDirectory(Path.Combine(GetAppDataPath(), "Plugins"));

        public static string GetComicChapterDataPath(ContentIdentifier comicIdentifier, string chapterId) =>
            EnsureDirectory(GetChapterPath(comicIdentifier, chapterId));

        public static bool DeleteComicChapterDataPath(ContentIdentifier comicIdentifier, string chapterId)
        {
            string path = GetChapterPath(comicIdentifier, chapterId);
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    return true;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetChapterPath(ContentIdentifier comicIdentifier, string chapterId) =>
            Path.Combine(GetDataPath(), comicIdentifier.Source, comicIdentifier.Id, chapterId);

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
