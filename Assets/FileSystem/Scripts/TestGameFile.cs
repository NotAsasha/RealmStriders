using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using UnityEngine;
using System;

namespace FileSystem
{
    [CreateAssetMenu(fileName = "TestGameFile", menuName = "Not/TestGameFile")]
    public class TestGameFile : GameFile
    {
        [Header("TestGameFile Info")]
        
        public int buttonPresses = 0;
        public override void ProcessData(string inputData)
        {
            if (inputData == null || inputData.Length == 0) { buttonPresses = 0; return; }
            Debug.Log("InputData:" + inputData + GetFullPath());
            buttonPresses = Convert.ToInt32(inputData);
        }
        public override string GetData()
        {
            Debug.Log("GetData -- buttonPresses:" + buttonPresses + GetFullPath());
            return buttonPresses.ToString();
        }
        public void AddButtonClick()
        {
            buttonPresses++;
        }
    }
}
