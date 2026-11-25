using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FileSystem.Scripts
{
    public class GameFile : ScriptableObject
    {
        [Header("File Settings")]
        [Tooltip("Displayed name of a file.")]
        [SerializeField] private string fileName = "Default";
        [Tooltip("File directory. Application.persistentDataPath is automatically added before it.")]
        [SerializeField] private string directory = "/Default/";
        [Tooltip("File extension. Can be anything, it does not matter.")]
        [SerializeField] private string fileExtension = ".NotA";
        [Tooltip("Whether to encrypt output files.")]
        [SerializeField] private bool useEncryption;

        private const string EncryptionCodeWord = "NotTheBestSaveSystem";
        private const string BackupSuffix = ".bak";
        private static readonly object FileLock = new();
        private static readonly UTF8Encoding Utf8 = new(false, true);
        protected static readonly JsonSerializer SaveSerializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore
        });

        private enum ReadStatus
        {
            Missing,
            Valid,
            Invalid,
            Unavailable,
            Unsupported
        }

        public string FileName => fileName;
        public string FileDirectory => directory;
        public string FileExtension => fileExtension;
        public bool IsLoadBlocked { get; private set; }

        public virtual string GetFullPath() => FileDirectory + FileName + FileExtension;
        public virtual string GetData() => "Default";
        protected virtual Action PrepareData(string inputData) => () => Debug.Log(inputData);
        protected virtual void ResetData() { }
        protected virtual void OnSaved(Action applyData) { }

        public void Load()
        {
            string path = Application.persistentDataPath + GetFullPath();
            lock (FileLock)
            {
                ReadStatus primary = ReadFile(path, out Action applyPrimary);
                if (primary == ReadStatus.Valid)
                {
                    IsLoadBlocked = false;
                    applyPrimary();
                    return;
                }

                if (primary != ReadStatus.Unsupported)
                {
                    ReadStatus backup = ReadFile(path + BackupSuffix, out Action applyBackup);
                    if (backup == ReadStatus.Valid)
                    {
                        IsLoadBlocked = false;
                        applyBackup();
                        Debug.LogWarning($"[{nameof(GameFile)}] Recovered {path} from its backup.");
                        return;
                    }

                    if (primary == ReadStatus.Missing && backup == ReadStatus.Missing)
                    {
                        IsLoadBlocked = false;
                        ResetData();
                        return;
                    }
                }

                IsLoadBlocked = true;
                ResetData();
                Debug.LogError($"[{nameof(GameFile)}] Could not load {path}.");
            }
        }

        public bool Save()
        {
            string path = Application.persistentDataPath + GetFullPath();
            string backupPath = path + BackupSuffix;
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Action applyData;

            lock (FileLock)
            {
                try
                {
                    ReadStatus primary = ReadFile(path, out _);
                    ReadStatus backup = ReadFile(backupPath, out _);
                    if (IsLoadBlocked || CannotReplace(primary) || CannotReplace(backup) ||
                        (primary != ReadStatus.Valid && backup != ReadStatus.Valid &&
                         (primary != ReadStatus.Missing || backup != ReadStatus.Missing)))
                    {
                        throw new IOException("The existing file or backup could not be verified.");
                    }

                    string data = GetData();
                    applyData = PrepareData(data);
                    byte[] bytes = Utf8.GetBytes(useEncryption ? DataEncryptDecrypt(data) : data);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    if (primary == ReadStatus.Missing)
                        File.Move(temporaryPath, path);
                    else
                        File.Replace(temporaryPath, path, primary == ReadStatus.Valid ? backupPath : null);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[{nameof(GameFile)}] Could not save {path}. {exception}");
                    return false;
                }
                finally
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (DirectoryNotFoundException) { }
                    catch (IOException exception)
                    {
                        Debug.LogWarning($"[{nameof(GameFile)}] Could not remove {temporaryPath}. {exception.Message}");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        Debug.LogWarning($"[{nameof(GameFile)}] Could not remove {temporaryPath}. {exception.Message}");
                    }
                }
            }

            try
            {
                OnSaved(applyData);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            return true;
        }

        public void Delete()
        {
            string path = Application.persistentDataPath + GetFullPath();
            lock (FileLock)
            {
                try
                {
                    File.Delete(path + BackupSuffix);
                    File.Delete(path);
                }
                catch (DirectoryNotFoundException) { }
                IsLoadBlocked = false;
                ResetData();
            }
        }

        private ReadStatus ReadFile(string path, out Action apply)
        {
            apply = null;
            try
            {
                string data = File.ReadAllText(path, Utf8);
                apply = PrepareData(useEncryption ? DataEncryptDecrypt(data) : data);
                return ReadStatus.Valid;
            }
            catch (FileNotFoundException) { return ReadStatus.Missing; }
            catch (DirectoryNotFoundException) { return ReadStatus.Missing; }
            catch (NotSupportedException exception)
            {
                Debug.LogWarning($"[{nameof(GameFile)}] Unsupported file {path}. {exception.Message}");
                return ReadStatus.Unsupported;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is JsonException ||
                                              exception is ArgumentException || exception is OverflowException)
            {
                Debug.LogWarning($"[{nameof(GameFile)}] Invalid file {path}. {exception.Message}");
                return ReadStatus.Invalid;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[{nameof(GameFile)}] Cannot read {path}. {exception.Message}");
                return ReadStatus.Unavailable;
            }
        }

        private static bool CannotReplace(ReadStatus status) =>
            status == ReadStatus.Unavailable || status == ReadStatus.Unsupported;

        protected static JObject ReadJsonObject(string data)
        {
            using JsonTextReader reader = new(new StringReader(data)) { DateParseHandling = DateParseHandling.None };
            JObject value = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (reader.Read())
                throw new InvalidDataException("Unexpected data after the JSON object.");
            return value;
        }

        protected static int ReadFormatVersion(JObject data, int currentVersion)
        {
            if (!HasField(data, "formatVersion", JTokenType.Integer))
                return 0;

            string storedVersion = data["formatVersion"].ToString(Formatting.None);
            if (storedVersion.StartsWith("-", StringComparison.Ordinal))
                throw new InvalidDataException("The format version must be nonnegative.");
            if (!int.TryParse(storedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out int version) ||
                version > currentVersion)
                throw new NotSupportedException($"A newer game version wrote format {storedVersion}.");
            return version;
        }

        protected static bool HasField(JObject data, string name, JTokenType expected, bool required = false)
        {
            JToken field = data[name];
            if (field == null && !required)
                return false;
            if (field?.Type != expected)
                throw new InvalidDataException($"Expected {name} to be {expected}.");
            return true;
        }

        protected static bool HasNumber(JObject data, string name, bool required = false)
        {
            JToken field = data[name];
            if (field == null && !required)
                return false;
            if ((field?.Type != JTokenType.Integer && field?.Type != JTokenType.Float) ||
                !float.IsFinite(field.Value<float>()))
                throw new InvalidDataException($"Expected {name} to be a finite number.");
            return true;
        }

        public string DataEncryptDecrypt(string data)
        {
            char[] characters = data.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
                characters[i] ^= EncryptionCodeWord[i % EncryptionCodeWord.Length];
            return new string(characters);
        }
    }
}
