using System.Collections.Generic;
using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// Mirrors the haptic hardware in the scene: one small glow per
    /// vibration unit, anchored at that actuator's physical position on the
    /// virtual leg (see the layout table in haptics/README.md). Whenever the
    /// HapticController issues a command the matching glow lights up with
    /// the command's duty, so what the user sees and what they feel happen
    /// at the same spot - the welcome wave visibly travels down the leg and
    /// the big fish bite flashes at the big actuator.
    /// </summary>
    public class HapticNodeGlow : MonoBehaviour
    {
        class Node
        {
            public GameObject glow;
            public Material material;
            public float current;
            public float target;
        }

        public HapticController haptics;
        public float baseRadius = 0.012f;
        /// <summary>Extra radius at full duty.</summary>
        public float pulseRadius = 0.018f;
        public float riseSpeed = 14f;
        public float fallSpeed = 5f;

        readonly Dictionary<int, Node> nodes = new Dictionary<int, Node>();

        static readonly Color GlowColor = new Color(0.55f, 0.9f, 1f, 0.55f);
        static readonly Color GlowEmission = new Color(0.3f, 1.2f, 1.5f);

        void Start()
        {
            if (haptics != null)
                haptics.CommandSent += OnCommand;
        }

        void OnDestroy()
        {
            if (haptics != null)
                haptics.CommandSent -= OnCommand;
        }

        /// <summary>Place a glow for one unit address at its anchor.</summary>
        public void Register(int addr, Transform anchor)
        {
            var material = CreatureBuilder.JellyMaterial(GlowColor, Color.black);
            var glow = CreatureBuilder.Primitive(PrimitiveType.Sphere,
                $"HapticGlow{addr}", anchor, Vector3.zero,
                Vector3.one * baseRadius * 2f, material);
            glow.SetActive(false);
            nodes[addr] = new Node { glow = glow, material = material };
        }

        void OnCommand(int addr, int mode, int duty)
        {
            if (nodes.TryGetValue(addr, out var node))
                node.target = mode == 1 ? Mathf.Clamp01(duty / 15f) : 0f;
        }

        void Update()
        {
            foreach (var node in nodes.Values)
            {
                var speed = node.target > node.current ? riseSpeed : fallSpeed;
                node.current = Mathf.MoveTowards(node.current, node.target,
                    speed * Time.deltaTime);

                var visible = node.current > 0.01f;
                if (node.glow.activeSelf != visible)
                    node.glow.SetActive(visible);
                if (!visible)
                    continue;

                var radius = baseRadius + pulseRadius * node.current;
                node.glow.transform.localScale = Vector3.one * radius * 2f;
                node.material.SetColor("_EmissionColor",
                    GlowEmission * node.current);
            }
        }
    }
}
