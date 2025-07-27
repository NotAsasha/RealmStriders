using System.Collections;
using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public IEnumerator DestroyInTime(float _time)
    {
        Debug.Log($"[AutoDestroy] Will destroy in {_time} seconds");
        yield return new WaitForSeconds(_time);
        Debug.Log("[AutoDestroy] Destroying now");
        Destroy(gameObject);
    }
}
