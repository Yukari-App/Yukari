using System;
using System.IO;
using Yukari.Models.Common;
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

        public static string GetComicDataPath(ContentKey comicKey) =>
            EnsureDirectory(Path.Combine(GetDataPath(), comicKey.Source, comicKey.Id));

        public static string GetComicChapterDataPath(ContentKey comicKey, string chapterId) =>
            EnsureDirectory(GetChapterPath(comicKey, chapterId));

        public static void DeleteComicChapterDataPath(ContentKey comicKey, string chapterId)
        {
            string path = GetChapterPath(comicKey, chapterId);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static string CopyDllToPluginsDirectory(string sourceDllPath)
        {
            string fileName = Path.GetFileName(sourceDllPath);
            string destPath = Path.Combine(GetPluginsPath(), fileName);
            File.Copy(sourceDllPath, destPath, true);
            return destPath;
        }

        private static string GetChapterPath(ContentKey comicKey, string chapterId) =>
            Path.Combine(GetComicDataPath(comicKey), chapterId);

        private static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
