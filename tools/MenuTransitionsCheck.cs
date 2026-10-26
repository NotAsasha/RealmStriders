using System;
using System.Linq;
using System.Threading.Tasks;
using Steam;
using UnityEngine;
using Assets.Player.UI;

public static class MenuTransitionsCheck
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying) throw new Exception("Play Mode required.");
        bool oldBackground=Application.runInBackground;
        Application.runInBackground=true;
        LobbyUiManager menu=null;
        for(int i=0;i<100 && menu==null;i++)
        {
            menu=Resources.FindObjectsOfTypeAll<LobbyUiManager>().SingleOrDefault(m=>m.gameObject.scene.IsValid());
            if(menu==null)await Task.Delay(100);
        }
        if(menu==null) { Application.runInBackground=oldBackground; throw new Exception("Menu did not load."); }
        var main=menu.transform.Find("Base").gameObject;
        var join=menu.transform.Find("JoinLobby").gameObject;
        var host=menu.transform.Find("SaveSelector").gameObject;
        var settings=menu.transform.Find("Settings").gameObject;
        float oldScale=Time.timeScale;
        Application.runInBackground=true;
        try
        {
            await Settle(menu);
            menu.ShowMainMenu();
            await Settle(menu);
            Time.timeScale=0f;
            var title=join.transform.Find("Title").GetComponent<RectTransform>();
            Vector2 origin=title.anchoredPosition;
            menu.ShowJoinLobby();
            if(!main.activeSelf || !join.activeSelf) throw new Exception("Crossfade should keep both pages active.");
            if(main.GetComponent<CanvasGroup>().interactable || join.GetComponent<CanvasGroup>().blocksRaycasts)
                throw new Exception("Input was not blocked.");
            menu.ShowHostLobby();
            await Settle(menu);
            if(!join.activeSelf || main.activeSelf || host.activeSelf) throw new Exception("Repeated click changed transition destination.");
            if(join.GetComponent<CanvasGroup>().alpha!=1f || !join.GetComponent<CanvasGroup>().interactable)
                throw new Exception("Incoming page did not restore.");
            if((title.anchoredPosition-origin).sqrMagnitude>.001f) throw new Exception("Title drift.");
            menu.ShowMainMenu(); await Settle(menu);
            menu.ShowHostLobby(); await Settle(menu);
            if(!host.activeSelf || main.activeSelf) throw new Exception("Host transition failed.");
            menu.ShowMainMenu(); await Settle(menu);
            menu.ShowSettings(); await Settle(menu);
            settings.GetComponent<SettingsMenu>().Close();
            if(!settings.activeSelf) throw new Exception("Settings closed before its fade.");
            await Settle(menu);
            if(!main.activeSelf || settings.activeSelf) throw new Exception("Settings return failed.");
            menu.ShowJoinLobby();
            menu.enabled=false;
            if(!join.activeSelf || !join.GetComponent<CanvasGroup>().interactable ||
                join.GetComponent<CanvasGroup>().alpha!=1f) throw new Exception("Interrupted transition left an unusable page.");
            menu.enabled=true;
            menu.ShowMainMenu(); await Settle(menu);
            return "PASS in Play Mode: crossfade, blocked clicks, timeScale=0, host/join/settings round trips, deferred Settings close, title reset, interrupted transition cleanup.";
        }
        finally { Time.timeScale=oldScale; menu.enabled=true; Application.runInBackground=oldBackground; }
    }

    private static async Task Settle(LobbyUiManager menu)
    {
        var field=typeof(LobbyUiManager).GetField("transition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        for(int i=0;i<100 && field.GetValue(menu)!=null;i++) await Task.Delay(100);
        if(field.GetValue(menu)!=null) throw new Exception("Transition did not finish within 10 seconds.");
        if(menu.transform.Find("Base").gameObject.activeSelf &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
            throw new Exception("Returning to Base retained an automatic button selection.");
    }
}
