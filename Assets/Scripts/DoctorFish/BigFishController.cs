using System;
using System.Collections;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// The big fish. It circles the feet once, lines up on one foot, lunges
    /// in for a strong bite (raising Bit for audio and the big_fish_bite
    /// haptic), shakes, then retreats to the edge of the pool.
    /// Positions are local to this component's transform at the pool centre.
    /// </summary>
    public class BigFishController : MonoBehaviour
    {
        public float bodyLength = 0.3f;
        public float poolRadius = 0.42f;
        public float swimHeight = 0.16f;
        public float circleSeconds = 7f;
        public float lungeSeconds = 0.45f;

        public event Action<HapticLeg> Bit;

        Transform fishRoot;
        Transform tailPivot;
        Transform leftFoot;
        Transform rightFoot;
        Coroutine choreography;

        public void Configure(Transform left, Transform right)
        {
            leftFoot = left;
            rightFoot = right;
        }

        void Start()
        {
            var root = CreatureBuilder.BuildFish("BigFish", transform,
                bodyLength, new Color(0.42f, 0.5f, 0.58f));
            fishRoot = root.transform;
            tailPivot = fishRoot.Find("TailPivot");
            root.SetActive(false);
        }

        /// <summary>Run the full approach, bite and retreat choreography.</summary>
        public void BeginEncounter()
        {
            if (choreography != null)
                StopCoroutine(choreography);
            choreography = StartCoroutine(Encounter());
        }

        public void Retreat()
        {
            if (choreography != null)
            {
                StopCoroutine(choreography);
                choreography = null;
            }
            if (fishRoot != null && fishRoot.gameObject.activeSelf)
                StartCoroutine(SwimOut());
        }

        IEnumerator Encounter()
        {
            var legs = new[] { HapticLeg.Left, HapticLeg.Right };
            var firstLeg = legs[UnityEngine.Random.Range(0, legs.Length)];
            fishRoot.gameObject.SetActive(true);

            // Slide in from the far edge of the pool.
            var startAngle = Mathf.PI * 0.5f;
            fishRoot.localPosition = AngleToPosition(startAngle, poolRadius);

            // One slow curious circle around the feet.
            var circleRadius = poolRadius * 0.62f;
            var elapsed = 0f;
            while (elapsed < circleSeconds)
            {
                elapsed += Time.deltaTime;
                var angle = startAngle + elapsed / circleSeconds * Mathf.PI * 2.2f;
                MoveAlong(AngleToPosition(angle, circleRadius), 6f);
                yield return null;
            }

            yield return Lunge(firstLeg);
            yield return Hold(1.6f);

            // A second, quicker pass at the other foot.
            yield return Lunge(firstLeg == HapticLeg.Left
                ? HapticLeg.Right : HapticLeg.Left);
            yield return Hold(1f);

            yield return SwimOut();
            choreography = null;
        }

        IEnumerator Lunge(HapticLeg leg)
        {
            var foot = leg == HapticLeg.Left ? leftFoot : rightFoot;
            if (foot == null)
                yield break;
            var target = transform.InverseTransformPoint(foot.position);
            target.y = Mathf.Max(0.06f, target.y - 0.02f);

            // Line up a bite length away, pause, then strike.
            var lineUp = target + (fishRoot.localPosition - target).normalized
                * bodyLength * 1.2f;
            var settle = 0f;
            while (settle < 1.2f)
            {
                settle += Time.deltaTime;
                MoveAlong(lineUp, 5f);
                yield return null;
            }

            var start = fishRoot.localPosition;
            var strike = 0f;
            while (strike < lungeSeconds)
            {
                strike += Time.deltaTime;
                var k = Mathf.SmoothStep(0f, 1f, strike / lungeSeconds);
                fishRoot.localPosition = Vector3.Lerp(start, target, k);
                FaceTowards(target, 20f);
                yield return null;
            }

            Bit?.Invoke(leg);

            // Head shake while it holds the bite.
            var shake = 0f;
            var baseRotation = fishRoot.localRotation;
            while (shake < 0.7f)
            {
                shake += Time.deltaTime;
                fishRoot.localRotation = baseRotation * Quaternion.Euler(
                    0f, Mathf.Sin(shake * 40f) * 14f, Mathf.Sin(shake * 33f) * 8f);
                yield return null;
            }
            fishRoot.localRotation = baseRotation;

            // Let go and back off a little.
            var backOff = fishRoot.localPosition
                + (fishRoot.localPosition - target).normalized * 0.12f
                + Vector3.up * 0.02f;
            var release = 0f;
            while (release < 0.8f)
            {
                release += Time.deltaTime;
                MoveAlong(backOff, 4f);
                yield return null;
            }
        }

        IEnumerator Hold(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator SwimOut()
        {
            var direction = fishRoot.localPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector3.back;
            var exit = direction.normalized * (poolRadius * 1.08f)
                + Vector3.up * swimHeight * 0.6f;
            while (Vector3.Distance(fishRoot.localPosition, exit) > 0.03f)
            {
                MoveAlong(exit, 4f);
                yield return null;
            }
            fishRoot.gameObject.SetActive(false);
        }

        Vector3 AngleToPosition(float angle, float radius)
        {
            return new Vector3(Mathf.Cos(angle) * radius, swimHeight,
                Mathf.Sin(angle) * radius);
        }

        void MoveAlong(Vector3 target, float turnSpeed)
        {
            var previous = fishRoot.localPosition;
            fishRoot.localPosition = Vector3.MoveTowards(previous, target,
                Time.deltaTime * 0.4f);
            FaceTowards(target, turnSpeed);
        }

        void FaceTowards(Vector3 target, float turnSpeed)
        {
            var direction = target - fishRoot.localPosition;
            if (direction.sqrMagnitude < 0.00001f)
                return;
            var look = Quaternion.LookRotation(direction.normalized);
            fishRoot.localRotation = Quaternion.Slerp(
                fishRoot.localRotation, look, Time.deltaTime * turnSpeed);
        }

        void Update()
        {
            if (tailPivot != null && fishRoot.gameObject.activeSelf)
                tailPivot.localRotation = Quaternion.Euler(
                    0f, Mathf.Sin(Time.time * 6f) * 22f, 0f);
        }
    }
}
