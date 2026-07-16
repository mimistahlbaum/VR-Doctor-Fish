using UnityEngine;

namespace DoctorFish
{
    /// <summary>
    /// The pool's water: a procedural disc mesh whose vertices ride three
    /// overlapping sine waves. Choppiness and tint are lerped between stages
    /// by the VisualController (calm welcome, lively fish, eerie jellyfish).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        public float radius = 0.5f;
        [Range(4, 48)] public int segments = 28;
        [Range(2, 24)] public int rings = 10;

        [Tooltip("Wave height multiplier, animated per stage.")]
        public float choppiness = 1f;
        public float baseWaveHeight = 0.004f;

        Mesh mesh;
        Vector3[] baseVertices;
        Vector3[] animatedVertices;
        Material material;
        Color currentColor;

        void Awake()
        {
            material = CreatureBuilder.WaterMaterial();
            currentColor = material.GetColor("_BaseColor");
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The mesh is built in Start so the bootstrap can set the radius
        // right after AddComponent (its Awake runs during that call).
        void Start()
        {
            BuildMesh();
        }

        void BuildMesh()
        {
            mesh = new Mesh { name = "WaterDisc" };
            var vertexCount = 1 + segments * rings;
            baseVertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            baseVertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (var r = 0; r < rings; r++)
            {
                var distance = radius * (r + 1) / rings;
                for (var s = 0; s < segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    var index = 1 + r * segments + s;
                    baseVertices[index] = new Vector3(
                        Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                    uv[index] = new Vector2(
                        0.5f + baseVertices[index].x / (radius * 2f),
                        0.5f + baseVertices[index].z / (radius * 2f));
                }
            }

            var triangles = new System.Collections.Generic.List<int>();
            for (var s = 0; s < segments; s++)
            {
                var next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + s);
            }
            for (var r = 0; r < rings - 1; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var next = (s + 1) % segments;
                    int a = 1 + r * segments + s;
                    int b = 1 + r * segments + next;
                    int c = 1 + (r + 1) * segments + s;
                    int d = 1 + (r + 1) * segments + next;
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                }
            }

            animatedVertices = (Vector3[])baseVertices.Clone();
            mesh.vertices = animatedVertices;
            mesh.uv = uv;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Update()
        {
            if (mesh == null)
                return;
            var t = Time.time;
            var height = baseWaveHeight * choppiness;
            for (var i = 0; i < baseVertices.Length; i++)
            {
                var v = baseVertices[i];
                var wave =
                    Mathf.Sin(t * 1.7f + v.x * 9f) * 0.5f +
                    Mathf.Sin(t * 2.3f + v.z * 11f) * 0.3f +
                    Mathf.Sin(t * 3.1f + (v.x + v.z) * 6f) * 0.2f;
                animatedVertices[i] = new Vector3(v.x, wave * height, v.z);
            }
            mesh.vertices = animatedVertices;
            mesh.RecalculateNormals();
        }

        public void SetTint(Color color)
        {
            currentColor = color;
            if (material != null)
                material.SetColor("_BaseColor", color);
        }

        public Color GetTint()
        {
            return currentColor;
        }
    }
}
