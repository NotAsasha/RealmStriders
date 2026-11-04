using Player;
using UnityEngine;

namespace Game.Tutorial
{
    // Production capture and effects without enemy AI or a NavMesh dependency.
    public sealed class TutorialDummy : Entity
    {
        [SerializeField] private Collider hitCollider;
        protected override void BeginCaptureLockServer() => hitCollider.enabled = false;
    }
}
