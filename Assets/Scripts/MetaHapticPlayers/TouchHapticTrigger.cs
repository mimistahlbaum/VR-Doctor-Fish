using UnityEngine;

/// <summary>
/// Optional helper for playing any haptic player when this object is touched.
/// Works with trigger colliders and regular collisions.
/// </summary>
[AddComponentMenu("Haptics/Touch Haptic Trigger")]
public class TouchHapticTrigger : MonoBehaviour
{
    [Header("Haptic")]
    [Tooltip("The haptic player to call when this object is touched.")]
    [SerializeField] private HapticPlayerBase hapticPlayer;

    [Header("Touch Rules")]
    [Tooltip("Only objects on these layers can trigger the haptic.")]
    [SerializeField] private LayerMask touchLayers = ~0;

    [Tooltip("Play only the first time this object is touched.")]
    [SerializeField] private bool playOnlyOnce = false;

    [Tooltip("Stop the haptic when the touching object exits.")]
    [SerializeField] private bool stopOnExit = false;


    private bool m_hasPlayed;

    private void Reset()
    {
        hapticPlayer = GetComponent<HapticPlayerBase>();
    }

    public void PlayHaptic()
    {
        if (playOnlyOnce && m_hasPlayed)
        {
            return;
        }

        if (hapticPlayer == null)
        {
            Debug.LogWarning("[TouchHapticTrigger] No haptic player assigned.", this);
            return;
        }

        m_hasPlayed = true;
        hapticPlayer.Play();
    }

    public void ResetPlayOnce()
    {
        m_hasPlayed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (CanReactTo(other.gameObject))
        {
            PlayHaptic();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (stopOnExit && CanReactTo(other.gameObject))
        {
            hapticPlayer?.Stop();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (CanReactTo(collision.gameObject))
        {
            PlayHaptic();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (stopOnExit && CanReactTo(collision.gameObject))
        {
            hapticPlayer?.Stop();
        }
    }

    private bool CanReactTo(GameObject touchingObject)
    {
        int touchingLayer = 1 << touchingObject.layer;
        return (touchLayers.value & touchingLayer) != 0;
    }
}
