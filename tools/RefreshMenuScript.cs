using UnityEditor;
public static class RefreshMenuScript
{
    public static void Run() => AssetDatabase.ImportAsset("Assets/Steam/LobbyUiManager.cs", ImportAssetOptions.ForceUpdate);
}
