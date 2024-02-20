using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;

namespace FileSystem
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
         ProcessData(), does something with data from the file on drive, meant to be overdrived.
         GetData(), returns every data from this object to store on a drive.
         Load(),Save(),Delete() - directly effects file on a drive.
         DataEncryptDecrypt() - Encrypts given data using key word from "encryptionCodeWord".
        */
        [Header("File Settings")]
        [Tooltip("Displayed name of a file.")]
        [SerializeField] private string _fileName = "Default";
        [Tooltip("File directory. Application.persistentDataPath is automatically added before it.")]
        [SerializeField] private string _directory = "/Default/";
        [Tooltip("File extention. Can be anything, it does not matter.")]
        [SerializeField] private string _fileExtention = ".NotA";

        private const string encryptionCodeWord = "NotTheBestSaveSystem";

        public string FileName => _fileName;
        public string FileDirectory => _directory;
        public string FileExtention => _fileExtention;
        /// <summary>
        /// Returns full file path (WITHOUT Application.persistentDataPath!)  
        /// </summary>
        public virtual string GetFullPath()
        {
            string fullPath = Path.Combine(FileDirectory + FileName + FileExtention);
            return fullPath;
        }
        /// <summary>
        /// Does something with data from the file on drive
        /// </summary>
        public virtual void ProcessData(string inputData)
        {
           Debug.Log(inputData);
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
        public void Load(bool useDecryption)
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
                if (useDecryption) dataToLoad = DataEncryptDecrypt(dataToLoad);
                ProcessData(dataToLoad);
            }
            catch (Exception e)
            {
                Debug.LogError("Error occured when trying to load data from file: " + filePath + "\n" + e);
            }
        }
        /// <summary>
        /// Tries to save some data to the file
        /// </summary>
        public void Save(bool useEncryption)
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
                Debug.LogError($"Error saving data to file: {filePath}\n{e}");
            }
            Debug.Log("File saved:" + filePath);
        }
        /// <summary>
        /// Deletes current file
        /// </summary>
        public void Delete()
        {
            string path = Path.Combine(Application.persistentDataPath + GetFullPath());
            File.Delete(path);
            ProcessData("");
            Debug.Log("File deleted:" + path);
        }
        /// <summary>
        /// Encrypts given data
        /// </summary>
        public string DataEncryptDecrypt(string data)
        {
            string modifiedData = "";
            for (int i = 0; i < data.Length; i++)
            {
                modifiedData += (char)(data[i] ^ encryptionCodeWord[i % encryptionCodeWord.Length]);
            }
            return modifiedData;
        }
        
    }
}
