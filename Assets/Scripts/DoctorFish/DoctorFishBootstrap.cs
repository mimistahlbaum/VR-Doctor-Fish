using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;

namespace DoctorFish
{
    /// <summary>
    /// Builds the whole experience when the scene starts: the seated XR
    /// camera rig, lighting, the wooden foot-spa tub with animated water,
    /// the virtual feet (with short ankle stubs carrying the haptic node
    /// anchors), the creatures and the three controllers. Everything
    /// is generated from primitives and the base materials in Resources, so
    /// the scene file itself stays tiny.
    ///
    /// The user sits at the world origin with their feet in the tub in
    /// front of them, matching the real bucket of water.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DoctorFishBootstrap : MonoBehaviour
    {
        [Header("Layout")]
        public Vector3 poolCentre = new Vector3(0f, 0f, 0.55f);
        public float tubRadius = 0.5f;
        public float tubWallHeight = 0.36f;
        public float waterLevel = 0.3f;
        public float seatedEyeHeight = 1.1f;

        Transform poolRoot;
        Transform leftFoot;
        Transform rightFoot;
        readonly Dictionary<int, Transform> hapticAnchors =
            new Dictionary<int, Transform>();
        Light sun;
        WaterSurface water;
        TextMesh statusText;

        void Awake()
        {
            BuildCameraRig();
            BuildLighting();
            BuildEnvironment();
            BuildPool();
            BuildLegs();
            BuildBubbles();
            BuildStatusText();
            WireControllers();
        }

        void BuildCameraRig()
        {
            var rig = new GameObject("XR Rig");
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(rig.transform, false);
            // Desktop start pose: leaning a little forward over the tub so
            // both feet are in frame in the water. With a headset the
            // tracked pose replaces this.
            cameraGo.transform.localPosition =
                new Vector3(0f, seatedEyeHeight + 0.1f, 0.3f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.14f, 0.18f);
            cameraGo.AddComponent<AudioListener>();

            // Head tracking when a headset is connected. Without one the
            // actions simply never fire and the desktop controls take over.
            var poseDriver = cameraGo.AddComponent<TrackedPoseDriver>();
            var position = new InputAction("HeadPosition",
                binding: "<XRHMD>/centerEyePosition");
            var rotation = new InputAction("HeadRotation",
                binding: "<XRHMD>/centerEyeRotation");
            position.Enable();
            rotation.Enable();
            poseDriver.positionInput = new InputActionProperty(position);
            poseDriver.rotationInput = new InputActionProperty(rotation);

            // Look steeply down at the feet in the tub.
            var desktop = cameraGo.AddComponent<DesktopCameraController>();
            desktop.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
        }

        void BuildLighting()
        {
            var go = new GameObject("Sun");
            go.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.62f, 0.68f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.055f;
            RenderSettings.fogColor = new Color(0.16f, 0.27f, 0.32f);
        }

        void BuildEnvironment()
        {
            var floorMaterial = CreatureBuilder.CreatureMaterial(
                new Color(0.14f, 0.17f, 0.21f), 0.15f);
            var floor = CreatureBuilder.Primitive(PrimitiveType.Plane,
                "Floor", null, Vector3.zero, new Vector3(4f, 1f, 4f),
                floorMaterial);
            floor.GetComponent<Renderer>().shadowCastingMode =
                ShadowCastingMode.Off;

            // A soft round mat under the tub grounds the scene.
            var matMaterial = CreatureBuilder.CreatureMaterial(
                new Color(0.24f, 0.28f, 0.3f), 0.1f);
            CreatureBuilder.Primitive(PrimitiveType.Cylinder, "Mat", null,
                poolCentre + new Vector3(0f, 0.005f, 0f),
                new Vector3(tubRadius * 2.8f, 0.005f, tubRadius * 2.8f),
                matMaterial);
        }

        void BuildPool()
        {
            poolRoot = new GameObject("Pool").transform;
            poolRoot.position = poolCentre;

            var wood = CreatureBuilder.CreatureMaterial(
                new Color(0.45f, 0.31f, 0.19f), 0.35f);
            var darkWood = CreatureBuilder.CreatureMaterial(
                new Color(0.3f, 0.2f, 0.12f), 0.3f);

            // Tub base.
            CreatureBuilder.Primitive(PrimitiveType.Cylinder, "TubBase",
                poolRoot, new Vector3(0f, 0.02f, 0f),
                new Vector3(tubRadius * 2f, 0.02f, tubRadius * 2f), darkWood);

            // Tub wall from a ring of wooden staves.
            const int staves = 16;
            var chord = 2f * Mathf.PI * tubRadius / staves;
            for (var i = 0; i < staves; i++)
            {
                var angle = i * Mathf.PI * 2f / staves;
                var direction = new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle));
                var stave = CreatureBuilder.Primitive(PrimitiveType.Cube,
                    $"Stave{i}", poolRoot,
                    direction * tubRadius
                        + new Vector3(0f, tubWallHeight * 0.5f, 0f),
                    new Vector3(chord * 1.12f, tubWallHeight, 0.03f), wood);
                stave.transform.localRotation =
                    Quaternion.LookRotation(direction);
            }

            // Animated water surface.
            var waterGo = new GameObject("Water");
            waterGo.transform.SetParent(poolRoot, false);
            waterGo.transform.localPosition = new Vector3(0f, waterLevel, 0f);
            water = waterGo.AddComponent<WaterSurface>();
            water.radius = tubRadius * 0.94f;
        }

        // Physical position of each vibration unit on one leg, matching the
        // layout table in haptics/README.md (left leg = even addresses,
        // right leg = left + 1). Offsets are metres from the leg root at
        // (x, tub floor, poolCentre.z); positive x is outward, positive z
        // is toward the toes.
        static readonly (int addr, Vector3 offset)[] HapticNodeLayout =
        {
            (0,  new Vector3(0f, 0.24f, -0.015f)),  // A1 top front (shin)
            (16, new Vector3(0f, 0.205f, -0.09f)),  // B1 top back
            (2,  new Vector3(0f, 0.145f, -0.005f)), // A2 mid front (shin)
            (18, new Vector3(0f, 0.075f, -0.085f)), // B2 low back (heel)
            (4,  new Vector3(0f, 0.105f, 0.06f)),   // A3 bottom front (instep)
            (32, new Vector3(0.045f, 0.06f, -0.03f)) // big (outer ankle)
        };

        void BuildLegs()
        {
            var skin = CreatureBuilder.CreatureMaterial(
                new Color(0.87f, 0.67f, 0.53f), 0.35f);
            leftFoot = BuildLeg("LeftLeg", -0.09f, skin, 0);
            rightFoot = BuildLeg("RightLeg", 0.09f, skin, 1);
        }

        Transform BuildLeg(string name, float x, Material skin,
            int addressOffset)
        {
            var leg = new GameObject(name);
            leg.transform.SetParent(poolRoot.parent, false);

            // Short shin stub: ankle to just below the waterline, only tall
            // enough to carry the upper haptic nodes. Full-length shins read
            // as too long from the seated viewpoint.
            var shin = CreatureBuilder.Primitive(PrimitiveType.Capsule,
                name + "Shin", leg.transform,
                new Vector3(x, 0.16f, poolCentre.z - 0.05f),
                new Vector3(0.08f, 0.1f, 0.08f), skin);
            shin.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            // Foot: resting on the tub floor, toes forward.
            var foot = CreatureBuilder.Primitive(PrimitiveType.Capsule,
                name + "Foot", leg.transform,
                new Vector3(x, 0.06f, poolCentre.z + 0.02f),
                new Vector3(0.085f, 0.1f, 0.075f), skin);
            foot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Anchors marking where each vibration unit sits on this leg, so
            // the HapticNodeGlow can show every command at its real position.
            var outward = Mathf.Sign(x);
            foreach (var (addr, offset) in HapticNodeLayout)
            {
                var anchor = new GameObject($"{name}HapticNode{addr + addressOffset}");
                anchor.transform.SetParent(leg.transform, false);
                anchor.transform.localPosition = new Vector3(
                    x + offset.x * outward, offset.y,
                    poolCentre.z + offset.z);
                hapticAnchors[addr + addressOffset] = anchor.transform;
            }
            return foot.transform;
        }

        void BuildBubbles()
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(poolRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var bubbles = go.AddComponent<ParticleSystem>();
            var main = bubbles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startColor = new Color(1f, 1f, 1f, 0.35f);
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = bubbles.emission;
            emission.rateOverTime = 10f;

            var shape = bubbles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = tubRadius * 0.75f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = CreatureBuilder.BubbleMaterial(
                new Color(1f, 1f, 1f, 0.35f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildStatusText()
        {
            var go = new GameObject("StatusText");
            go.transform.position = poolCentre
                + new Vector3(0f, 0.5f, 0.6f);
            // Tilt up towards the seated viewer so the steep look-down
            // desktop view still reads it comfortably.
            go.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            statusText = go.AddComponent<TextMesh>();
            statusText.text = "VR Doctor Fish";
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.characterSize = 0.018f;
            statusText.fontSize = 64;
            statusText.color = new Color(0.92f, 0.97f, 1f, 0.9f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                statusText.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        void WireControllers()
        {
            var creatures = new GameObject("Creatures");
            creatures.transform.SetParent(poolRoot, false);

            var smallFish = creatures.AddComponent<SmallFishSchool>();
            smallFish.poolRadius = tubRadius * 0.85f;
            smallFish.Configure(leftFoot, rightFoot);

            var bigFish = creatures.AddComponent<BigFishController>();
            bigFish.poolRadius = tubRadius * 0.85f;
            bigFish.Configure(leftFoot, rightFoot);

            var jellyfish = creatures.AddComponent<JellyfishSwarm>();
            jellyfish.poolRadius = tubRadius * 0.85f;
            jellyfish.Configure(leftFoot, rightFoot);

            var visual = gameObject.AddComponent<VisualController>();
            visual.sun = sun;
            visual.water = water;
            visual.smallFish = smallFish;
            visual.bigFish = bigFish;
            visual.jellyfish = jellyfish;

            var audioController = gameObject.AddComponent<AudioController>();
            audioController.SetEffectsPosition(
                poolCentre + new Vector3(0f, waterLevel, 0f));

            var haptics = gameObject.AddComponent<HapticController>();
            haptics.sender = GetComponent<VibraForge>();

            // Visual glows at the actuator positions, driven by the haptic
            // commands so vibration and visuals share one position.
            var nodeGlow = gameObject.AddComponent<HapticNodeGlow>();
            nodeGlow.haptics = haptics;
            foreach (var pair in hapticAnchors)
                nodeGlow.Register(pair.Key, pair.Value);

            var manager = gameObject.AddComponent<ExperienceStateManager>();
            manager.visual = visual;
            manager.audioController = audioController;
            manager.haptics = haptics;
            manager.StageChanged += OnStageChanged;
        }

        void OnStageChanged(ExperienceStage stage)
        {
            if (statusText == null)
                return;
            switch (stage)
            {
                case ExperienceStage.Welcome:
                    statusText.text = "VR Doctor Fish";
                    break;
                case ExperienceStage.Finished:
                    statusText.text = "Session complete";
                    break;
                default:
                    statusText.text = "";
                    break;
            }
        }
    }
}
