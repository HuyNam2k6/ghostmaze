using System.Collections.Generic;
using UnityEngine;

public enum ChainSealPickupType
{
    Chain,
    PentacleSeal
}

/// <summary>
/// Vật phẩm Dây Xích & Dấu Ấn Pentacle rơi ngẫu nhiên trên mặt sàn bản đồ:
/// - Dùng model/prefab từ Asset (gán trong Inspector hoặc đặt tại Assets/Resources/Pickups/).
/// - Chỉ Con Người (Human) còn sống trong bán kính pickupRadius (3m) mới hiện nút Bàn Tay và nhặt được.
/// </summary>
public class ChainPickupItem : MonoBehaviour
{
    public static readonly List<ChainPickupItem> ActivePickups = new List<ChainPickupItem>();

    [Header("Loại & Asset Vật Phẩm")]
    public ChainSealPickupType itemType = ChainSealPickupType.Chain;

    [Tooltip("Prefab 3D model tải từ Asset cho vật phẩm này (nếu để trống sẽ tự tìm trong Resources/Pickups/)")]
    public GameObject customModelPrefab;

    [Tooltip("Bán kính hiện nút bàn tay để nhặt khi người chơi đi tới gần (m)")]
    public float pickupRadius = 3.0f;

    [Tooltip("Tốc độ xoay vật phẩm trên mặt sàn")]
    public float rotateSpeed = 55f;

    private static Mesh s_SharedPentacleMesh;
    private static Material s_SharedPentacleMaterial;

    private Transform visualTransform;
    private float baseY;
    private float animPhase;
    private bool isCollected;

    public string DisplayName => itemType == ChainSealPickupType.PentacleSeal ? "Dấu Ấn Pentacle" : "Dây Xích";

    public static ChainPickupItem CreateAt(Vector3 groundPosition, ChainSealPickupType type, GameObject modelPrefab = null, Transform parent = null)
    {
        GameObject go = new GameObject(type == ChainSealPickupType.PentacleSeal ? "PentacleSealPickup" : "ChainPickupItem");
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = groundPosition + Vector3.up * 0.22f;

        ChainPickupItem item = go.AddComponent<ChainPickupItem>();
        item.itemType = type;
        item.customModelPrefab = modelPrefab;
        item.BuildVisual();
        return item;
    }

    private void OnEnable()
    {
        if (!ActivePickups.Contains(this))
        {
            ActivePickups.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
    }

    private void OnDestroy()
    {
        ActivePickups.Remove(this);
    }

    private void Start()
    {
        if (visualTransform == null)
        {
            BuildVisual();
        }
        baseY = transform.position.y;
        animPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (isCollected) return;

        float t = Time.time + animPhase;
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(t * 2.4f) * 0.06f;
        transform.position = pos;

        if (visualTransform != null)
        {
            visualTransform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    public static ChainPickupItem FindNearestForHuman(ThirdPersonPlayerController player)
    {
        if (!IsLocalHumanAlive(player)) return null;

        Vector3 pPos = player.transform.position;
        ChainPickupItem best = null;
        float bestSqr = float.MaxValue;

        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            ChainPickupItem item = ActivePickups[i];
            if (item == null || item.isCollected || !item.gameObject.activeInHierarchy) continue;

            Vector3 diff = item.transform.position - pPos;
            float sqr = diff.sqrMagnitude;
            float r = item.pickupRadius;
            if (sqr <= r * r && sqr < bestSqr)
            {
                bestSqr = sqr;
                best = item;
            }
        }

        return best;
    }

    public bool TryManualCollect(ThirdPersonPlayerController player)
    {
        if (isCollected || !IsLocalHumanAlive(player)) return false;

        Vector3 diff = player.transform.position - transform.position;
        if (diff.sqrMagnitude > pickupRadius * pickupRadius) return false;

        if (itemType == ChainSealPickupType.PentacleSeal)
        {
            if (!player.CanAddChainCharge())
            {
                player.ShowTargetNotice($"Đã đầy {player.maxChainCharges}/{player.maxChainCharges} Dấu Ấn!");
                return false;
            }
            player.UnlockPentacleSeal();
            player.TryAddChainCharge(1);
            isCollected = true;
            Destroy(gameObject);
            return true;
        }

        if (!player.CanAddChainCharge())
        {
            player.ShowTargetNotice($"Đã đầy {player.maxChainCharges}/{player.maxChainCharges} Dây Xích!");
            return false;
        }

        if (player.TryAddChainCharge(1))
        {
            isCollected = true;
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    public static bool IsLocalHumanAlive(ThirdPersonPlayerController player)
    {
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        if (PumpkinGhostAI.PlayerControlledGhost != null) return false;
        if (PhotonMultiplayerChatVoice.Instance != null && PhotonMultiplayerChatVoice.Instance.IsLocalPlayerGhostRole())
        {
            return false;
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && health.isDead) return false;

        return true;
    }

    private void BuildVisual()
    {
        GameObject prefabToUse = customModelPrefab;
        if (prefabToUse == null)
        {
            string resPath = itemType == ChainSealPickupType.PentacleSeal
                ? "Pickups/PentaclePickup"
                : "Pickups/ChainPickup";
            prefabToUse = Resources.Load<GameObject>(resPath);
        }

        if (prefabToUse != null)
        {
            GameObject vis = Instantiate(prefabToUse, transform, false);
            vis.name = "PickupAssetVisual";
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            foreach (Collider col in vis.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            // Tự động scale vật phẩm Pentacle trên sàn (~0.45m)
            Renderer[] childRenderers = vis.GetComponentsInChildren<Renderer>(true);
            if (childRenderers.Length > 0)
            {
                Bounds b = childRenderers[0].bounds;
                for (int i = 1; i < childRenderers.Length; i++)
                {
                    b.Encapsulate(childRenderers[i].bounds);
                }
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 1.2f || maxDim < 0.2f)
                {
                    float targetSize = 0.45f;
                    float s = targetSize / Mathf.Max(0.001f, maxDim);
                    vis.transform.localScale = Vector3.one * s;
                }
            }

            visualTransform = vis.transform;
        }
        else
        {
            // Hiển thị Pentacle 3D phát sáng đỏ dưới sàn
            EnsurePentacleVisualResources();
            GameObject vis = new GameObject("PentaclePickupVisual");
            vis.transform.SetParent(transform, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            MeshFilter mf = vis.AddComponent<MeshFilter>();
            mf.sharedMesh = s_SharedPentacleMesh;
            MeshRenderer mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterial = s_SharedPentacleMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualTransform = vis.transform;
        }

        GameObject lightGo = new GameObject("PickupLight");
        lightGo.transform.SetParent(visualTransform, false);
        lightGo.transform.localPosition = Vector3.up * 0.15f;
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.16f, 0.10f);
        l.intensity = 1.8f;
        l.range = 2.8f;
        l.shadows = LightShadows.None;
    }

    private static void EnsurePentacleVisualResources()
    {
        if (s_SharedPentacleMaterial == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            s_SharedPentacleMaterial = new Material(urpLit);
            s_SharedPentacleMaterial.color = new Color(0.60f, 0.05f, 0.05f, 1f);
            s_SharedPentacleMaterial.EnableKeyword("_EMISSION");
            s_SharedPentacleMaterial.SetColor("_EmissionColor", new Color(1.0f, 0.12f, 0.08f) * 4.2f);
            s_SharedPentacleMaterial.SetFloat("_Metallic", 0.55f);
            s_SharedPentacleMaterial.SetFloat("_Smoothness", 0.80f);
        }

        if (s_SharedPentacleMesh == null)
        {
            s_SharedPentacleMesh = BuildPentaclePickupMesh(0.24f, 0.016f);
        }
    }

    private static Mesh BuildPentaclePickupMesh(float radius, float tubeRadius)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        int ringSegments = 30;
        for (int i = 0; i < ringSegments; i++)
        {
            float a0 = (i / (float)ringSegments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)ringSegments) * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
            AddTubeSegment(p0, p1, tubeRadius, 6, verts, norms, tris);
        }

        Vector3[] starPts = new Vector3[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI * 0.5f + i * (Mathf.PI * 2f / 5f);
            starPts[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < 5; i++)
        {
            AddTubeSegment(starPts[i], starPts[(i + 2) % 5], tubeRadius * 0.85f, 6, verts, norms, tris);
        }

        Mesh m = new Mesh();
        m.name = "PentaclePickupMesh";
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }

    private static void AddTubeSegment(Vector3 start, Vector3 end, float r, int sides, List<Vector3> verts, List<Vector3> norms, List<int> tris)
    {
        Vector3 dir = (end - start).normalized;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector3 norm = Vector3.Cross(dir, Vector3.forward);
        if (norm.sqrMagnitude < 0.0001f) norm = Vector3.Cross(dir, Vector3.up);
        norm.Normalize();
        Vector3 binorm = Vector3.Cross(dir, norm).normalized;

        int baseIdx = verts.Count;
        for (int ring = 0; ring < 2; ring++)
        {
            Vector3 center = ring == 0 ? start : end;
            for (int s = 0; s < sides; s++)
            {
                float a = (s / (float)sides) * Mathf.PI * 2f;
                Vector3 radial = (Mathf.Cos(a) * norm + Mathf.Sin(a) * binorm).normalized;
                verts.Add(center + radial * r);
                norms.Add(radial);
            }
        }

        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            int a = baseIdx + s;
            int b = baseIdx + next;
            int c = baseIdx + sides + s;
            int d = baseIdx + sides + next;

            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
    }
}
