using Oculus.Haptics;
using UnityEngine;
using UnityEngine.UI;

public class WorkshopHapticsSample : MonoBehaviour
{
    [SerializeField] private HapticClip[] hapticClips;
    [SerializeField] private HapticSourcePlayer hapticSourcePlayer;
    [SerializeField] private Text currentHapticsClipText;
    [SerializeField] private LocalizedHapticPlayer localizedHapticPlayer;
    [SerializeField] private Text currentLocationText;
    private int m_currentHapticSourceIndex = 0;

    private void Start()
    {
        hapticSourcePlayer.HapticSource.clip = hapticClips[m_currentHapticSourceIndex];
        UpdateClipText();
    }

    public void MoveToNextHapticClip()
    {
        m_currentHapticSourceIndex++;
        if (m_currentHapticSourceIndex >= hapticClips.Length)
        {
            m_currentHapticSourceIndex = 0;
        }
        UpdateHapticClip();
    }

    public void MoveToPreviousHapticClip()
    {
        m_currentHapticSourceIndex--;
        if (m_currentHapticSourceIndex < 0)
        {
            m_currentHapticSourceIndex = hapticClips.Length - 1;
        }
        UpdateHapticClip();
    }

    private void UpdateHapticClip()
    {
        hapticSourcePlayer.HapticSource.clip = hapticClips[m_currentHapticSourceIndex];
        // Displays the current pattern name on the Play button
        UpdateClipText();
    }

    public void PlaySelectedHapticClip()
    {
        hapticSourcePlayer.Play();
    }

    public void PlayLocalizedHaptic()
    {
        localizedHapticPlayer.Play();
    }

    public void MoveToNextLocation()
    {
        if (localizedHapticPlayer.HapticsLocation == OVRInput.HapticsLocation.Hand)
        {
            localizedHapticPlayer.HapticsLocation = OVRInput.HapticsLocation.Index;
        }
        else if (localizedHapticPlayer.HapticsLocation == OVRInput.HapticsLocation.Index)
        {
            localizedHapticPlayer.HapticsLocation = OVRInput.HapticsLocation.Thumb;
        }
        else
        {
            localizedHapticPlayer.HapticsLocation = OVRInput.HapticsLocation.Hand;
        }
        
        
        currentLocationText.text = $"Localized Haptic: {localizedHapticPlayer.HapticsLocation}";
    }

    private void UpdateClipText()
    {
        currentHapticsClipText.text = $".haptic Clip Player:\n Pattern: {hapticClips[m_currentHapticSourceIndex].name}";
    }
}
