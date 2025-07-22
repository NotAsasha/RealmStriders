using UnityEngine;
using Unity.Netcode;
using System;
using Player;
using UnityEngine.InputSystem;
public class Inventory : NetworkBehaviour
    //In multiplayer:
    //SetActive does not synchronize
    //parent changing not working on client
{
    public int capacity = 4;
    [SerializeField] Vector3 handPosition;
    public Transform userInterface;
    [SerializeField] GameObject slotPrefab;

    public ITakable[] items;
    public UISlot[] slots;
    public int activeSlotIndex;


    private Controls controls;
    void Start()
    {
        items = new ITakable[capacity];
        slots = new UISlot[capacity];
        controls = Movement.instance._controls;
        controls.Gameplay.MouseWheel.performed += ChangeSlot;
        controls.Gameplay.Use.performed += UseItem;
        controls.Gameplay.Drop.performed += DropItem;

        UpdateUI();
    }
    public override void OnDestroy()
    {
        controls.Gameplay.MouseWheel.performed -= ChangeSlot;
        controls.Gameplay.Use.performed -= UseItem;
        controls.Gameplay.Drop.performed -= DropItem;
    }
    public void AddItem(GameObject _toAdd, int _slot)
    {
        if (items[_slot] != null) return;

        items[_slot] = _toAdd.GetComponent<ITakable>();
        _toAdd.transform.parent = gameObject.transform;
        _toAdd.transform.localPosition = handPosition;
        _toAdd.transform.localEulerAngles = new Vector3(0,0,0);

        UpdateUI();
    }
    public void DropItem(InputAction.CallbackContext obj)
    {
        if (Movement.instance.isInInteraction) return;

        ITakable toDrop = items[activeSlotIndex];
        if (toDrop == null) return;
        GameObject toDropObject = toDrop.GetGameObject();
        items[activeSlotIndex] = null;
        toDropObject.transform.parent = null;
        toDropObject.SetActive(true);
        toDrop.Drop(gameObject);

        UpdateUI();
    }
    void ChangeSlot(InputAction.CallbackContext obj)
    {
        if (Movement.instance.isInInteraction) return;

        //Read controls
        float scrool = controls.Gameplay.MouseWheel.ReadValue<float>();
        if (scrool == 0) return;

        //Checks for out of bounds
        int diff = activeSlotIndex;
        diff = scrool > 0 ? diff += 1 : diff -= 1;
        if (diff >= capacity || diff < 0) return;


        //Actual change
        items[activeSlotIndex]?.GetGameObject().SetActive(false);
        activeSlotIndex = diff;
        items[activeSlotIndex]?.GetGameObject().SetActive(true);

        UpdateUI();

    }

    void UseItem(InputAction.CallbackContext obj) {
        if (Movement.instance.isInInteraction) return;

        items[activeSlotIndex]?.Use(gameObject);
    }

    void UpdateUI()
    {
        if (userInterface.childCount > capacity)
        {
            throw new OverflowException("More UI slots than inventory can process");
        }
        if (userInterface.childCount == 0) CreateUISlots();


        for (int i = 0; i < capacity; i++)
        {
            slots[i].UpdateUI(items[i] != null, i == activeSlotIndex);
        }
    }

    void CreateUISlots()
    {
        for (int i = 0; i < capacity; i++)
        {
            slots[i] = Instantiate(slotPrefab, userInterface).GetComponent<UISlot>();
        }
    }
}
