using UnityEngine;

namespace Player.Equipment
{
    public interface ICollidable
    {

        void OnColliderEnter(GameObject collider) { return; }
        void OnColliderExit(GameObject collider) { return; }


    }
}
