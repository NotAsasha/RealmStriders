using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteamInitialize : MonoBehaviour
{
    public static SteamInitialize instance;
    public bool connectedToSteam = true;


    public readonly static uint appId = 480;
    public string PlayerName;
    public SteamId PlayerSteamId;
    public string playerSteamIdString;
    public void Start()
    {
        if (instance == null)
        {
            DontDestroyOnLoad(gameObject);
            instance = this;
            try
            {
                // Create client
                SteamClient.Init(appId, true);
                if (!SteamClient.IsValid)
                {
                    Debug.Log("Steam client not valid");
                    throw new Exception();
                }
                connectedToSteam = true;
                Debug.Log("Steam still hasn`t broke down");
            }
            catch (Exception e)
            {
                connectedToSteam = false;
                Debug.LogWarning("Error connecting to Steam");
                Debug.LogWarning(e);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }


    public bool TryToReconnectToSteam()
    {
        Debug.Log("Attempting to reconnect to Steam");
        try
        {
            // Create client
            SteamClient.Init(appId, true);

            if (!SteamClient.IsValid)
            {
                Debug.Log("Steam client not valid");
                throw new Exception();
            }

            Debug.Log("Steam still hasn`t broke down after reconnecting");
            connectedToSteam = true;
            return true;
        }
        catch (Exception e)
        {
            connectedToSteam = false;
            Debug.Log("Error connecting to Steam");
            Debug.Log(e);
            return false;
        }
    }
}
