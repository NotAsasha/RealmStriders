using UnityEditor;
public static class RefreshPauseShopScripts
{
    public static void Run() => AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
}
