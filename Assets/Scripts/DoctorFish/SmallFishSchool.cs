using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// A school of small doctor fish. They swim in from the pool edge, hover
    /// around the feet and dart in for quick ticklish nibbles. Each nibble
    /// raises the Nibbled event so the audio controller can respond; the
    /// matching haptic loop (small_fish_nibble.json) runs independently.
    /// All positions are local to this component's transform, which sits at
    /// the centre of the pool floor.
    /// </summary>
    public class SmallFishSchool : MonoBehaviour
    {
        public int fishCount = 9;
        public float poolRadius = 0.42f;
        public float swimHeightMin = 0.08f;
        public float swimHeightMax = 0.24f;
        public float swimSpeed = 0.35f;
        public float nibbleDistance = 0.05f;

        public event Action<HapticLeg> Nibbled;

        Transform leftFoot;
        Transform rightFoot;

        enum FishMode { Hidden, Roaming, Nibbling, Leaving }

        class Fish
        {
            public Transform root;
            public Transform tailPivot;
            public FishMode mode;
            public Vector3 target;
            public float speed;
            public float phase;
            public float nibbleUntil;
            public float retargetAt;
        }

        readonly List<Fish> fish = new List<Fish>();
        bool nibblingEnabled;

        static readonly Color[] FishColors =
        {
            new Color(0.85f, 0.45f, 0.2f),   // warm orange
            new Color(0.7f, 0.72f, 0.75f),   // silver
            new Color(0.55f, 0.42f, 0.3f),   // sandy brown
            new Color(0.9f, 0.6f, 0.35f)     // light orange
        };

        public void Configure(Transform left, Transform right)
        {
            leftFoot = left;
            rightFoot = right;
        }

        void Start()
        {
            for (var i = 0; i < fishCount; i++)
            {
                var color = FishColors[i % FishColors.Length];
                var length = UnityEngine.Random.Range(0.055f, 0.085f);
                var root = CreatureBuilder.BuildFish($"SmallFish{i}",
                    transform, length, color);
                root.SetActive(false);
                fish.Add(new Fish
                {
                    root = root.transform,
                    tailPivot = root.transform.Find("TailPivot"),
                    mode = FishMode.Hidden,
                    speed = swimSpeed * UnityEngine.Random.Range(0.8f, 1.25f),
                    phase = UnityEngine.Random.value * 10f
                });
            }
        }

        /// <summary>Fish swim in from the pool edge and start roaming.</summary>
        public void Enter()
        {
            nibblingEnabled = false;
            foreach (var f in fish)
            {
                var angle = UnityEngine.Random.value * Mathf.PI * 2f;
                f.root.localPosition = new Vector3(
                    Mathf.Cos(angle) * poolRadius,
                    UnityEngine.Random.Range(swimHeightMin, swimHeightMax),
                    Mathf.Sin(angle) * poolRadius);
                f.root.gameObject.SetActive(true);
                f.mode = FishMode.Roaming;
                f.target = RandomPoint(false);
                f.retargetAt = Time.time + UnityEngine.Random.Range(1f, 3f);
            }
        }

        /// <summary>Bias the school towards the feet and let them nibble.</summary>
        public void StartNibbling()
        {
            nibblingEnabled = true;
        }

        public void Leave()
        {
            nibblingEnabled = false;
            foreach (var f in fish)
            {
                if (f.mode == FishMode.Hidden)
                    continue;
                var direction = f.root.localPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.forward;
                f.target = direction.normalized * (poolRadius * 1.05f)
                    + Vector3.up * swimHeightMin;
                f.mode = FishMode.Leaving;
            }
        }

        Vector3 RandomPoint(bool nearFeet)
        {
            if (nearFeet && leftFoot != null && rightFoot != null)
            {
                var foot = UnityEngine.Random.value < 0.5f ? leftFoot : rightFoot;
                var local = transform.InverseTransformPoint(foot.position);
                var offset = UnityEngine.Random.insideUnitSphere * 0.07f;
                local += offset;
                local.y = Mathf.Clamp(local.y, swimHeightMin, swimHeightMax);
                return local;
            }
            var point = UnityEngine.Random.insideUnitCircle * poolRadius * 0.85f;
            return new Vector3(point.x,
                UnityEngine.Random.Range(swimHeightMin, swimHeightMax), point.y);
        }

        void Update()
        {
            var now = Time.time;
            foreach (var f in fish)
            {
                if (f.mode == FishMode.Hidden)
                    continue;

                // Tail wiggle and a gentle bob make the primitives feel alive.
                if (f.tailPivot != null)
                    f.tailPivot.localRotation = Quaternion.Euler(
                        0f, Mathf.Sin(now * 9f + f.phase) * 28f, 0f);

                var position = f.root.localPosition;
                var toTarget = f.target - position;
                var arrived = toTarget.magnitude < nibbleDistance;

                if (f.mode == FishMode.Leaving)
                {
                    if (arrived)
                    {
                        f.root.gameObject.SetActive(false);
                        f.mode = FishMode.Hidden;
                        continue;
                    }
                }
                else if (f.mode == FishMode.Nibbling)
                {
                    // Quick trembling darts right at the skin.
                    f.root.localPosition = position + new Vector3(
                        Mathf.Sin(now * 30f + f.phase),
                        Mathf.Sin(now * 26f + f.phase * 2f),
                        Mathf.Cos(now * 28f + f.phase)) * 0.0015f;
                    if (now >= f.nibbleUntil)
                    {
                        f.mode = FishMode.Roaming;
                        f.target = RandomPoint(false);
                        f.retargetAt = now + UnityEngine.Random.Range(0.5f, 1.5f);
                    }
                    continue;
                }
                else if (arrived || now >= f.retargetAt)
                {
                    var wantsNibble = nibblingEnabled
                        && arrived
                        && IsNearFoot(position, out var leg)
                        && UnityEngine.Random.value < 0.8f;
                    if (wantsNibble)
                    {
                        f.mode = FishMode.Nibbling;
                        f.nibbleUntil = now + UnityEngine.Random.Range(0.6f, 1.4f);
                        Nibbled?.Invoke(leg);
                        continue;
                    }
                    f.target = RandomPoint(nibblingEnabled
                        && UnityEngine.Random.value < 0.75f);
                    f.retargetAt = now + UnityEngine.Random.Range(1.5f, 4f);
                }

                var step = f.speed * Time.deltaTime;
                f.root.localPosition = Vector3.MoveTowards(position, f.target, step);
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(toTarget.normalized);
                    f.root.localRotation = Quaternion.Slerp(
                        f.root.localRotation, look, Time.deltaTime * 4f);
                }
            }
        }

        bool IsNearFoot(Vector3 localPosition, out HapticLeg leg)
        {
            leg = HapticLeg.Left;
            if (leftFoot == null || rightFoot == null)
                return false;
            var world = transform.TransformPoint(localPosition);
            var toLeft = Vector3.Distance(world, leftFoot.position);
            var toRight = Vector3.Distance(world, rightFoot.position);
            leg = toLeft <= toRight ? HapticLeg.Left : HapticLeg.Right;
            return Mathf.Min(toLeft, toRight) < 0.16f;
        }
    }
}
