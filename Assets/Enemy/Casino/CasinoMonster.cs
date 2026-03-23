using System.Collections;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

namespace Enemy.Casino
{
    public class CasinoMonster : Enemy
    {
        [SerializeField] private bool isStatic;

        [SerializeField] private float legSpacing = 3.5f;
        [SerializeField] private float legRotation = 60f;
        [SerializeField] private float stepLength = 2f;
        [SerializeField] private float speed = 4f;

        [SerializeField] private Animator casinoAnimator;
        //[SerializeField] private Rig rig;

        //public float legHeight;

        [SerializeField] private LayerMask ground;

        //All the legs (0 - Front Left, 1 - Front Right, 2 - Back Left, 3 - Back Right)
        [SerializeField] private ChainIKConstraint[] ikComponents = new ChainIKConstraint[4];
        [SerializeField] private Transform centralBody;

        private Transform[] legsTargets = new Transform[4];
        private Vector3[] currentTargets = new Vector3[4];

    

        private bool isSpawned;

        protected override void Start()
        {
            animator = casinoAnimator;
            base.Start();
            animator.speed = 0f;
            //rig.weight = 0f;

            if (!isStatic)
            {
                isSpawned = true;
                StartCoroutine(WakeAnimation());
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && isStatic)
            {
                effects[EffectType.Freeze].Value = true;
                effects[EffectType.Invincible].Value = true;
            }
        
        }

        [ClientRpc]
        public void WakeUpClientRpc()
        {
            if (isSpawned)
            {
                Debug.LogError($"---CasinoMonster: Trying to play animation on active monster.");
                return;
            }
            isSpawned = true;
            StartCoroutine(WakeAnimation());
        }
        private IEnumerator WakeAnimation()
        {
            Debug.Log($"---CasinoMonster: Waking up");
            animator.speed = 1f;
            //rig.weight = 1f;
    
            yield return new WaitForSeconds(4f);

            for (int i = 0; i < legsTargets.Length; i++)
            {
                GameObject newTarget = new GameObject($"Target{i}");
                newTarget.transform.position = ikComponents[i].data.tip.position;

                var data = ikComponents[i].data;
                data.target = newTarget.transform;
                ikComponents[i].data = data;

                legsTargets[i] = newTarget.transform;
                currentTargets[i] = newTarget.transform.position;
            }
            GetComponentInChildren<RigBuilder>().Build();

            if (IsServer)
            {
                effects[EffectType.Freeze].Value = false;
                effects[EffectType.Invincible].Value = false;
            }
        }



        private void LateUpdate()
        {
            if (isDead.Value || IsEffectActive(EffectType.Freeze)) return;
            UpdateLegPosition();
            UpdateBodyPosition();
        }
        private float[] lerp = { 1f, 1f };
        private void UpdateLegPosition()
        {
            float[] legsRotation = {
                //First Pair
                transform.eulerAngles.y + legRotation,
                transform.eulerAngles.y + 180 + legRotation,

                //Second Pair
                transform.eulerAngles.y - legRotation,
                transform.eulerAngles.y + 180 - legRotation
            };


            for (int i = 0; i < 2; i++)
            {
                int otherGroup = (i + 1) % 2;
                if (lerp[otherGroup] < 1) continue;


                int firstLeg = i * 2;
                int secondLeg = i * 2 + 1;


                if (lerp[i] >= 1)
                {
                    Vector3 target1 = RaycastToTarget(CalculateTargetOffset(legsRotation[firstLeg], legSpacing));
                    Vector3 target2 = RaycastToTarget(CalculateTargetOffset(legsRotation[secondLeg], legSpacing));

                    if (Vector3.Distance(target1, currentTargets[firstLeg]) > stepLength ||
                        Vector3.Distance(target2, currentTargets[secondLeg]) > stepLength)
                    {
                        lerp[i] = 0; 
                        currentTargets[firstLeg] = target1;
                        currentTargets[secondLeg] = target2;
                    }
                }
                if (lerp[i] < 1)
                {
                    lerp[i] += Time.deltaTime * speed;
                    MoveLeg(firstLeg, i);
                    MoveLeg(secondLeg, i);
                }
            }
        }
        void MoveLeg(int legIdx, int groupIdx)
        {
            Vector3 tempPos = Vector3.Lerp(legsTargets[legIdx].position, currentTargets[legIdx], lerp[groupIdx]);
            tempPos.y += Mathf.Sin(Mathf.PI * lerp[groupIdx]) * 0.2f;
            legsTargets[legIdx].position = tempPos;
        }


        private void UpdateBodyPosition()
        {
            float sum = 0;
            foreach (Transform t in legsTargets) {
                sum += t.position.y;
            }
            float bodyHeight = Mathf.Lerp(centralBody.position.y, (sum / 4) + 1f, Time.deltaTime);
            centralBody.position = new Vector3(centralBody.position.x, bodyHeight, centralBody.position.z);
        }


        private IEnumerator MoveLeg(int legIndex)
        {
            yield return new WaitForSeconds(1f);
        }

        private Vector3 CalculateTargetOffset(float rotation, float legOffset)
        {
            return transform.position + new Vector3(
                Mathf.Sin(rotation * Mathf.Deg2Rad) * legOffset, 0f,
                Mathf.Cos((rotation) * Mathf.Deg2Rad) * legOffset);
        }

        private Vector3 RaycastToTarget(Vector3 target)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit navHit, stepLength * 3, 1 << 0))
            {
                return navHit.position;
            }
            if (Physics.Raycast(target, Vector3.down, out var hit, stepLength * 2, ground))
            {
                return hit.point;
            }
            return target;
        
        }

        private void OnDrawGizmosSelected()
        {
            float[] legsRotation = {
                transform.eulerAngles.y + legRotation,
                transform.eulerAngles.y + 180 - legRotation,

                transform.eulerAngles.y - legRotation,
                transform.eulerAngles.y + 180 + legRotation
            
            };

            for (int i = 0; i < legsTargets.Length; i++)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(CalculateTargetOffset(legsRotation[i], legSpacing), 0.2f);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(RaycastToTarget(CalculateTargetOffset(legsRotation[i], legSpacing)), 0.2f);

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(currentTargets[i], 0.2f);
            }
        }
    }
}
