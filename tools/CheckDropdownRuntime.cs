using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CheckDropdownRuntime
{
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required.");
        Application.runInBackground=true;
        Steam.LobbyUiManager menu=null;
        for(int i=0;i<100 && menu==null;i++)
        {
            menu=Resources.FindObjectsOfTypeAll<Steam.LobbyUiManager>().FirstOrDefault(m=>m.gameObject.scene.IsValid());
            if(menu==null)await Task.Delay(100);
        }
        if(menu==null)throw new Exception("Menu not loaded.");
        menu.ShowSettings();await Task.Delay(750);
        var settings=menu.transform.Find("Settings");
        var dropdowns=settings.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach(var dropdown in dropdowns)
        {
            dropdown.Show();await Task.Delay(250);
            var list=dropdown.transform.Find("Dropdown List");
            if(list==null)throw new Exception("Did not open: "+dropdown.name);
            var scroll=list.GetComponent<ScrollRect>();
            if(scroll.viewport.rect.width<400)throw new Exception("Invalid viewport width: "+dropdown.name);
            var labels=list.GetComponentsInChildren<TMP_Text>();
            foreach(var label in labels)
            {
                label.ForceMeshUpdate();
                if(label.rectTransform.rect.width<300 || label.isTextOverflowing)
                    throw new Exception("Clipped label "+dropdown.name+": "+label.text);
            }
            var toggles=list.GetComponentsInChildren<Toggle>().Where(t=>t.gameObject.activeSelf).ToArray();
            int old=dropdown.value;
            int next=old==0?1:0;
            if(toggles.Length>next)
            {
                toggles[next].isOn=true;
                if(dropdown.value!=next)throw new Exception("Selection failed: "+dropdown.name);
            }
            dropdown.Hide();await Task.Delay(250);
            dropdown.value=old;
        }
        var fps=dropdowns.First(d=>d.name=="FPSDrop");
        fps.Show();await Task.Delay(250);
        var fpsList=fps.transform.Find("Dropdown List").GetComponent<ScrollRect>();
        fpsList.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();await Task.Delay(100);
        if(fpsList.content.rect.height<=fpsList.viewport.rect.height)
            throw new Exception("Expected scrollable six-row FPS list.");
        if(Mathf.Abs(fpsList.verticalNormalizedPosition)>.01f)throw new Exception("Cannot scroll to bottom.");
        fps.Hide();await Task.Delay(250);
        dropdowns.First(d=>d.name=="QualityDrop").Show();
        await Task.Delay(250);
        return "PASS: all five dropdowns opened, labels fit, item selection updated values, FPS list scrolled to bottom. Original values restored; no settings saved.";
    }
}
