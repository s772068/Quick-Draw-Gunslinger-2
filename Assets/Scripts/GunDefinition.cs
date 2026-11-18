using UnityEngine;

/// <summary>
/// Player crosshair movement for one gun. Lives on a child of the Guns root.
/// </summary>
public class GunDefinition : MonoBehaviour
{
    [Header("Aim path")]
    [Tooltip("Radius of the crosshair path around the enemy (UI units).")]
    public float aimRadius = 300f;

    [Tooltip("Random time from Pull until the crosshair first reaches MiddlePoint. X = min, Y = max.")]
    public Vector2 timeToMiddleRange = new Vector2(2f, 3f);

    [Tooltip("Seconds for the crosshair to travel from start to the enemy on first aim.")]
    public float aimDuration = 1f;

    [Tooltip("Seconds for the crosshair to travel after a player miss re-pull.")]
    public float aimAfterMissDuration = 0.5f;

    [Header("Crossfire shake")]
    [Tooltip("How far the crosshair wobbles sideways from the path (UI units).")]
    public float crossfireShakeIntensity = 50f;

    [Tooltip("How many full side-to-side cycles per second.")]
    public float crossfireShakeFrequency = 3f;

    [Tooltip("0 = shake stays full strength until the end. 1 = shake fades to ~0 by the time the crosshair reaches the target.")]
    [Range(0f, 1f)] public float crossfireShakeFadeByEnd = 0.35f;

    [Header("Miss bounce")]
    [Tooltip("How far the crosshair bounces up on a player miss (UI units).")]
    public float missBounceUp = 40f;

    [Tooltip("Duration of the miss bounce-up (seconds).")]
    public float missBounceDuration = 0.12f;

    [Tooltip("Duration returning from the miss bounce to the path (seconds).")]
    public float missToMidDuration = 0.25f;
}
