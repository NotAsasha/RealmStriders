using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

namespace Player.InventorySystem
{
    [RequireComponent(typeof(EntityDetector))]
    public class Scanner : Item
    {
        [SerializeField] TMP_Text danger;
        [SerializeField] Image freezeIcon;
        [SerializeField] Image waterIcon;


        [SerializeField] AudioClip toggleSound;
        [SerializeField] AudioClip idleSound;
        [SerializeField] AudioClip activeSound;


        NetworkVariable<bool> isOn = new(false, 0, 0);

        EntityDetector detector;

        private void Start()
        {
            detector = GetComponent<EntityDetector>();
            isOn.OnValueChanged += SwitchState;
        }

        #region Item Specific Functionality

        override protected void ExecuteItemAction(GameObject player)
        {
            SwitchStateServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SwitchStateServerRpc()
        {
            isOn.Value = !isOn.Value;
        }

        private void SwitchState(bool oldV, bool newV)
        {
            if (!newV) danger.text = " ";
            audioSource.clip = toggleSound;
            audioSource.Play();
        }

        float cooldown = 1f;
        float currTime = 0f;
        private void Update()
        {
            if (!isOn.Value) return;
            detector.DrawViewState();

            //peep once per second
            currTime += Time.deltaTime;
            if (currTime < cooldown) return;
            currTime = 0f;

            GameObject entityObj = detector.EntityInSight(true);
            if (entityObj)
            {

                Entity entity = entityObj.GetComponent<Entity>();

                danger.text = entity.GetHealth().ToString();

                freezeIcon.color = entity.IsEffectActive(EffectType.Freeze) ? Color.white : Color.black;
                waterIcon.color = entity.IsEffectActive(EffectType.Water) ? Color.white : Color.black;


                audioSource.clip = activeSound;
                cooldown = 0.75f;
                Debug.Log($"---Scanner: Entity danger: {entity.GetHealth()}");
            }
            else
            {
                freezeIcon.color = Color.black;
                waterIcon.color = Color.black;
                danger.text = "Not Found";

                audioSource.clip = idleSound;
                cooldown = 1f;
            }

            audioSource.Play();

            // - Particle effects
            // - Sound effects --- DONE
            // - Physics interactions with enemies --- DONE
            // - Cooldown mechanics --- DONE
        }

        #endregion
    }
}