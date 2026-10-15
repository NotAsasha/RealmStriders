using Base.Billiard;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

namespace Base.Billiard
{
    public class Billiard : NetworkBehaviour
    {
        public static List<BilliardBall> Balls = new();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CallRespawnBallsRpc()
        {
            RespawnBalls();
        }

        public void RespawnBalls()
        {
            if (!IsServer) return;
            foreach (var b in Balls)
            {
                b.ResetToSpawnServer();
            }
        }
    }
}
