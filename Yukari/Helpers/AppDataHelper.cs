using System;
using System.IO;
using Yukari.Models.DTO;

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

        public static string GetComicChapterDataPath(ContentKey comicKey, string chapterId) =>
            EnsureDirectory(GetChapterPath(comicKey, chapterId));

        public static bool DeleteComicChapterDataPath(ContentKey comicKey, string chapterId)
        {
            string path = GetChapterPath(comicKey, chapterId);
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

        private static string GetChapterPath(ContentKey comicKey, string chapterId) =>
            Path.Combine(GetDataPath(), comicKey.Source, comicKey.Id, chapterId);

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
