using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Steam;

public static class MenuSaveSelectionCheck
{
    private static void Click(Button button)
    {
        for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
            button.onClick.SetPersistentListenerState(i,
                button.onClick.GetPersistentTarget(i) is LobbyUiManager ? UnityEventCallState.EditorAndRuntime : UnityEventCallState.Off);
        button.onClick.Invoke();
    }

    public static string Verify()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var original=Resources.FindObjectsOfTypeAll<LobbyUiManager>().Single(m=>m.gameObject.scene.IsValid() && !EditorSceneManager.IsPreviewScene(m.gameObject.scene));
            var root=UnityEngine.Object.Instantiate(original.gameObject);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var menu=root.GetComponent<LobbyUiManager>();
            menu.ShowHostLobby();
            var host=root.transform.Find("SaveSelector");
            var create=host.Find("Footer/Create Lobby").GetComponent<Button>();
            if(create.interactable)throw new Exception("Create enabled without selection.");
            if(create.onClick.GetPersistentEventCount()!=1 || create.onClick.GetPersistentMethodName(0)!="CreateLobbyButton")
                throw new Exception("Create has unguarded side effects.");
            // No selection: must return without file, scene, or network operations.
            Click(create);
            if(!host.gameObject.activeSelf || create.interactable)throw new Exception("Guard failed.");
            for(int i=0;i<3;i++)
            {
                var card=host.Find("Saves").GetChild(i);
                Click(card.GetComponentInChildren<LoadSaveButton>().GetComponent<Button>());
                if(!create.interactable)throw new Exception("Selection did not unlock Create.");
                for(int j=0;j<3;j++)
                {
                    var other=host.Find("Saves").GetChild(j);
                    string label=other.GetComponentInChildren<LoadSaveButton>().GetComponentInChildren<TMP_Text>().text;
                    if(label!=(i==j?"SELECTED":"SELECT"))throw new Exception("Selection labels not exclusive.");
                }
            }
            menu.SelectSave(99);
            if(!create.interactable)throw new Exception("Invalid index corrupted selection.");
            var management=root.transform.Find("FileManagement").gameObject;
            var confirm=root.transform.Find("DeleteConfirmation").gameObject;
            menu.DeleteFiles(); // Confirmation is closed, so this must be a no-op.
            Click(host.Find("Delete").GetComponent<Button>());
            if(!management.activeSelf || confirm.activeSelf || host.GetComponent<CanvasGroup>().interactable)
                throw new Exception("Management modal did not isolate background.");
            Click(management.transform.Find("RequestDelete").GetComponent<Button>());
            if(management.activeSelf || !confirm.activeSelf)throw new Exception("Confirmation step missing.");
            var delete=confirm.transform.Find("ConfirmDelete").GetComponent<Button>();
            if(delete.onClick.GetPersistentMethodName(0)!="DeleteFiles")throw new Exception("Confirm not wired.");
            // Never execute the destructive callback during verification.
            Click(confirm.transform.Find("Cancel").GetComponent<Button>());
            if(confirm.activeSelf || !host.GetComponent<CanvasGroup>().interactable)throw new Exception("Cancel did not restore host.");
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                if(!text.font.name.Contains("Cinzel") && !text.font.name.Contains("Smooch"))
                    throw new Exception("Unexpected font: "+text.font.name);
            var started=(UnityEvent)typeof(LobbyUiManager).GetField("onHostStarted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            if(started.GetPersistentEventCount()==0)throw new Exception("Host continuation lost.");
            return "PASS: no-save guard, exclusive save selection, guarded Create events, management/confirmation/cancel, original fonts. No files deleted or hosts started.";
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
}
