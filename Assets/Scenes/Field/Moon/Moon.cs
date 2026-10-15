using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(AudioSource))]
public class Moon : MonoBehaviour
{
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;

    public float soundPause = 0.9f;
    public float animSpeed = 1f;

    public AudioClip[] audioClips;
    public Sprite[] sprites;

    int currentSound = 0;
    float timePassed = 0.85f;


    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
    }

    void FixedUpdate()
    {
        if (timePassed > soundPause)
        {
            audioSource.PlayOneShot(audioClips[currentSound]);
            spriteRenderer.sprite = sprites[currentSound];


            currentSound = (currentSound + 1) % audioClips.Length;
            timePassed = 0f;
        }
        timePassed += Time.deltaTime;
    }
}
