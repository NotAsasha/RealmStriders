using Steamworks;
using Steamworks.Data;

namespace Player.Network
{
    public class SteamPlayer
    {
        public string playerName;
        public SteamId playerSteamId;
        public Image? playerImage;
        public Friend player;
        public SteamPlayer(string playerName, SteamId playerSteamId, Image? playerImage, Friend player)
        {
            this.playerName = playerName;
            this.playerSteamId = playerSteamId;
            this.playerImage = playerImage;
            this.player = player;
        }
    }
}
