using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;
public class InputBindings : MonoBehaviour
{
    public Controls _controls;
    public ActionToBind actionToBind;

    private const string RebindsKey = "rebinds";
    private RebindingOperation rebindingOperation;
    private InputAction inputAction;
    public enum ActionToBind
    {
        Movement = 0,
        Voice = 1,
        Jump = 2,

    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("Binding!");
            CallRebind();
        }
    }

    public void Save()
    {
        string rebinds = _controls.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(RebindsKey, rebinds);
        Debug.Log("Saved!");
    }
    public void CallRebind()
    {
        string rebinds = PlayerPrefs.GetString(RebindsKey, string.Empty);
        _controls = Movement._controls;
        _controls.LoadBindingOverridesFromJson(rebinds);
        CheckWhatToRebind();
        StartRebinding(inputAction);
    }
    private void CheckWhatToRebind()
    {
        switch (actionToBind)
        {
            case ActionToBind.Movement:
                inputAction = _controls.Gameplay.Movement; break;
            case ActionToBind.Jump:
                inputAction = _controls.Gameplay.Jump; break;
            case ActionToBind.Voice:
                inputAction = _controls.Gameplay.Voice; break;
        }
    }
    public void StartRebinding(InputAction rebindingAction)
    {
        rebindingAction.Disable();
        rebindingOperation = rebindingAction.PerformInteractiveRebinding()
            .WithControlsExcluding("Mouse")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => RebindComplete(rebindingAction))
            .Start();
    }
    private void RebindComplete(InputAction rebindingAction)
    {
        rebindingOperation.Dispose();

        rebindingAction.Enable();
        Debug.LogWarning(rebindingAction.GetBindingDisplayString(0));
        Save();
    }
}