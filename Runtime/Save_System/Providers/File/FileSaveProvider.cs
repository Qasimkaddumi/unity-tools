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
    /// corrupt an existing save. <see cref="Encode"/>/<see cref="Decode"/> are virtual so
    /// <see cref="EncryptedFileSaveProvider"/> can layer encryption on the same file logic.
    /// </summary>
    public class FileSaveProvider : ISaveProvider
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

        public void Write(string key, string data, Action<SaveResult> onComplete)
        {
            string path = PathFor(key);
            string temp = path + ".tmp";
            try
            {
                File.WriteAllBytes(temp, Encode(data));

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

        public void Read(string key, Action<SaveResult> onComplete)
        {
            string path = PathFor(key);
            if (!File.Exists(path))
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.NotFound, $"No save file for key '{key}'."));
                return;
            }
            try
            {
                string data = Decode(File.ReadAllBytes(path));
                onComplete?.Invoke(SaveResult.Ok(data));
            }
            catch (Exception e)
            {
                onComplete?.Invoke(SaveResult.Fail(SaveErrorType.Corrupted, e.Message));
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
                foreach (var file in Directory.GetFiles(rootPath, "*" + extension))
                {
                    keys.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            onComplete?.Invoke(keys.ToArray());
        }

        // --- Encoding hooks (overridden by the encrypted provider) -------------

        /// <summary>Converts the payload string to the bytes stored on disk.</summary>
        protected virtual byte[] Encode(string data) => Encoding.UTF8.GetBytes(data);

        /// <summary>Converts stored bytes back into the payload string.</summary>
        protected virtual string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);

        // --- Helpers ----------------------------------------------------------

        private string PathFor(string key) => Path.Combine(rootPath, key + extension);

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
