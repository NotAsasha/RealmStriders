using UnityEngine;
using UnityEngine.AI;

public class PortalTeleporter : MonoBehaviour
{
    public Transform receiver;
    public Transform enemyReceiver;


    private bool objectIsOverlapping = false;
    private Transform objectToTeleport;

    private void Update()
    {
        if (objectIsOverlapping && objectToTeleport != null)
        {
            Vector3 portalToObject = objectToTeleport.position - transform.position;
            float dotProduct = Vector3.Dot(transform.up, portalToObject);
            if (dotProduct < 0f)
            {
                Debug.Log($"---Portal: Teleporting {objectToTeleport.name} to {receiver.name}");

                //Quaternion portalRotationDifference = receiver.rotation * Quaternion.Inverse(transform.rotation);

                CharacterController cc = objectToTeleport.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                //NavMeshAgent nma = objectToTeleport.GetComponent<NavMeshAgent>();
                //if (nma != null) nma.enabled = false;


                //objectToTeleport.rotation = portalRotationDifference * objectToTeleport.rotation;
                objectToTeleport.position = receiver.position + /* portalRotationDifference * */ portalToObject;

                if (cc != null) cc.enabled = true;
                //if (nma != null) nma.enabled = true;

                objectIsOverlapping = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            objectToTeleport = other.transform;
            objectIsOverlapping = true;
            //Debug.Log($"---{name}: {other.gameObject.name} entered.");
        }
        else if (other.gameObject.GetComponent<Enemy>() != null)
        {
            if (other.TryGetComponent<NavMeshAgent>(out var nma)) nma.enabled = false;
            other.transform.position = enemyReceiver.position;
            if (nma != null) nma.enabled = true;

            if (other.TryGetComponent<Enemy>(out var en)) en.Lure(enemyReceiver.position);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform == objectToTeleport)
        {
            //Debug.Log($"---{name}: {other.gameObject.name} exited.");
            objectIsOverlapping = false;
            objectToTeleport = null;
        }
    }
}
