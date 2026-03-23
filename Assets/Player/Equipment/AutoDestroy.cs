using System.Collections;
using UnityEngine;

namespace Player.Equipment
{
    public class AutoDestroy : MonoBehaviour
    {
        public IEnumerator DestroyInTime(float time)
        {
            Debug.Log($"[AutoDestroy] Will destroy in {time} seconds");
            yield return new WaitForSeconds(time);
            Debug.Log("[AutoDestroy] Destroying now");
            Destroy(gameObject);
        }
    }
}
