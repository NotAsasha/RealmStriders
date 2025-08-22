using UnityEngine;
using Unity.Netcode;
using NUnit.Framework;
using System.Collections.Generic;
public class Base : NetworkBehaviour
{
    [SerializeField] List<ItemCard> cards;

    [SerializeField] NetworkObject radarTerminal;
    [SerializeField] NetworkObject radarButton;

    public NetworkVariable<int> RadarLevel = new(1);


}
