using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteamPlayer
{
    public string PlayerName;
    public SteamId PlayerSteamId;
    public Image? PlayerImage;
    public Friend Player;
    public SteamPlayer(string playerName, SteamId playerSteamId, Image? playerImage, Friend player)
    {
        PlayerName = playerName;
        PlayerSteamId = playerSteamId;
        PlayerImage = playerImage;
        Player = player;
    }
}
