using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Player
{
    public class StartAnimation : MonoBehaviour
    {
        private Camera mCamera;
        [SerializeField] TextMeshPro text;
        [SerializeField] Image image;
        void Start()
        {
            StartCoroutine(StartAnim());
        }


        private IEnumerator StartAnim()
        {
            yield return new WaitForEndOfFrame();
        }

        void Update()
        {
        
        }
    }
}
