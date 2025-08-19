using System;
using System.Collections.Generic;
using UnityEngine;

namespace FileSystem
{
    public class GameFileHandler : MonoBehaviour
    {
        public List<GameFile> availableFiles;
        public static GameFileHandler Instance;

        /// <summary>
        /// Loads all avaible files
        /// </summary>
        /// 
        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAll(availableFiles);
        }

        public GameFile SearchForFileByName(string name)
        {
            foreach (var file in availableFiles)
            {
                if (file.FileName == name) return file;
            };
            return null;
        }

        public void DeleteAvaible() => DeleteAll(availableFiles);

        public void LoadAll(List<GameFile> filesToLoad)
        {
            if (filesToLoad == null || filesToLoad.Count == 0)
            {
                Debug.LogWarning("No files to load :(");
                return;
            }
            foreach (GameFile file in filesToLoad)
            {
                file.Load(false);
            }
        }

        public void DeleteAll(List<GameFile> filesToLoad)
        {
            if (filesToLoad == null || filesToLoad.Count == 0)
            {
                Debug.LogWarning("No files to delete :(");
                return;
            }
            foreach (GameFile file in filesToLoad)
            {
                file.Delete();
                
            }
        }
    }
}