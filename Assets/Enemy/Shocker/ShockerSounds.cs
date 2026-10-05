using UnityEngine;

// Note: the namespace "Enemy.Shocker" and the class "Shocker" share the same name,
// so we use the fully-qualified type to avoid ambiguity.
using ShockerEnemy = Enemy.Shocker.Shocker;

/// <summary>
/// Plays audio cues for the Shocker enemy's two key moments:
///   - Charge-up: a build-up buzz while the discharge is warming up.
///   - Discharge: a sharp electric burst played the instant the AOE fires.
/// Follows the same pattern as CasinoSounds — pure audio, no game logic.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ShockerSounds : EnemySounds
{
    [Header("Shocker Sounds")]
    public AudioClip chargeSound;
    public AudioClip dischargeSound;

    [Header("Shocker Radii")]
    public float chargeRadius = 20f;
    public float dischargeRadius = 30f;

    private ShockerEnemy _shocker;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (enemy is ShockerEnemy shocker)
        {
            _shocker = shocker;
            _shocker.OnChargeStarted += PlayChargeSound;
            _shocker.OnDischarged   += PlayDischargeSound;
        }
    }

    protected override void OnDisable()
    {
        if (_shocker != null)
        {
            _shocker.OnChargeStarted -= PlayChargeSound;
            _shocker.OnDischarged   -= PlayDischargeSound;
            _shocker = null;
        }
        base.OnDisable();
    }

    private void PlayChargeSound()
    {
        if (chargeSound == null) return;
        SetRadius(chargeRadius);
        source.PlayOneShot(chargeSound);
    }

    private void PlayDischargeSound()
    {
        if (dischargeSound == null) return;
        SetRadius(dischargeRadius);
        source.PlayOneShot(dischargeSound);
    }
}
