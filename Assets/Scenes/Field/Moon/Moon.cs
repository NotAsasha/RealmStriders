using UnityEngine;

[RequireComponent(typeof(Animator), typeof(AudioSource))]
public class Moon : MonoBehaviour
{
    private Animator animator;
    private AudioSource audioSource;

    public float soundPause = 0.9f;
    public float animSpeed = 1f;

    public AudioClip[] audioClips;
    void Start()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    int currentSound = 0;
    float timePassed = 0.85f;
    void Update()
    {
        //Amination
        animator.speed = animSpeed;



        //Sound
        if (timePassed > soundPause)
        {
            audioSource.clip = audioClips[currentSound];
            audioSource.Play();

            currentSound = (currentSound + 1) % audioClips.Length;
            timePassed = 0f;
        }
        timePassed += Time.deltaTime;
    }
}
