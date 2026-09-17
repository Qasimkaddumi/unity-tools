using Kaddumi.UnityTools.Save.Core;
using Kaddumi.UnityTools.Save.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// Writes each slot payload to its own file under
    /// <see cref="Application.persistentDataPath"/>. The default backend for real games:
    /// no size limit, human-readable JSON on disk, and easy to back up.
    ///
    /// Writes go through a temp file and an atomic replace so a crash mid-write can't
    /// corrupt an existing save. <see cref="EncodeBytes"/>/<see cref="DecodeBytes"/> are
    /// virtual so <see cref="EncryptedFileSaveProvider"/> can layer encryption on the same
    /// file logic.
    ///
    /// <para>Also implements <see cref="ISaveBlobProvider"/>, so screenshots and other
    /// binary payloads land on disk as-is instead of going through a base64 string.</para>
    /// </summary>
    public class FileSaveProvider : ISaveProvider, ISaveBlobProvider
    {
        private readonly string subFolder;
        private readonly string extension;

        private string rootPath;

        public bool IsInitialized { get; private set; }

        public FileSaveProvider(string subFolder = "Saves", string extension = ".save")
        {
            this.subFolder = subFolder ?? string.Empty;
            this.extension = NormalizeExtension(extension);
        }

        public void Initialize(Action onComplete)
        {
            rootPath = string.IsNullOrEmpty(subFolder)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, subFolder);

            try
            {
                Directory.CreateDirectory(rootPath);
                IsInitialized = true;
                Debug.Log($"<color=cyan>[Save-File]</color> Initialized at {rootPath}");
                onComplete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save-File] Could not create save directory '{rootPath}': {e.Message}");
                onComplete?.Invoke();
            }
        }

        // Text and binary share one path on disk: a payload is just bytes, and the UTF-8
        // conversion is the only thing that differs.
        public void Write(string key, string data, Action<SaveResult> onComplete) =>
            WriteBytes(key, Encoding.UTF8.GetBytes(data ?? string.Empty), onComplete);

        public void Read(string key, Action<SaveResult> onComplete) =>
            ReadBytes(key, result => onComplete?.Invoke(result.Success
                ? SaveResult.Ok(Encoding.UTF8.GetString(result.Data))
                : SaveResult.Fail(result.Error)));

        // --- Binary payloads (ISaveBlobProvider) ------------------------------

        public void WriteBytes(string key, byte[] data, Action<SaveResult> onComplete)
        {
            string path = PathFor(key);
            string temp = path + ".tmp";
            try
            {
                // Blob keys nest into sub-folders, which may not exist yet.
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllBytes(temp, EncodeBytes(data ?? Array.Empty<byte>()));

                // Atomic-ish replace: never leave a half-written file where the real save was.
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);

                onComplete?.Invoke(SaveResult.Ok());
            }
            catch (Exception e)
            {
                TryDelete(temp);
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Io, e.Message));
            }
        }

        public void ReadBytes(string key, Action<SaveBlobResult> onComplete)
        {
            string path = PathFor(key);
            if (!File.Exists(path))
            {
                onComplete?.Invoke(SaveBlobResult.Fail(SaveErrorType.NotFound,
                    $"No save file for key '{key}'."));
                return;
            }
            try
            {
                onComplete?.Invoke(SaveBlobResult.Ok(DecodeBytes(File.ReadAllBytes(path))));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveBlobResult.Fail(SaveErrorType.Corrupted, e.Message));
            }
        }

        public void Delete(string key, Action<SaveResult> onComplete)
        {
            try
            {
                TryDelete(PathFor(key));
                onComplete?.Invoke(SaveResult.Ok());
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Io, e.Message));
            }
        }

        public void Exists(string key, Action<bool> onComplete) =>
            onComplete?.Invoke(File.Exists(PathFor(key)));

        public void List(Action<string[]> onComplete)
        {
            var keys = new List<string>();
            if (Directory.Exists(rootPath))
            {
                // Recursive, because a slot's blobs live in their own sub-folder.
                foreach (string file in Directory.GetFiles(rootPath, "*" + extension, SearchOption.AllDirectories))
                {
                    keys.Add(KeyFor(file));
                }
            }
            onComplete?.Invoke(keys.ToArray());
        }

        // --- Encoding hooks (overridden by the encrypted provider) -------------

        /// <summary>
        /// Transforms a payload on its way to disk. Byte-to-byte, so a subclass treats slot
        /// text and binary blobs alike without needing to know which it is holding.
        /// </summary>
        protected virtual byte[] EncodeBytes(byte[] data) => data;

        /// <summary>Reverses <see cref="EncodeBytes"/> for data read back from disk.</summary>
        protected virtual byte[] DecodeBytes(byte[] bytes) => bytes;

        // --- Helpers ----------------------------------------------------------

        /// <summary>
        /// Maps a key to a file path. A '/' in the key becomes a folder separator, so a
        /// slot's blobs group into their own directory; every other character that can't
        /// safely appear in a file name is replaced, which also stops a key from escaping
        /// the save root.
        /// </summary>
        private string PathFor(string key) => Path.Combine(rootPath, Sanitize(key) + extension);

        /// <summary>Inverse of <see cref="PathFor"/>: turns a file path back into its key.</summary>
        private string KeyFor(string path)
        {
            string relative = path.Substring(rootPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Substring(0, relative.Length - extension.Length);
            return relative.Replace(Path.DirectorySeparatorChar, '/')
                           .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string Sanitize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "_";

            var builder = new StringBuilder(key.Length);
            foreach (char c in key)
            {
                if (c == '/') builder.Append(Path.DirectorySeparatorChar);
                else if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_') builder.Append(c);
                else builder.Append('_');
            }
            // Collapse any "..", so a crafted key can't walk up out of the save root.
            return builder.Replace("..", "__").ToString();
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return ".save";
            return ext.StartsWith(".") ? ext : "." + ext;
        }
    }
}
