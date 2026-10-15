using Player;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace Portals
{
    public class PortalTeleporter : MonoBehaviour
    {
        private const float EnemyTeleportCooldown = 1f;
        private const float PlayerTeleportCooldown = 0.5f;
        private static readonly Dictionary<Enemy.Enemy, float> EnemyTeleportCooldowns = new();
        private static float playerTeleportCooldownUntil = 0f;

        public Transform receiver;
        public Transform enemyReceiver;

        private bool objectIsOverlapping = false;
        private Transform objectToTeleport;

        private void Awake()
        {
            if (TryGetComponent<BoxCollider>(out var boxCollider))
            {
                Vector3 size = boxCollider.size;
                if (size.y < 1f)
                {
                    size.y = 2.5f;
                    boxCollider.size = size;
                }
            }
        }

        private void Update()
        {
            CheckPlayerTeleport();
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTriggerEnter(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!objectIsOverlapping && other.CompareTag("Player") && other is CharacterController)
            {
                HandleTriggerEnter(other);
            }
            CheckPlayerTeleport();
        }

        private void HandleTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && other is CharacterController cc)
            {
                if (Time.time < playerTeleportCooldownUntil) return;

                NetworkObject netObj = cc.GetComponent<NetworkObject>();
                if (netObj != null && !netObj.IsOwner) return;

                Human human = cc.GetComponent<Human>();
                objectToTeleport = human != null ? human.transform : cc.transform;
                objectIsOverlapping = true;

                CheckPlayerTeleport();
            }
            else if (other.GetComponentInParent<Enemy.Enemy>() != null)
            {
                // Enemy movement and the shield decision are server-authoritative.
                if (PortalManager.Instance == null || !PortalManager.Instance.IsServer) return;
                if (!PortalManager.Instance.IsPortalOpen) return;
                if (PortalManager.Instance.IsMonsterPassBlockedServer) return;

                Enemy.Enemy enemy = other.GetComponentInParent<Enemy.Enemy>();
                if (EnemyTeleportCooldowns.TryGetValue(enemy, out float cooldownUntil))
                {
                    if (Time.time < cooldownUntil) return;

                    EnemyTeleportCooldowns.Remove(enemy);
                }

                EnemyTeleportCooldowns[enemy] = Time.time + EnemyTeleportCooldown;

                NavMeshAgent nma = enemy.GetComponent<NavMeshAgent>();
                if (nma != null) nma.enabled = false;

                if (enemy.TryGetComponent<NetworkTransform>(out var netTransform))
                {
                    netTransform.Teleport(enemyReceiver.position, enemyReceiver.rotation, enemy.transform.localScale);
                }
                else
                {
                    enemy.transform.position = enemyReceiver.position;
                }

                if (nma != null)
                {
                    nma.enabled = true;
                    nma.Warp(enemyReceiver.position);
                }

                enemy.Lure(enemyReceiver.position);
            }
        }

        private void CheckPlayerTeleport()
        {
            if (!objectIsOverlapping || !objectToTeleport) return;
            if (PortalManager.Instance != null && !PortalManager.Instance.IsPortalOpen) return;
            if (Time.time < playerTeleportCooldownUntil) return;

            Transform portalRoot = transform.parent;
            // Calculate position relative to the portal
            Vector3 portalToObject = objectToTeleport.position - portalRoot.position;

            // The portal faces -Z (visual), so its root forward (+Z) points INTO the portal.
            // Teleport when the player passes through the plane (dot product becomes positive).
            float dotProduct = Vector3.Dot(portalRoot.forward, portalToObject);

            if (dotProduct > 0f)
            {
                Debug.Log($"---Portal: Teleporting {objectToTeleport.name} to {receiver.name}");

                // Calculate rotation difference (180 degree flip to face out of the exit portal)
                Quaternion portalRotationDifference = receiver.rotation * Quaternion.Euler(0, 180, 0) * Quaternion.Inverse(portalRoot.rotation);
                Quaternion targetRotation = portalRotationDifference * objectToTeleport.rotation;

                // Flip the offset as well to place the player in front of the exit portal
                Vector3 localOffset = portalRoot.InverseTransformPoint(objectToTeleport.position);
                Vector3 mirrorOffset = new Vector3(-localOffset.x, localOffset.y, -localOffset.z);
                Vector3 targetPosition = receiver.TransformPoint(mirrorOffset);

                Human human = objectToTeleport.GetComponent<Human>();
                if (human != null)
                {
                    human.Teleport(targetPosition, targetRotation);
                }
                else
                {
                    CharacterController cc = objectToTeleport.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;

                    if (objectToTeleport.TryGetComponent<NetworkTransform>(out var netTransform))
                    {
                        netTransform.Teleport(targetPosition, targetRotation, objectToTeleport.localScale);
                    }
                    else
                    {
                        objectToTeleport.SetPositionAndRotation(targetPosition, targetRotation);
                    }

                    if (cc != null) cc.enabled = true;
                }

                playerTeleportCooldownUntil = Time.time + PlayerTeleportCooldown;

                // Switch active cameras via manager
                PortalManager.Instance.SwitchCameras();

                objectIsOverlapping = false;
                objectToTeleport = null;

                PortalManager.Instance.CallOnTeleport(human);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // ONLY reset if the main player CharacterController exits the trigger!
            // Child colliders (arms, legs, ragdoll, items) exiting must NOT cancel overlap.
            if (objectToTeleport != null && other is CharacterController cc && cc.transform == objectToTeleport)
            {
                //Debug.Log($"---{name}: {other.gameObject.name} exited.");
                objectIsOverlapping = false;
                objectToTeleport = null;
            }
        }
    }
}
