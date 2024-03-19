using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public Controls _controls;
    private const string RebindsKey = "rebinds";

    void Start()
    {
        _controls = new Controls();
        LoadBindingsFromPlayerPrefs();
        _controls.Enable();
    }
    public void LoadBindingsFromPlayerPrefs()
    {
        string rebinds = PlayerPrefs.GetString(RebindsKey, string.Empty);
        if (!string.IsNullOrEmpty(rebinds))
        {
            _controls.LoadBindingOverridesFromJson(rebinds);
            Debug.Log("Bindings loaded!");
        }
    }
    public void SaveBindingsToPlayerPrefs()
    {
        string rebinds = _controls.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(RebindsKey, rebinds);
        Debug.Log("Bindings saved!");
        
    }
}
