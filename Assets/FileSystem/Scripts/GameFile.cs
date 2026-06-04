using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

namespace FileSystem.Scripts
{
    public class GameFile : ScriptableObject
    {
        /*
         ---This is a base game file script, is not meant to be used itself:
         You should inherit from this object like this: "public class TestGameFile : GameFile"
         Fields to set up: "_fileName", "_directory", "_fileExtention".
         encryptionCodeWord - Word to be used as a password for encryption.
         ---Has basic methods:
         GetFullPath(), returns full path of the file.
         ProcessData(), does something with data from the file on drive, meant to be overwriten.
         GetData(), returns every data from this object to store on a drive.
         Load(),Save(),Delete() - directly effects file on a drive.
         DataEncryptDecrypt() - Encrypts given data using key word from "encryptionCodeWord".
        */
        [Header("File Settings")]
        [Tooltip("Displayed name of a file.")]
        [SerializeField] private string fileName = "Default";
        [Tooltip("File directory. Application.persistentDataPath is automatically added before it.")]
        [SerializeField] private string directory = "/Default/";
        [Tooltip("File extension. Can be anything, it does not matter.")]
        [SerializeField] private string fileExtension = ".NotA";
        [Tooltip("Whether to encrypt output files.")]
        [SerializeField] private bool useEncryption = false;

        private const string EncryptionCodeWord = "NotTheBestSaveSystem";

        public string FileName => fileName;
        public string FileDirectory => directory;
        public string FileExtension => fileExtension;
        /// <summary>
        /// Returns full file path (WITHOUT Application.persistentDataPath!)  
        /// </summary>
        public virtual string GetFullPath()
        {
            string fullPath = Path.Combine(FileDirectory + FileName + FileExtension);
            return fullPath;
        }
        /// <summary>
        /// Does something with data from the file on drive
        /// </summary>
        public virtual void ProcessData(string inputData)
        {
            Debug.Log("---GameFile: " + inputData);
        }
        /// <summary>
        /// Returns every data from this object to store on a drive.
        /// </summary>
        public virtual string GetData()
        {
            return "Default";
        }

        /// <summary>
        /// Tries to load all data from the file
        /// </summary>
        public void Load()
        {
            string filePath = Path.Combine(Application.persistentDataPath + GetFullPath());
            string dirPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dirPath)) { Directory.CreateDirectory(dirPath); }
            if (!File.Exists(filePath)) { ProcessData(""); return; }
            try
            {
                string dataToLoad = "";
                using StreamReader reader = new(filePath);
                dataToLoad = reader.ReadToEnd();
                if (useEncryption) dataToLoad = DataEncryptDecrypt(dataToLoad);
                ProcessData(dataToLoad);
            }
            catch (Exception e)
            {
                Debug.LogError("---GameFile: Error occured when trying to load data from file: " + filePath + "\n" + e);
            }
        }
        /// <summary>
        /// Tries to save some data to the file
        /// </summary>
        public void Save()
        {
            string filePath = Path.Combine(Application.persistentDataPath + GetFullPath());
            string dirPath = Path.GetDirectoryName(filePath);
            try
            {
                // create the directory the file will be written to if it doesn't already exist
                if (!Directory.Exists(dirPath)) { Directory.CreateDirectory(dirPath); }

                string dataToStore = GetData();

                // optionally encrypt the data
                if (useEncryption)
                {
                    dataToStore = DataEncryptDecrypt(dataToStore);
                }

                // write the serialized data to the file
                using StreamWriter writer = new(filePath);
                writer.Write(dataToStore);
            }
            catch (Exception e)
            {
                Debug.LogError($"---GameFile: Error saving data to file: {filePath}\n{e}");
            }
            Debug.Log($"---{nameof(GameFile)}: File saved: {filePath}");
        }
        /// <summary>
        /// Deletes current file
        /// </summary>
        public void Delete()
        {
            string path = Path.Combine(Application.persistentDataPath + GetFullPath());
            File.Delete(path);
            ProcessData("");
            Debug.Log($"---{nameof(GameFile)} File deleted: {path}");
        }
        /// <summary>
        /// Encrypts given data
        /// </summary>
        public string DataEncryptDecrypt(string data)
        {
            string modifiedData = "";
            for (int i = 0; i < data.Length; i++)
            {
                modifiedData += (char)(data[i] ^ EncryptionCodeWord[i % EncryptionCodeWord.Length]);
            }
            return modifiedData;
        }

    }
}
