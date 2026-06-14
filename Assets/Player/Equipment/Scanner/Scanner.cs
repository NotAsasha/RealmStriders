using Enemy;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace Player.Equipment.Scanner
{
    [RequireComponent(typeof(EntityDetector))]
    public class Scanner : Item
    {
        [SerializeField] TMP_Text danger;
        [SerializeField] Image freezeIcon;
        [SerializeField] Image waterIcon;


        [SerializeField] AudioClip toggleSound;
        [SerializeField] AudioClip scanSound;
        [SerializeField] [MinMax(0f, 1f)] float pitchDiff = 0.2f;


        NetworkVariable<bool> isOn = new(false, 0, 0);

        EntityDetector detector;
        private float basePitch = 1f;
        private void Start()
        {
            detector = GetComponent<EntityDetector>();
            basePitch = audioSource.pitch;
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
            audioSource.pitch = basePitch;
            audioSource.PlayOneShot(toggleSound);
            audioSource.clip = scanSound;
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


                audioSource.pitch = basePitch + pitchDiff;
                cooldown = 0.75f;
                Debug.Log($"---Scanner: Entity danger: {entity.GetHealth()}");
            }
            else
            {
                freezeIcon.color = Color.black;
                waterIcon.color = Color.black;
                danger.text = "Not Found";

                audioSource.pitch = basePitch;
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