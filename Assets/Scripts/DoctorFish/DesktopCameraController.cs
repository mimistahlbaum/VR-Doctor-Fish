using UnityEngine;
using UnityEngine.InputSystem;

namespace DoctorFish
{
    /// <summary>
    /// Fallback look controls for running the scene without a headset:
    /// hold the right mouse button and drag to look around from the seat.
    /// The bootstrap only enables this when no XR device is active.
    /// </summary>
    public class DesktopCameraController : MonoBehaviour
    {
        public float sensitivity = 0.15f;

        float yaw;
        float pitch = 30f;

        void Start()
        {
            var euler = transform.localEulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            Apply();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed)
                return;
            var delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -60f, 85f);
            Apply();
        }

        void Apply()
        {
            transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
