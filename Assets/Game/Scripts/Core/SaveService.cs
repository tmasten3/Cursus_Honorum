using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Save;
using Game.Systems.EventBus;
using Game.Systems.Time;

namespace Game.Core
{
    /// <summary>
    /// Orchestrates persistence by delegating serialization to <see cref="SaveSerializer"/> and file I/O to <see cref="SaveRepository"/>.
    /// </summary>
    public class SaveService
    {
        public const string DefaultFileName = SaveRepository.DefaultFileName;

        private readonly SaveRepository repository;
        private readonly SaveSerializer serializer;
        private GameState boundState;

        public SaveService() : this(null, null, null)
        {
        }

        public SaveService(string saveDirectory, string defaultFileName = DefaultFileName)
            : this(null, new SaveRepository(saveDirectory, defaultFileName), new SaveSerializer())
        {
        }

        public SaveService(GameState state = null, SaveRepository repository = null, SaveSerializer serializer = null)
        {
            boundState = state;
            this.repository = repository ?? new SaveRepository();
            this.serializer = serializer ?? new SaveSerializer();
        }

        public GameState BoundState => boundState;
        public SaveRepository Repository => repository;
        public SaveSerializer Serializer => serializer;

        public void BindGameState(GameState state)
        {
            boundState = state;
        }

        public SaveOperationResult SaveGame(string slotName = null)
        {
            var state = EnsureState();
            string label = slotName ?? repository.DefaultSlotName;
            Logger.Info("SaveService", $"Save began for slot '{label}'.");

            SaveData data;
            try
            {
                data = serializer.CreateSaveData(state);
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to capture game state: {ex.Message}");
                return SaveOperationResult.Failed(ex.Message);
            }

            var validation = serializer.Validate(data);
            LogValidation(validation);

            if (!validation.IsValid)
            {
                Logger.Warn("SaveService", "Save aborted due to validation errors.");
                return SaveOperationResult.Failed("Validation failed.", validation);
            }

            string json;
            try
            {
                json = serializer.Serialize(data);
            }
            catch (SaveDataValidationException ex)
            {
                Logger.Warn("SaveService", $"Save serialization blocked: {ex.Message}");
                LogValidation(ex.Result);
                return SaveOperationResult.Failed(ex.Message, ex.Result);
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to serialize save data: {ex.Message}");
                return SaveOperationResult.Failed(ex.Message, validation);
            }

            SaveFileMetadata metadata;
            try
            {
                metadata = repository.Write(slotName, Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to write save file: {ex.Message}");
                return SaveOperationResult.Failed(ex.Message, validation);
            }

            Logger.Info("SaveService", $"Save completed for slot '{metadata.SlotName}' at {metadata.FullPath}.");
            PublishSavedEvent(state, metadata, data, validation);

            return SaveOperationResult.Succeeded(metadata, data, validation);
        }

        public LoadOperationResult LoadGame(string slotName = null)
        {
            var state = EnsureState();
            string label = slotName ?? repository.DefaultSlotName;
            Logger.Info("SaveService", $"Load began for slot '{label}'.");

            SaveFileReadResult readResult;
            try
            {
                readResult = repository.Read(slotName);
            }
            catch (FileNotFoundException)
            {
                Logger.Warn("SaveService", $"Save file not found for slot '{label}'.");
                return LoadOperationResult.Failed("Save file not found.");
            }
            catch (InvalidDataException ex)
            {
                Logger.Warn("SaveService", $"Save file '{label}' was corrupt: {ex.Message}");
                return LoadOperationResult.Failed(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to read save file: {ex.Message}");
                return LoadOperationResult.Failed(ex.Message);
            }

            string json;
            try
            {
                json = Encoding.UTF8.GetString(readResult.Data);
            }
            catch (Exception ex)
            {
                Logger.Warn("SaveService", $"Save file '{label}' could not be decoded: {ex.Message}");
                return LoadOperationResult.Failed(ex.Message, metadata: readResult.Metadata);
            }

            SaveData data;
            try
            {
                data = serializer.Deserialize(json);
            }
            catch (Exception ex)
            {
                Logger.Warn("SaveService", $"Save file '{label}' was invalid: {ex.Message}");
                return LoadOperationResult.Failed(ex.Message, metadata: readResult.Metadata);
            }

            var validation = serializer.Validate(data);
            LogValidation(validation);

            if (!validation.IsValid)
            {
                Logger.Warn("SaveService", $"Load aborted due to validation errors for slot '{label}'.");
                return LoadOperationResult.Failed("Validation failed.", validation, readResult.Metadata, data);
            }

            try
            {
                serializer.ApplyToGameState(state, data);
            }
            catch (SaveDataValidationException ex)
            {
                Logger.Warn("SaveService", $"Save data rejected: {ex.Message}");
                LogValidation(ex.Result);
                return LoadOperationResult.Failed(ex.Message, ex.Result, readResult.Metadata, data);
            }
            catch (Exception ex)
            {
                Logger.Error("SaveService", $"Failed to apply save data: {ex.Message}");
                return LoadOperationResult.Failed(ex.Message, validation, readResult.Metadata, data);
            }

            Logger.Info("SaveService", $"Load completed for slot '{readResult.Metadata.SlotName}'.");
            PublishLoadedEvent(state, readResult.Metadata, data, validation);

            return LoadOperationResult.Succeeded(readResult.Metadata, data, validation);
        }

        public IReadOnlyList<SaveFileMetadata> ListSaves()
        {
            return repository.ListSaves();
        }

        public bool DeleteSave(string slotName = null)
        {
            return repository.Delete(slotName);
        }

        public bool HasSave(string slotName = null)
        {
            return repository.SaveExists(slotName);
        }

        public string Save(GameState state, string fileName = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            BindGameState(state);
            var result = SaveGame(fileName);
            return result.Success ? result.Metadata.FullPath : null;
        }

        public bool LoadInto(GameState state, string fileName = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            BindGameState(state);
            var result = LoadGame(fileName);
            return result.Success;
        }

        public bool Delete(string fileName = null)
        {
            return DeleteSave(fileName);
        }

        private GameState EnsureState()
        {
            if (boundState == null)
                throw new InvalidOperationException("SaveService requires an initialized GameState instance.");
            return boundState;
        }

        private void PublishSavedEvent(GameState state, SaveFileMetadata metadata, SaveData data, SaveValidationResult validation)
        {
            if (state == null)
                return;

            var bus = state.GetSystem<EventBus>();
            if (bus == null)
                return;

            var date = GetCurrentDate(state);
            var warnings = (validation?.Warnings ?? Array.Empty<string>()).ToArray();
            bus.Publish(new Game.Core.Save.OnGameSavedEvent(
                metadata?.SlotName ?? repository.DefaultSlotName,
                metadata?.FullPath ?? string.Empty,
                metadata?.LastModifiedUtc ?? data.TimestampUtc,
                metadata?.SizeInBytes ?? 0L,
                data.Version,
                data.TimestampUtc,
                warnings,
                date.year,
                date.month,
                date.day));
        }

        private void PublishLoadedEvent(GameState state, SaveFileMetadata metadata, SaveData data, SaveValidationResult validation)
        {
            if (state == null)
                return;

            var bus = state.GetSystem<EventBus>();
            if (bus == null)
                return;

            var date = GetCurrentDate(state);
            var warnings = (validation?.Warnings ?? Array.Empty<string>()).ToArray();
            bus.Publish(new Game.Core.Save.OnGameLoadedEvent(
                metadata?.SlotName ?? repository.DefaultSlotName,
                metadata?.FullPath ?? string.Empty,
                metadata?.LastModifiedUtc ?? data.TimestampUtc,
                DateTime.UtcNow,
                data.Version,
                data.TimestampUtc,
                warnings,
                date.year,
                date.month,
                date.day));
        }

        private static (int year, int month, int day) GetCurrentDate(GameState state)
        {
            if (state == null)
                return (0, 0, 0);

            var timeSystem = state.GetSystem<TimeSystem>();
            return timeSystem != null ? timeSystem.GetCurrentDate() : (0, 0, 0);
        }

        private static void LogValidation(SaveValidationResult validation)
        {
            if (validation == null)
                return;

            foreach (var warning in validation.Warnings ?? Array.Empty<string>())
                Logger.Warn("SaveService", $"Validation warning: {warning}");

            foreach (var error in validation.Errors ?? Array.Empty<string>())
                Logger.Warn("SaveService", $"Validation error: {error}");
        }
    }

    public sealed class SaveOperationResult
    {
        private SaveOperationResult(bool success, SaveFileMetadata metadata, SaveData data, SaveValidationResult validation, string error)
        {
            Success = success;
            Metadata = metadata;
            Data = data;
            Validation = validation;
            ErrorMessage = error;
        }

        public bool Success { get; }
        public SaveFileMetadata Metadata { get; }
        public SaveData Data { get; }
        public SaveValidationResult Validation { get; }
        public string ErrorMessage { get; }

        public static SaveOperationResult Succeeded(SaveFileMetadata metadata, SaveData data, SaveValidationResult validation)
        {
            return new SaveOperationResult(true, metadata, data, validation, null);
        }

        public static SaveOperationResult Failed(string error, SaveValidationResult validation = null)
        {
            return new SaveOperationResult(false, null, null, validation, error);
        }

    }

    public sealed class LoadOperationResult
    {
        private LoadOperationResult(bool success, SaveFileMetadata metadata, SaveData data, SaveValidationResult validation, string error)
        {
            Success = success;
            Metadata = metadata;
            Data = data;
            Validation = validation;
            ErrorMessage = error;
        }

        public bool Success { get; }
        public SaveFileMetadata Metadata { get; }
        public SaveData Data { get; }
        public SaveValidationResult Validation { get; }
        public string ErrorMessage { get; }

        public static LoadOperationResult Succeeded(SaveFileMetadata metadata, SaveData data, SaveValidationResult validation)
        {
            return new LoadOperationResult(true, metadata, data, validation, null);
        }

        public static LoadOperationResult Failed(string error, SaveValidationResult validation = null, SaveFileMetadata metadata = null, SaveData data = null)
        {
            return new LoadOperationResult(false, metadata, data, validation, error);
        }
    }
}
