using UnityEngine;

/// <summary>
/// ゲルストナー波による動的な海。
/// 空のGameObjectに付けるだけでメッシュを自動生成し、毎フレーム波で変形します。
/// マテリアルは URP/Lit を透明・青系にしたものを MeshRenderer に割り当ててください。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GerstnerOcean : MonoBehaviour
{
    [Header("メッシュ設定")]
    [Tooltip("海の一辺の長さ(m)")] public float size = 80f;
    [Tooltip("一辺の分割数（多いほど滑らか・重い）")] [Range(20, 250)] public int resolution = 140;

    [System.Serializable]
    public class Wave
    {
        public Vector2 direction = new Vector2(1, 0.3f);
        [Range(0.01f, 1f)] public float steepness = 0.25f; // 波の尖り
        public float wavelength = 8f;                       // 波長(m)
    }

    [Header("波（3〜4個の重ね合わせが自然）")]
    public Wave[] waves =
    {
        new Wave { direction = new Vector2(1f, 0.2f),  steepness = 0.22f, wavelength = 11f },
        new Wave { direction = new Vector2(0.7f, 1f),  steepness = 0.15f, wavelength = 5.5f },
        new Wave { direction = new Vector2(-0.4f, 0.8f), steepness = 0.10f, wavelength = 2.8f },
    };

    [Header("動き")]
    [Tooltip("全体の速度倍率")] public float timeScale = 1f;

    Mesh mesh;
    Vector3[] baseVerts;
    Vector3[] verts;

    void Awake()
    {
        BuildGrid();
    }

    void BuildGrid()
    {
        mesh = new Mesh { name = "OceanGrid" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int n = resolution;
        baseVerts = new Vector3[(n + 1) * (n + 1)];
        var uv = new Vector2[baseVerts.Length];
        for (int z = 0, i = 0; z <= n; z++)
        {
            for (int x = 0; x <= n; x++, i++)
            {
                baseVerts[i] = new Vector3(
                    (x / (float)n - 0.5f) * size, 0f,
                    (z / (float)n - 0.5f) * size);
                uv[i] = new Vector2(x / (float)n, z / (float)n) * 8f; // タイル用
            }
        }
        var tris = new int[n * n * 6];
        for (int z = 0, t = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + n + 1; tris[t++] = i + n + 2;
            }
        }
        verts = (Vector3[])baseVerts.Clone();
        mesh.vertices = verts;
        mesh.uv = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void Update()
    {
        float t = Time.time * timeScale;
        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 p = baseVerts[i];
            Vector3 disp = Vector3.zero;
            foreach (var w in waves)
            {
                float k = 2f * Mathf.PI / Mathf.Max(0.1f, w.wavelength);
                float c = Mathf.Sqrt(9.81f / k);           // 波の位相速度
                Vector2 d = w.direction.normalized;
                float f = k * (Vector2.Dot(d, new Vector2(p.x, p.z)) - c * t);
                float a = w.steepness / k;                  // 振幅
                disp.x += d.x * a * Mathf.Cos(f);
                disp.y += a * Mathf.Sin(f);
                disp.z += d.y * a * Mathf.Cos(f);
            }
            verts[i] = p + disp;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
