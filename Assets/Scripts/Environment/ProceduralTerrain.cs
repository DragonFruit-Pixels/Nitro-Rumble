using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteInEditMode]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("Mesh")]
    public int   resolution = 128;
    public float size       = 800f;

    [Header("Shape")]
    [Tooltip("Radio de la zona plana central (donde va la pista)")]
    public float flatRadius  = 150f;
    [Tooltip("Ancho de la transición de plano a montaña")]
    public float slopeWidth  = 100f;
    [Tooltip("Altura máxima de las montañas")]
    public float maxHeight   = 150f;

    [Header("Noise")]
    public float noiseScale     = 0.018f;
    public float noiseAmplitude = 0.7f;
    public int   seed           = 0;

    private MeshFilter _filter;

    private void Awake()  => Generate();
    private void OnValidate() => Generate();

    [ContextMenu("Generate Terrain")]
    public void Generate()
    {
        _filter = GetComponent<MeshFilter>();
        _filter.sharedMesh = BuildMesh();
    }

    private Mesh BuildMesh()
    {
        int verts = resolution + 1;
        Vector3[] vertices  = new Vector3[verts * verts];
        Vector2[] uvs       = new Vector2[verts * verts];
        int[]     triangles = new int[resolution * resolution * 6];

        float step    = size / resolution;
        float half    = size * 0.5f;
        float seedOff = seed * 137.3f;

        for (int z = 0; z < verts; z++)
        {
            for (int x = 0; x < verts; x++)
            {
                float wx = x * step - half;
                float wz = z * step - half;

                float dist = Mathf.Sqrt(wx * wx + wz * wz);
                float t    = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((dist - flatRadius) / Mathf.Max(slopeWidth, 0.01f)));

                float n = Mathf.PerlinNoise(wx * noiseScale + seedOff, wz * noiseScale + seedOff);
                n = Mathf.Pow(n, 1.5f);

                float h = t * maxHeight * Mathf.Lerp(1f - noiseAmplitude, 1f, n);

                int i = z * verts + x;
                vertices[i] = new Vector3(wx, h, wz);
                uvs[i]      = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        int tri = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int bl = z * verts + x;
                int br = bl + 1;
                int tl = bl + verts;
                int tr = tl + 1;

                triangles[tri++] = bl; triangles[tri++] = tl; triangles[tri++] = tr;
                triangles[tri++] = bl; triangles[tri++] = tr; triangles[tri++] = br;
            }
        }

        Mesh mesh = new Mesh { name = "ProceduralTerrain" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = vertices;
        mesh.uv          = uvs;
        mesh.triangles   = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
