using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// Builds the stylised creatures and props from Unity primitives so the
    /// scene needs no imported 3D models. Base materials live in
    /// Assets/Resources/DoctorFish so their URP shader variants are always
    /// included in builds; runtime copies are tinted per creature.
    /// </summary>
    public static class CreatureBuilder
    {
        public static Material CreatureMaterial(Color color, float smoothness = 0.6f)
        {
            var material = new Material(Resources.Load<Material>("DoctorFish/Creature"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        public static Material WaterMaterial()
        {
            return new Material(Resources.Load<Material>("DoctorFish/Water"));
        }

        public static Material JellyMaterial(Color color, Color emission)
        {
            var material = new Material(Resources.Load<Material>("DoctorFish/Jelly"));
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission);
            return material;
        }

        public static Material BubbleMaterial(Color color)
        {
            var material = new Material(Resources.Load<Material>("DoctorFish/Bubble"));
            material.SetColor("_BaseColor", color);
            return material;
        }

        /// <summary>A primitive without its collider (the pool uses no physics).</summary>
        public static GameObject Primitive(PrimitiveType type, string name,
            Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// A little fish facing +Z: stretched sphere body, tail on a pivot
        /// (so it can wiggle) and two dark eyes.
        /// Returns the root; the tail pivot is the child named "TailPivot".
        /// </summary>
        public static GameObject BuildFish(string name, Transform parent,
            float length, Color bodyColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var body = CreatureMaterial(bodyColor);
            var dark = CreatureMaterial(Color.Lerp(bodyColor, Color.black, 0.65f));
            var eye = CreatureMaterial(new Color(0.08f, 0.08f, 0.1f), 0.9f);

            Primitive(PrimitiveType.Sphere, "Body", root.transform,
                Vector3.zero,
                new Vector3(length * 0.36f, length * 0.42f, length),
                body);

            var tailPivot = new GameObject("TailPivot");
            tailPivot.transform.SetParent(root.transform, false);
            tailPivot.transform.localPosition = new Vector3(0f, 0f, -length * 0.46f);
            Primitive(PrimitiveType.Sphere, "Tail", tailPivot.transform,
                new Vector3(0f, 0f, -length * 0.22f),
                new Vector3(length * 0.06f, length * 0.34f, length * 0.42f),
                dark);

            Primitive(PrimitiveType.Sphere, "DorsalFin", root.transform,
                new Vector3(0f, length * 0.2f, -length * 0.1f),
                new Vector3(length * 0.05f, length * 0.22f, length * 0.35f),
                dark);

            var eyeScale = Vector3.one * length * 0.09f;
            Primitive(PrimitiveType.Sphere, "EyeL", root.transform,
                new Vector3(-length * 0.15f, length * 0.08f, length * 0.36f),
                eyeScale, eye);
            Primitive(PrimitiveType.Sphere, "EyeR", root.transform,
                new Vector3(length * 0.15f, length * 0.08f, length * 0.36f),
                eyeScale, eye);

            return root;
        }

        /// <summary>
        /// A jellyfish: translucent pulsing bell plus swaying line tentacles.
        /// The bell is the child named "Bell"; tentacles are LineRenderers.
        /// </summary>
        public static GameObject BuildJellyfish(string name, Transform parent,
            float bellRadius, Color color, Color emission, int tentacles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var jellyMaterial = JellyMaterial(color, emission);
            var bell = Primitive(PrimitiveType.Sphere, "Bell", root.transform,
                Vector3.zero,
                new Vector3(bellRadius * 2f, bellRadius * 1.3f, bellRadius * 2f),
                jellyMaterial);
            bell.name = "Bell";

            var tentacleMaterial = BubbleMaterial(
                new Color(color.r, color.g, color.b, 0.45f));
            for (var i = 0; i < tentacles; i++)
            {
                var go = new GameObject($"Tentacle{i}");
                go.transform.SetParent(root.transform, false);
                var angle = i * Mathf.PI * 2f / tentacles;
                go.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * bellRadius * 0.55f,
                    -bellRadius * 0.4f,
                    Mathf.Sin(angle) * bellRadius * 0.55f);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 6;
                line.startWidth = bellRadius * 0.08f;
                line.endWidth = bellRadius * 0.02f;
                line.sharedMaterial = tentacleMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return root;
        }
    }
}
