using System.Collections;
using System.Diagnostics.Tracing;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class CasinoMonster : Enemy
{
    void Start()
    {
        isFreezed.Value = true;
        //isInvincible.Value = true;
    }

    [ClientRpc]
    public void WakeUpClientRpc()
    {
        StartCoroutine(WakeAnimation());
    }

    
    private IEnumerator WakeAnimation()
    {
        yield return new WaitForSeconds(1f);
        if (IsServer)
        {
            isFreezed.Value = false;
            //isInvincible.Value = false;
        }
    }

}
