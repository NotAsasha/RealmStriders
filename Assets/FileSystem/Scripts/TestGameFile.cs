using System;
using UnityEngine;

namespace FileSystem.Scripts
{
    [CreateAssetMenu(fileName = "TestGameFile", menuName = "Not/TestGameFile")]
    public class TestGameFile : GameFile
    {
        [Header("TestGameFile Info")]
        
        public int buttonPresses = 0;
        protected override Action PrepareData(string inputData)
        {
            if (!int.TryParse(inputData, out int loaded))
                throw new System.IO.InvalidDataException("Expected an integer.");
            return () => buttonPresses = loaded;
        }

        protected override void ResetData() => buttonPresses = 0;
        public override string GetData()
        {
            Debug.Log($"GetData -- buttonPresses:" + buttonPresses + GetFullPath());
            return buttonPresses.ToString();
        }
        public void AddButtonClick()
        {
            buttonPresses++;
        }
    }
}
