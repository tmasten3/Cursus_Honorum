using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Game.Core.Save
{
    /// <summary>
    /// Handles low-level file system responsibilities for save slots.
    /// This class does not understand JSON or simulation structures; it only manages bytes.
    /// </summary>
    public class SaveRepository
    {
        public const string DefaultFileName = "autosave.json";
        public const string DefaultFileExtension = ".json";

        private readonly string saveDirectory;
        private readonly string defaultFileName;
        private readonly string fileExtension;

        public SaveRepository(string saveDirectory = null, string defaultFileName = DefaultFileName, string fileExtension = DefaultFileExtension)
        {
            this.saveDirectory = ResolveDirectory(saveDirectory);
            this.defaultFileName = string.IsNullOrWhiteSpace(defaultFileName) ? DefaultFileName : defaultFileName.Trim();
            this.fileExtension = NormalizeExtension(fileExtension);
        }

        public string SaveDirectory => saveDirectory;
        public string DefaultSlotName => defaultFileName;
        public string FileExtension => fileExtension;

        public bool SaveExists(string slotName = null)
        {
            string path = GetSaveFilePath(slotName);
            return File.Exists(path);
        }

        public SaveFileMetadata Write(string slotName, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string path = GetSaveFilePath(slotName);
            EnsureDirectoryExistsFor(path);
            File.WriteAllBytes(path, data);
            return BuildMetadata(path);
        }

        public SaveFileReadResult Read(string slotName)
        {
            string path = GetSaveFilePath(slotName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Save file not found.", path);

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null || bytes.Length == 0)
                throw new InvalidDataException("Save file was empty.");

            return new SaveFileReadResult(bytes, BuildMetadata(path));
        }

        public bool Delete(string slotName)
        {
            string path = GetSaveFilePath(slotName);
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }

        public IReadOnlyList<SaveFileMetadata> ListSaves()
        {
            if (string.IsNullOrWhiteSpace(saveDirectory) || !Directory.Exists(saveDirectory))
                return Array.Empty<SaveFileMetadata>();

            var searchPattern = string.IsNullOrEmpty(fileExtension)
                ? "*"
                : "*" + fileExtension;

            return Directory
                .EnumerateFiles(saveDirectory, searchPattern, SearchOption.TopDirectoryOnly)
                .Select(BuildMetadata)
                .OrderByDescending(meta => meta.LastModifiedUtc)
                .ToList();
        }

        public string GetSaveFilePath(string slotName)
        {
            string name = NormalizeSlotName(slotName);

            if (Path.IsPathRooted(name))
                return Path.GetFullPath(name);

            if (string.IsNullOrWhiteSpace(saveDirectory))
                return name;

            return Path.Combine(saveDirectory, name);
        }

        public void EnsureDirectoryExists()
        {
            if (!string.IsNullOrWhiteSpace(saveDirectory))
                Directory.CreateDirectory(saveDirectory);
        }

        private void EnsureDirectoryExistsFor(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                EnsureDirectoryExists();
            else
                Directory.CreateDirectory(directory);
        }

        private string NormalizeSlotName(string slotName)
        {
            string name = string.IsNullOrWhiteSpace(slotName) ? defaultFileName : slotName.Trim();

            if (Path.IsPathRooted(name))
                return name;

            name = Path.GetFileName(name);
            if (string.IsNullOrEmpty(name))
                name = defaultFileName;

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                builder.Append(invalidCharacters.Contains(c) ? '_' : c);
            }

            string sanitized = builder.ToString();
            if (!Path.HasExtension(sanitized) && !string.IsNullOrEmpty(fileExtension))
                sanitized += fileExtension;

            return sanitized;
        }

        private static string ResolveDirectory(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return Application.persistentDataPath;

            return Path.GetFullPath(candidate.Trim());
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return DefaultFileExtension;

            extension = extension.Trim();
            if (!extension.StartsWith('.'))
                extension = "." + extension;
            return extension;
        }

        private SaveFileMetadata BuildMetadata(string path)
        {
            var info = new FileInfo(path);
            string fileName = info.Exists ? info.Name : Path.GetFileName(path);
            string slotName = Path.GetFileNameWithoutExtension(fileName);
            DateTime lastModified = info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow;
            long size = info.Exists ? info.Length : 0L;

            return new SaveFileMetadata(slotName, fileName, Path.GetFullPath(path), lastModified, size);
        }
    }

    public readonly struct SaveFileReadResult
    {
        public SaveFileReadResult(byte[] data, SaveFileMetadata metadata)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Metadata = metadata;
        }

        public byte[] Data { get; }
        public SaveFileMetadata Metadata { get; }
    }

    public sealed class SaveFileMetadata
    {
        public SaveFileMetadata(string slotName, string fileName, string fullPath, DateTime lastModifiedUtc, long sizeInBytes)
        {
            SlotName = slotName ?? string.Empty;
            FileName = fileName ?? slotName ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            LastModifiedUtc = lastModifiedUtc;
            SizeInBytes = Math.Max(0, sizeInBytes);
        }

        public string SlotName { get; }
        public string FileName { get; }
        public string FullPath { get; }
        public DateTime LastModifiedUtc { get; }
        public long SizeInBytes { get; }
    }
}
