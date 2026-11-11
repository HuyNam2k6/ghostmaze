using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng Xích Trói Ma Thuật (Spectral Binding Chains):
/// Xuất hiện các vòng xích ma thuật 3D phát sáng quấn chéo chữ X qua ngực, ngang eo và ghim chặt
/// con ma / đối thủ xuống đất khi bị ném trúng bởi Quả Cầu Ma Pháp (MagicWaveOrb).
/// Hoạt động đồng bộ 100% trên cả máy chủ, máy người ném, máy con ma và toàn bộ khán giả!
/// </summary>
public class GhostChainStunVFX : MonoBehaviour
{
    private static Material s_SharedChainMaterial;
    private static Mesh s_SharedChainMesh;

    private GameObject chainsRoot;
    private Light bindingLight;
    private Coroutine activeStunRoutine;
    private Animator targetAnimator;
    private float originalAnimSpeed = 1f;

    public bool IsStunned { get; private set; }

    /// <summary>
    /// Kích hoạt xích trói ma thuật và khóa cứng cử động mục tiêu trong duration giây.
    /// Hoạt động cho cả con ma của chính mình lẫn Avatar con ma từ xa của người chơi khác!
    /// </summary>
    public static GhostChainStunVFX ApplyChainStun(GameObject target, float duration, Vector3 pushDir, float pushDist, Animator anim = null)
    {
        if (target == null) return null;

        GhostChainStunVFX comp = target.GetComponent<GhostChainStunVFX>();
        if (comp == null)
        {
            comp = target.AddComponent<GhostChainStunVFX>();
        }

        comp.TriggerStun(duration, pushDir, pushDist, anim);
        return comp;
    }

    public void TriggerStun(float duration, Vector3 pushDir, float pushDist, Animator anim)
    {
        if (anim != null) targetAnimator = anim;
        else if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);

        if (activeStunRoutine != null)
        {
            StopCoroutine(activeStunRoutine);
        }

        activeStunRoutine = StartCoroutine(RoutineChainStun(duration, pushDir, pushDist));
    }

    private IEnumerator RoutineChainStun(float duration, Vector3 pushDir, float pushDist)
    {
        IsStunned = true;
        EnsureChainsCreated();

        if (chainsRoot != null)
        {
            chainsRoot.SetActive(true);
        }
        if (bindingLight != null)
        {
            bindingLight.enabled = true;
        }

        // Khóa đứng yên Animator mục tiêu
        if (targetAnimator != null)
        {
            originalAnimSpeed = targetAnimator.speed > 0.01f ? targetAnimator.speed : 1f;
            targetAnimator.speed = 0f;
            try
            {
                targetAnimator.Play("OutOfBreath", 0, 0.15f);
            }
            catch { }
        }

        // Hiệu ứng giật lùi nhẹ (Knockback) trong 0.18s đầu
        float knockTime = 0.18f;
        float elapsedKnock = 0f;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude > 0.001f) pushDir.Normalize();
        else pushDir = -transform.forward;

        float speed = pushDist / knockTime;
        while (elapsedKnock < knockTime)
        {
            float dt = Time.deltaTime;
            elapsedKnock += dt;
            transform.position += pushDir * (speed * dt);
            yield return null;
        }

        // Rung nhẹ ma thuật các mắt xích trong suốt thời gian bị trói
        float remaining = Mathf.Max(0.1f, duration - knockTime);
        float timer = 0f;
        Vector3 baseLocalPos = chainsRoot != null ? chainsRoot.transform.localPosition : Vector3.up * 0.95f;

        while (timer < remaining)
        {
            timer += Time.deltaTime;
            if (chainsRoot != null)
            {
                // Rung nhẹ như xích đang xiết chặt lấy con ma
                float shake = Mathf.Sin(timer * 25f) * 0.012f;
                chainsRoot.transform.localPosition = baseLocalPos + new Vector3(shake, Mathf.Cos(timer * 20f) * 0.008f, shake);
            }
            yield return null;
        }

        // Hết thời gian: Mở khóa giải phóng xích
        ReleaseStun();
    }

    public void ReleaseStun()
    {
        IsStunned = false;
        if (chainsRoot != null)
        {
            chainsRoot.SetActive(false);
        }
        if (bindingLight != null)
        {
            bindingLight.enabled = false;
        }

        if (targetAnimator != null)
        {
            targetAnimator.speed = originalAnimSpeed > 0.01f ? originalAnimSpeed : 1f;
        }

        if (activeStunRoutine != null)
        {
            StopCoroutine(activeStunRoutine);
            activeStunRoutine = null;
        }
    }

    private void EnsureChainsCreated()
    {
        if (chainsRoot != null) return;

        Transform existing = transform.Find("GhostBindingChains_Root");
        if (existing != null)
        {
            chainsRoot = existing.gameObject;
            bindingLight = chainsRoot.GetComponentInChildren<Light>();
            return;
        }

        chainsRoot = new GameObject("GhostBindingChains_Root");
        chainsRoot.transform.SetParent(transform, false);
        chainsRoot.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        // Khởi tạo Material xích ma thuật phát sáng URP Lit
        if (s_SharedChainMaterial == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            s_SharedChainMaterial = new Material(urpLit);
            Color chainGlow = new Color(0.20f, 0.88f, 1.0f); // Xanh lam ma thuật rực rỡ
            s_SharedChainMaterial.color = new Color(0.12f, 0.35f, 0.55f, 1.0f);
            s_SharedChainMaterial.EnableKeyword("_EMISSION");
            s_SharedChainMaterial.SetColor("_EmissionColor", chainGlow * 3.5f);
            s_SharedChainMaterial.SetFloat("_Metallic", 0.85f);
            s_SharedChainMaterial.SetFloat("_Smoothness", 0.75f);
        }

        // Tạo Mesh xích ma thuật
        if (s_SharedChainMesh == null)
        {
            s_SharedChainMesh = GenerateCompleteBindingChainsMesh();
        }

        MeshFilter mf = chainsRoot.AddComponent<MeshFilter>();
        mf.sharedMesh = s_SharedChainMesh;

        MeshRenderer mr = chainsRoot.AddComponent<MeshRenderer>();
        mr.sharedMaterial = s_SharedChainMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Đèn tỏa sáng ma mị bao quanh xích
        GameObject lightObj = new GameObject("ChainGlowLight");
        lightObj.transform.SetParent(chainsRoot.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        bindingLight = lightObj.AddComponent<Light>();
        bindingLight.type = LightType.Point;
        bindingLight.color = new Color(0.25f, 0.85f, 1.0f);
        bindingLight.intensity = 4.0f;
        bindingLight.range = 4.5f;
        bindingLight.shadows = LightShadows.None;

        chainsRoot.SetActive(false);
    }

    /// <summary>
    /// Dựng toàn bộ hệ thống mắt xích 3D:
    /// 1. Sợi xích chéo ngực 1 (Vai trái -> Hông phải)
    /// 2. Sợi xích chéo ngực 2 (Vai phải -> Hông trái) tạo thành chữ X khóa chặt thân
    /// 3. Sợi xích ngang đai eo quấn quanh người
    /// 4. Hai sợi xích ghim chặt xuống đất
    /// </summary>
    private static Mesh GenerateCompleteBindingChainsMesh()
    {
        List<Vector3> allVertices = new List<Vector3>();
        List<Vector3> allNormals = new List<Vector3>();
        List<int> allTriangles = new List<int>();

        // Tạo một mắt xích mẫu (Single Chain Link Mesh: Oval Ring)
        Mesh linkMesh = CreateOvalChainLinkMesh(0.048f, 0.088f, 0.018f, 14, 8);

        // 1. Chuỗi xích chéo ngực A: từ (-0.32, 0.50, 0.15) qua ngực (0.0, 0.15, 0.38) đến (0.35, -0.28, -0.15)
        AddChainAlongCurve(linkMesh, allVertices, allNormals, allTriangles, 11, t =>
        {
            float x = Mathf.Lerp(-0.35f, 0.38f, t);
            float y = Mathf.Lerp(0.50f, -0.32f, t);
            float z = Mathf.Sin(t * Mathf.PI) * 0.42f - Mathf.Cos(t * Mathf.PI) * 0.08f;
            return new Vector3(x, y, z);
        });

        // 2. Chuỗi xích chéo ngực B: từ (0.32, 0.50, 0.15) qua ngực (0.0, 0.15, 0.38) đến (-0.35, -0.28, -0.15) [Chữ X]
        AddChainAlongCurve(linkMesh, allVertices, allNormals, allTriangles, 11, t =>
        {
            float x = Mathf.Lerp(0.35f, -0.38f, t);
            float y = Mathf.Lerp(0.50f, -0.32f, t);
            float z = Mathf.Sin(t * Mathf.PI) * 0.42f - Mathf.Cos(t * Mathf.PI) * 0.08f;
            return new Vector3(x, y, z);
        });

        // 3. Chuỗi xích ngang eo: Vòng tròn bao quanh thân (bán kính 0.44m, cao y = -0.10m)
        AddChainAlongCurve(linkMesh, allVertices, allNormals, allTriangles, 14, t =>
        {
            float angle = t * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * 0.44f;
            float z = Mathf.Sin(angle) * 0.40f;
            return new Vector3(x, -0.10f, z);
        });

        // 4. Hai sợi xích ghim xuống đất (Ground Anchor Chains)
        AddChainAlongCurve(linkMesh, allVertices, allNormals, allTriangles, 8, t =>
        {
            float x = Mathf.Lerp(-0.40f, -0.75f, t);
            float y = Mathf.Lerp(-0.15f, -0.92f, t);
            float z = Mathf.Lerp(0.10f, 0.45f, t);
            return new Vector3(x, y, z);
        });

        AddChainAlongCurve(linkMesh, allVertices, allNormals, allTriangles, 8, t =>
        {
            float x = Mathf.Lerp(0.40f, 0.75f, t);
            float y = Mathf.Lerp(-0.15f, -0.92f, t);
            float z = Mathf.Lerp(0.10f, 0.45f, t);
            return new Vector3(x, y, z);
        });

        Mesh combined = new Mesh();
        combined.name = "SpectralBindingChainsMesh";
        combined.SetVertices(allVertices);
        combined.SetNormals(allNormals);
        combined.SetTriangles(allTriangles, 0);
        combined.RecalculateBounds();
        return combined;
    }

    /// <summary>
    /// Đặt các mắt xích nối nhau dọc theo đường cong tham số, xoay so le 0° và 90° để tạo thành dây xích chuẩn xác.
    /// </summary>
    private static void AddChainAlongCurve(Mesh linkMesh, List<Vector3> outVerts, List<Vector3> outNormals, List<int> outTris, int linkCount, System.Func<float, Vector3> pathFunc)
    {
        Vector3[] baseVerts = linkMesh.vertices;
        Vector3[] baseNormals = linkMesh.normals;
        int[] baseTris = linkMesh.triangles;

        for (int i = 0; i < linkCount; i++)
        {
            float t = i / (float)linkCount;
            float tNext = (i + 1) / (float)linkCount;

            Vector3 pos = pathFunc(t);
            Vector3 nextPos = pathFunc(tNext);
            Vector3 forward = (nextPos - pos).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

            // Xoay so le 90 độ giữa các mắt xích liền kề
            float rollAngle = (i % 2 == 0) ? 0f : 90f;
            Quaternion rot = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, rollAngle);

            int startVertex = outVerts.Count;
            for (int v = 0; v < baseVerts.Length; v++)
            {
                outVerts.Add(pos + rot * baseVerts[v]);
                outNormals.Add(rot * baseNormals[v]);
            }
            for (int k = 0; k < baseTris.Length; k++)
            {
                outTris.Add(startVertex + baseTris[k]);
            }
        }
    }

    /// <summary>
    /// Tạo Mesh một mắt xích hình oval khép kín chuẩn 3D.
    /// </summary>
    private static Mesh CreateOvalChainLinkMesh(float innerWidth, float innerLength, float tubeRadius, int pathSegments, int tubeSides)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ChainLinkMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Tạo đường cong khép kín hình oval gồm 2 cung tròn và 2 đoạn thẳng
        Vector3[] pathPoints = new Vector3[pathSegments];
        Vector3[] pathTangents = new Vector3[pathSegments];
        Vector3[] pathNormals = new Vector3[pathSegments];

        float halfLen = innerLength * 0.5f;
        float halfWid = innerWidth * 0.5f;

        for (int i = 0; i < pathSegments; i++)
        {
            float angle = (i / (float)pathSegments) * Mathf.PI * 2f;
            float sinA = Mathf.Sin(angle);
            float cosA = Mathf.Cos(angle);

            // Nửa trên cung tròn (+halfLen), nửa dưới cung tròn (-halfLen)
            float yOffset = cosA >= 0f ? halfLen : -halfLen;
            Vector3 p = new Vector3(sinA * halfWid, yOffset + cosA * (halfLen * 0.35f), 0f);
            pathPoints[i] = p;
        }

        for (int i = 0; i < pathSegments; i++)
        {
            int next = (i + 1) % pathSegments;
            Vector3 tang = (pathPoints[next] - pathPoints[i]).normalized;
            pathTangents[i] = tang;
            pathNormals[i] = Vector3.Cross(tang, Vector3.forward).normalized;
        }

        // Quét ống tròn quanh đường cong oval
        for (int i = 0; i < pathSegments; i++)
        {
            Vector3 center = pathPoints[i];
            Vector3 tang = pathTangents[i];
            Vector3 norm = pathNormals[i];
            Vector3 binorm = Vector3.Cross(tang, norm);

            for (int s = 0; s < tubeSides; s++)
            {
                float rad = (s / (float)tubeSides) * Mathf.PI * 2f;
                Vector3 radial = (Mathf.Cos(rad) * norm + Mathf.Sin(rad) * binorm).normalized;
                vertices.Add(center + radial * tubeRadius);
                normals.Add(radial);
            }
        }

        for (int i = 0; i < pathSegments; i++)
        {
            int nextSlice = (i + 1) % pathSegments;
            for (int s = 0; s < tubeSides; s++)
            {
                int nextSide = (s + 1) % tubeSides;

                int current = i * tubeSides + s;
                int right = i * tubeSides + nextSide;
                int top = nextSlice * tubeSides + s;
                int topRight = nextSlice * tubeSides + nextSide;

                triangles.Add(current);
                triangles.Add(top);
                triangles.Add(right);

                triangles.Add(right);
                triangles.Add(top);
                triangles.Add(topRight);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
