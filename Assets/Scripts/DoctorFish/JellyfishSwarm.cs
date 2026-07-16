using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// A few translucent jellyfish that pulse and drift past the legs. When
    /// one brushes a foot it flashes and raises Stung so the jellyfish_sting
    /// haptic and the electric sound can fire on the matching leg.
    /// Positions are local to this component's transform at the pool centre.
    /// </summary>
    public class JellyfishSwarm : MonoBehaviour
    {
        public int jellyCount = 3;
        public float poolRadius = 0.42f;
        public float driftSpeed = 0.09f;
        public float stingDistance = 0.11f;
        public float stingCooldown = 5f;

        public event Action<HapticLeg> Stung;

        Transform leftFoot;
        Transform rightFoot;

        class Jelly
        {
            public Transform root;
            public Transform bell;
            public Vector3 baseBellScale;
            public Material bellMaterial;
            public Color baseEmission;
            public Vector3 target;
            public float phase;
            public float nextStingAllowed;
            public float flashUntil;
            public bool active;
        }

        readonly List<Jelly> jellies = new List<Jelly>();
        bool entered;

        public void Configure(Transform left, Transform right)
        {
            leftFoot = left;
            rightFoot = right;
        }

        void Start()
        {
            var tints = new[]
            {
                new Color(0.85f, 0.5f, 0.9f, 0.4f),
                new Color(0.55f, 0.6f, 0.95f, 0.4f),
                new Color(0.95f, 0.55f, 0.7f, 0.4f)
            };
            var glows = new[]
            {
                new Color(0.45f, 0.12f, 0.55f),
                new Color(0.15f, 0.2f, 0.6f),
                new Color(0.55f, 0.15f, 0.3f)
            };
            for (var i = 0; i < jellyCount; i++)
            {
                var tint = tints[i % tints.Length];
                var glow = glows[i % glows.Length];
                var radius = UnityEngine.Random.Range(0.05f, 0.075f);
                var root = CreatureBuilder.BuildJellyfish($"Jellyfish{i}",
                    transform, radius, tint, glow, 5);
                root.SetActive(false);
                var bell = root.transform.Find("Bell");
                jellies.Add(new Jelly
                {
                    root = root.transform,
                    bell = bell,
                    baseBellScale = bell.localScale,
                    bellMaterial = bell.GetComponent<Renderer>().sharedMaterial,
                    baseEmission = glow,
                    phase = UnityEngine.Random.value * 10f
                });
            }
        }

        public void Enter()
        {
            entered = true;
            var index = 0;
            foreach (var jelly in jellies)
            {
                var angle = index * Mathf.PI * 2f / jellies.Count
                    + UnityEngine.Random.value;
                jelly.root.localPosition = new Vector3(
                    Mathf.Cos(angle) * poolRadius,
                    UnityEngine.Random.Range(0.12f, 0.24f),
                    Mathf.Sin(angle) * poolRadius);
                jelly.root.gameObject.SetActive(true);
                jelly.active = true;
                jelly.target = NextDriftTarget();
                jelly.nextStingAllowed = Time.time
                    + UnityEngine.Random.Range(2f, 5f);
                index++;
            }
        }

        public void Leave()
        {
            entered = false;
            foreach (var jelly in jellies)
            {
                if (!jelly.active)
                    continue;
                var direction = jelly.root.localPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.001f)
                    direction = Vector3.forward;
                jelly.target = direction.normalized * (poolRadius * 1.05f)
                    + Vector3.up * 0.18f;
            }
        }

        Vector3 NextDriftTarget()
        {
            // Drift paths pass close to the legs more often than not.
            if (UnityEngine.Random.value < 0.6f
                && leftFoot != null && rightFoot != null)
            {
                var foot = UnityEngine.Random.value < 0.5f ? leftFoot : rightFoot;
                var local = transform.InverseTransformPoint(foot.position);
                local += UnityEngine.Random.insideUnitSphere * 0.08f;
                local.y = Mathf.Clamp(local.y + 0.05f, 0.1f, 0.24f);
                return local;
            }
            var point = UnityEngine.Random.insideUnitCircle * poolRadius * 0.8f;
            return new Vector3(point.x,
                UnityEngine.Random.Range(0.12f, 0.24f), point.y);
        }

        void Update()
        {
            var now = Time.time;
            foreach (var jelly in jellies)
            {
                if (!jelly.active)
                    continue;

                // Bell pulse: slow contraction with a small upward push.
                var pulse = Mathf.Sin(now * 2.2f + jelly.phase);
                if (jelly.bell != null)
                    jelly.bell.localScale = Vector3.Scale(jelly.baseBellScale,
                        new Vector3(1f - pulse * 0.08f, 1f + pulse * 0.16f,
                            1f - pulse * 0.08f));

                Drift(jelly, now, pulse);
                UpdateTentacles(jelly, now);
                UpdateFlash(jelly, now);
                TrySting(jelly, now);
            }
        }

        void Drift(Jelly jelly, float now, float pulse)
        {
            var position = jelly.root.localPosition;
            var speed = driftSpeed * (0.7f + Mathf.Max(0f, pulse) * 0.8f);
            jelly.root.localPosition = Vector3.MoveTowards(
                position, jelly.target, speed * Time.deltaTime);
            if (Vector3.Distance(position, jelly.target) < 0.03f)
            {
                if (!entered)
                {
                    jelly.root.gameObject.SetActive(false);
                    jelly.active = false;
                    return;
                }
                jelly.target = NextDriftTarget();
            }
        }

        void UpdateTentacles(Jelly jelly, float now)
        {
            foreach (Transform child in jelly.root)
            {
                if (!child.name.StartsWith("Tentacle"))
                    continue;
                var line = child.GetComponent<LineRenderer>();
                if (line == null)
                    continue;
                for (var p = 0; p < line.positionCount; p++)
                {
                    var t = p / (float)(line.positionCount - 1);
                    line.SetPosition(p, new Vector3(
                        Mathf.Sin(now * 1.6f + jelly.phase + t * 3f) * 0.02f * t,
                        -t * 0.12f,
                        Mathf.Cos(now * 1.3f + jelly.phase + t * 2f) * 0.02f * t));
                }
            }
        }

        void UpdateFlash(Jelly jelly, float now)
        {
            if (jelly.bellMaterial == null)
                return;
            var boost = now < jelly.flashUntil
                ? Mathf.Lerp(4f, 1f, 1f - (jelly.flashUntil - now) / 0.4f)
                : 1f;
            jelly.bellMaterial.SetColor("_EmissionColor",
                jelly.baseEmission * boost);
        }

        void TrySting(Jelly jelly, float now)
        {
            if (!entered || now < jelly.nextStingAllowed)
                return;
            if (leftFoot == null || rightFoot == null)
                return;
            var world = jelly.root.position;
            var toLeft = Vector3.Distance(world, leftFoot.position);
            var toRight = Vector3.Distance(world, rightFoot.position);
            if (Mathf.Min(toLeft, toRight) > stingDistance)
                return;
            jelly.nextStingAllowed = now + stingCooldown
                + UnityEngine.Random.Range(0f, 3f);
            jelly.flashUntil = now + 0.4f;
            Stung?.Invoke(toLeft <= toRight ? HapticLeg.Left : HapticLeg.Right);
        }
    }
}
