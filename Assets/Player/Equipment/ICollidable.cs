using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public interface ICollidable
{

    void OnColliderEnter(GameObject _collider) { return; }
    void OnColliderExit(GameObject _collider) { return; }


}
