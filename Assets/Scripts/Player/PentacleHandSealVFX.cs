using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng Dấu Ấn Pentacle trên tay người chơi khi nhấn/giữ phím E để trói ma:
/// 1. Dấu ấn Pentacle 3D phát sáng đỏ (hỗ trợ gắn Prefab Pentacle từ Sketchfab hoặc tự dựng mesh 3D).
/// 2. Particle System tia lửa/tàn đỏ xoay quanh bàn tay.
/// 3. LineRenderer bắn sợi xích năng lượng đỏ nối từ dấu ấn trên tay tới thân con ma.
/// </summary>
public class PentacleHandSealVFX : MonoBehaviour
{
    [Header("Tùy Chỉnh Dấu Ấn Pentacle")]
    [Tooltip("(Tùy chọn) Kéo Prefab/Model Pentacle tải từ Sketchfab vào đây. Để trống sẽ tự dựng Pentacle 3D phát sáng đỏ.")]
    public GameObject customPentaclePrefab;

    [Tooltip("Kích thước dấu ấn Pentacle ngay trong lòng bàn tay (đơn vị mét thế giới thực)")]
    [Range(0.05f, 0.8f)] public float sealScale = 0.22f;

    [Tooltip("Độ lệch tinh chỉnh của dấu ấn tính từ tâm lòng bàn tay theo hướng chiếu ra trước (đơn vị mét thế giới thực, không bị phóng đại theo xương FBX)")]
    public Vector3 handLocalOffset = new Vector3(0f, 0.01f, 0.03f);

    [Tooltip("Tỷ lệ nội suy từ cổ tay (RightHand) tới khớp ngón giữa (RightHandMiddle1) để định tâm lòng bàn tay")]
    [Range(0f, 1f)] public float palmFingerLerp = 0.58f;

    [Tooltip("Độ rộng sợi xích LineRenderer nối từ tay tới ma")]
    [Range(0.03f, 0.35f)] public float chainBeamWidth = 0.13f;

    [Tooltip("Số điểm uốn trên LineRenderer để tạo độ rung căng của dây xích")]
    [Range(2, 24)] public int beamSegments = 12;

    private static Mesh s_SharedPentacleMesh;
    private static Material s_SharedSealMaterial;
    private static Material s_SharedChainLineMaterial;
    private static Material s_SharedParticleMaterial;
    private static Texture2D s_SharedChainTexture;
    private static Texture2D s_SharedSparkTexture;
    private static readonly int s_BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int s_MainTexId = Shader.PropertyToID("_MainTex");
    private static bool s_ChainLineHasBaseMap;
    private static bool s_ChainLineHasMainTex;

    private Transform currentHandAnchor;
    private Transform currentTarget;
    private GameObject sealRoot;
    private Transform sealSpinPivot;
    private GameObject sealVisual;
    private Vector3 basePivotScale = Vector3.one;
    private Light sealLight;
    private ParticleSystem handParticles;
    private LineRenderer chainLine;
    private bool isActive;
    private float activeTimer;

    public bool IsActive => isActive;

    public void Activate(Transform target, Transform handAnchor)
    {
        if (target == null) return;

        currentTarget = target;
        EnsureSetup(handAnchor);
        SyncSealTransformToPalm();

        if (!isActive)
        {
            isActive = true;
            activeTimer = 0f;

            if (sealRoot != null)
            {
                sealRoot.SetActive(true);
            }
            SyncSealTransformToPalm();
            if (sealLight != null)
            {
                sealLight.enabled = true;
            }
            if (handParticles != null && !handParticles.isPlaying)
            {
                handParticles.Play();
            }
            if (chainLine != null)
            {
                chainLine.enabled = true;
            }
            UpdateChainLine();
        }
    }

    public void Deactivate()
    {
        if (!isActive && sealRoot == null) return;

        isActive = false;
        currentTarget = null;

        if (sealRoot != null)
        {
            sealRoot.SetActive(false);
        }
        if (sealLight != null)
        {
            sealLight.enabled = false;
        }
        if (handParticles != null && handParticles.isPlaying)
        {
            handParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (chainLine != null)
        {
            chainLine.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!isActive) return;

        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            Deactivate();
            return;
        }

        activeTimer += Time.deltaTime;
        SyncSealTransformToPalm();

        // Xoay nhẹ dấu ấn Pentacle quanh đúng tâm hình học ngay giữa lòng bàn tay + nhịp đập phát sáng
        if (sealSpinPivot != null)
        {
            sealSpinPivot.localRotation = Quaternion.Euler(0f, 0f, activeTimer * 110f);
            float pulse = 1f + Mathf.Sin(activeTimer * 10f) * 0.08f;
            sealSpinPivot.localScale = basePivotScale * pulse;
        }

        if (sealLight != null)
        {
            sealLight.intensity = 2.6f + Mathf.Sin(activeTimer * 14f) * 0.6f;
        }

        UpdateChainLine();
    }

    private void SyncSealTransformToPalm()
    {
        if (sealRoot == null) return;

        // Giữ tỷ lệ thế giới thực của sealRoot luôn chuẩn 1:1 (bất chấp Player scale 0.1 hay xương FBX scale 100/7.2)
        Vector3 parentLossy = transform.lossyScale;
        float invX = Mathf.Abs(parentLossy.x) > 0.0001f ? 1f / parentLossy.x : 1f;
        float invY = Mathf.Abs(parentLossy.y) > 0.0001f ? 1f / parentLossy.y : 1f;
        float invZ = Mathf.Abs(parentLossy.z) > 0.0001f ? 1f / parentLossy.z : 1f;
        sealRoot.transform.localScale = new Vector3(invX, invY, invZ);

        Vector3 palmWorldPos;
        Quaternion palmWorldRot;
        var playerCtrl = GetComponent<ThirdPersonPlayerController>();
        if (playerCtrl != null)
        {
            playerCtrl.palmFingerLerp = palmFingerLerp;
        }
        if (playerCtrl != null && playerCtrl.TryGetRightPalmWorldPose(out palmWorldPos, out palmWorldRot))
        {
            // Đã lấy chính xác tọa độ tâm lòng bàn tay động (giữa cổ tay & khớp ngón giữa)
        }
        else if (currentHandAnchor != null && currentHandAnchor != transform)
        {
            palmWorldPos = currentHandAnchor.position;
            palmWorldRot = currentHandAnchor.rotation;
        }
        else
        {
            palmWorldPos = transform.position + transform.rotation * new Vector3(0.18f, 1.05f, 0.32f);
            palmWorldRot = transform.rotation;
        }

        // Hướng mặt phẳng Dấu Ấn Pentacle từ lòng bàn tay chiếu thẳng về phía con Ma đang bị trói
        Quaternion aimRot = palmWorldRot;
        if (currentTarget != null)
        {
            Vector3 toTarget = (currentTarget.position + Vector3.up * 0.95f) - palmWorldPos;
            if (toTarget.sqrMagnitude > 0.001f)
            {
                aimRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }
        }

        sealRoot.transform.position = palmWorldPos + (aimRot * handLocalOffset);
        sealRoot.transform.rotation = aimRot;
    }

    private void UpdateChainLine()
    {
        if (chainLine == null || sealRoot == null || currentTarget == null) return;

        Vector3 startPos = sealRoot.transform.position;
        Vector3 endPos = currentTarget.position + Vector3.up * 0.95f;

        int count = Mathf.Max(2, beamSegments);
        if (chainLine.positionCount != count)
        {
            chainLine.positionCount = count;
        }

        Vector3 dir = endPos - startPos;
        float dist = dir.magnitude;
        Vector3 right = Vector3.Cross(dir.normalized, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 p = Vector3.Lerp(startPos, endPos, t);

            // Tạo độ võng + rung căng nhẹ ở giữa sợi xích (2 đầu bám chặt vào tay và thân ma)
            float envelope = Mathf.Sin(t * Mathf.PI);
            float sag = -0.10f * envelope;
            float jitter = Mathf.Sin(activeTimer * 28f + t * 10f) * 0.025f * envelope;
            p += Vector3.up * sag + right * jitter;

            chainLine.SetPosition(i, p);
        }

        // Cuộn texture mắt xích dọc theo tia bắn (0 tra cứu chuỗi mỗi frame)
        if (s_SharedChainLineMaterial != null)
        {
            float tileCount = Mathf.Max(1.5f, dist * 3.2f);
            float scroll = -activeTimer * 2.5f;
            Vector2 scaleVec = new Vector2(tileCount, 1f);
            Vector2 offsetVec = new Vector2(scroll, 0f);
            if (s_ChainLineHasBaseMap)
            {
                s_SharedChainLineMaterial.SetTextureScale(s_BaseMapId, scaleVec);
                s_SharedChainLineMaterial.SetTextureOffset(s_BaseMapId, offsetVec);
            }
            if (s_ChainLineHasMainTex)
            {
                s_SharedChainLineMaterial.SetTextureScale(s_MainTexId, scaleVec);
                s_SharedChainLineMaterial.SetTextureOffset(s_MainTexId, offsetVec);
            }
        }
    }

    private void OnDisable()
    {
        Deactivate();
    }

    private void EnsureSetup(Transform handAnchor)
    {
        Transform anchor = handAnchor != null ? handAnchor : transform;
        currentHandAnchor = anchor;

        if (sealRoot != null)
        {
            SyncSealTransformToPalm();
            return;
        }

        EnsureSharedResources();

        // Gắn sealRoot vào gốc nhân vật (không gắn trực tiếp làm con của xương FBX có scale 7.2x/100x để không bao giờ bị lệch vị trí hay biến dạng kích thước)
        sealRoot = new GameObject("PentacleHandSeal_Root");
        sealRoot.transform.SetParent(transform, false);
        SyncSealTransformToPalm();

        GameObject spinGo = new GameObject("PentacleSpinPivot");
        spinGo.transform.SetParent(sealRoot.transform, false);
        spinGo.transform.localPosition = Vector3.zero;
        spinGo.transform.localRotation = Quaternion.identity;
        spinGo.transform.localScale = Vector3.one;
        sealSpinPivot = spinGo.transform;

        // 1. Visual Pentacle 3D ngay trong lòng bàn tay (tự động căn chỉnh tâm hình học & mặt phẳng của FBX bất kỳ về đúng (0,0,0) của lòng bàn tay)
        GameObject prefabToUse = customPentaclePrefab != null
            ? customPentaclePrefab
            : Resources.Load<GameObject>("VFX/PentaclePrefab");

        if (prefabToUse != null)
        {
            sealVisual = Instantiate(prefabToUse, sealSpinPivot, false);
            sealVisual.name = "CustomPentacleModel";
            sealVisual.transform.localPosition = Vector3.zero;
            sealVisual.transform.localRotation = Quaternion.identity;
            sealVisual.transform.localScale = Vector3.one;
            foreach (Collider col in sealVisual.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            // Nếu FBX Pentacle nằm ngang trên mặt sàn XZ (trục Y mỏng nhất) hoặc YZ (trục X mỏng nhất), xoay dựng mặt ngôi sao hướng thẳng ra trước (+Z)
            MeshFilter[] mfs = sealVisual.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length > 0 && mfs[0] != null && mfs[0].sharedMesh != null)
            {
                Vector3 ms = mfs[0].sharedMesh.bounds.size;
                if (ms.y < ms.x * 0.35f && ms.y < ms.z * 0.35f)
                {
                    sealVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                else if (ms.x < ms.y * 0.35f && ms.x < ms.z * 0.35f)
                {
                    sealVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            // Gán vật liệu URP phát sáng đỏ và căn chỉnh kích thước + đưa đúng tâm Bounds của Mesh trùng khít 100% vào lòng bàn tay
            Renderer[] childRenderers = sealVisual.GetComponentsInChildren<Renderer>(true);
            bool hasMeshBounds = false;
            Bounds combinedBounds = default;
            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer r = childRenderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (s_SharedSealMaterial != null)
                {
                    int matCount = Mathf.Max(1, r.sharedMaterials.Length);
                    Material[] mats = new Material[matCount];
                    for (int m = 0; m < matCount; m++) mats[m] = s_SharedSealMaterial;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                if (!hasMeshBounds)
                {
                    combinedBounds = r.bounds;
                    hasMeshBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }

            if (hasMeshBounds)
            {
                float maxDim = Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z));
                float targetSize = Mathf.Max(0.08f, sealScale);
                if (maxDim > 0.0001f)
                {
                    float s = targetSize / maxDim;
                    sealVisual.transform.localScale = sealVisual.transform.localScale * s;
                }

                // Tính lại Bounds sau khi scale và dịch ngược để tâm ngôi sao nằm ngay chính giữa (0,0,0) của lòng bàn tay!
                hasMeshBounds = false;
                for (int i = 0; i < childRenderers.Length; i++)
                {
                    Renderer r = childRenderers[i];
                    if (r == null || r is ParticleSystemRenderer) continue;
                    if (!hasMeshBounds) { combinedBounds = r.bounds; hasMeshBounds = true; }
                    else combinedBounds.Encapsulate(r.bounds);
                }
                if (hasMeshBounds)
                {
                    Vector3 worldCenterOffset = combinedBounds.center - sealSpinPivot.position;
                    sealVisual.transform.position -= worldCenterOffset;
                }
            }
            else
            {
                sealVisual.transform.localScale = Vector3.one * sealScale;
            }
            basePivotScale = Vector3.one;
        }
        else
        {
            sealVisual = new GameObject("ProceduralPentacle3D");
            sealVisual.transform.SetParent(sealSpinPivot, false);
            sealVisual.transform.localPosition = Vector3.zero;
            sealVisual.transform.localRotation = Quaternion.identity;
            sealVisual.transform.localScale = Vector3.one * sealScale;
            basePivotScale = Vector3.one;

            MeshFilter mf = sealVisual.AddComponent<MeshFilter>();
            mf.sharedMesh = s_SharedPentacleMesh;
            MeshRenderer mr = sealVisual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = s_SharedSealMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 2. Đèn đỏ tỏa sáng quanh bàn tay
        GameObject lightGo = new GameObject("PentacleHandLight");
        lightGo.transform.SetParent(sealRoot.transform, false);
        lightGo.transform.localPosition = Vector3.zero;
        sealLight = lightGo.AddComponent<Light>();
        sealLight.type = LightType.Point;
        sealLight.color = new Color(1.0f, 0.14f, 0.10f);
        sealLight.intensity = 2.6f;
        sealLight.range = 3.2f;
        sealLight.shadows = LightShadows.None;

        // 3. Particle System tia lửa / tàn đỏ quanh bàn tay
        GameObject psGo = new GameObject("PentacleRedParticles");
        psGo.transform.SetParent(sealRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;
        handParticles = psGo.AddComponent<ParticleSystem>();
        ConfigureHandParticles(handParticles);

        // 4. LineRenderer dây xích năng lượng đỏ nối từ tay -> ma
        GameObject lineGo = new GameObject("PentacleChainLine");
        lineGo.transform.SetParent(transform, false);
        chainLine = lineGo.AddComponent<LineRenderer>();
        chainLine.useWorldSpace = true;
        chainLine.startWidth = chainBeamWidth;
        chainLine.endWidth = chainBeamWidth * 0.9f;
        chainLine.numCapVertices = 4;
        chainLine.textureMode = LineTextureMode.Tile;
        chainLine.alignment = LineAlignment.View;
        chainLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        chainLine.receiveShadows = false;
        chainLine.sharedMaterial = s_SharedChainLineMaterial;
        chainLine.enabled = false;

        sealRoot.SetActive(false);
    }

    private static void ConfigureHandParticles(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.15f, 0.08f, 1.0f),
            new Color(1.0f, 0.45f, 0.12f, 0.9f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.rateOverTime = 38f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.orbitalY = new ParticleSystem.MinMaxCurve(2.2f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.sharedMaterial = s_SharedParticleMaterial;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static void EnsureSharedResources()
    {
        if (s_SharedPentacleMesh == null)
        {
            s_SharedPentacleMesh = GeneratePentacleMesh(0.5f, 0.032f);
        }

        if (s_SharedSealMaterial == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            s_SharedSealMaterial = new Material(urpLit);
            s_SharedSealMaterial.color = new Color(0.65f, 0.05f, 0.05f, 1f);
            s_SharedSealMaterial.EnableKeyword("_EMISSION");
            s_SharedSealMaterial.SetColor("_EmissionColor", new Color(1.0f, 0.12f, 0.08f) * 4.5f);
            s_SharedSealMaterial.SetFloat("_Metallic", 0.5f);
            s_SharedSealMaterial.SetFloat("_Smoothness", 0.8f);
        }

        if (s_SharedChainTexture == null)
        {
            s_SharedChainTexture = CreateChainLinkTexture(128, 64);
        }

        if (s_SharedChainLineMaterial == null)
        {
            Shader lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            s_SharedChainLineMaterial = new Material(lineShader);
            s_SharedChainLineMaterial.color = new Color(1.0f, 0.22f, 0.16f, 1.0f);
            if (s_SharedChainLineMaterial.HasProperty(s_BaseMapId))
            {
                s_ChainLineHasBaseMap = true;
                s_SharedChainLineMaterial.SetTexture(s_BaseMapId, s_SharedChainTexture);
            }
            if (s_SharedChainLineMaterial.HasProperty(s_MainTexId))
            {
                s_ChainLineHasMainTex = true;
                s_SharedChainLineMaterial.SetTexture(s_MainTexId, s_SharedChainTexture);
            }
            if (s_SharedChainLineMaterial.HasProperty("_BaseColor"))
                s_SharedChainLineMaterial.SetColor("_BaseColor", new Color(1.0f, 0.25f, 0.18f, 1.0f));
            if (s_SharedChainLineMaterial.HasProperty("_EmissionColor"))
            {
                s_SharedChainLineMaterial.EnableKeyword("_EMISSION");
                s_SharedChainLineMaterial.SetColor("_EmissionColor", new Color(1.0f, 0.16f, 0.10f) * 3.0f);
            }
        }

        if (s_SharedSparkTexture == null)
        {
            s_SharedSparkTexture = CreateSoftGlowTexture(64);
        }

        if (s_SharedParticleMaterial == null)
        {
            Shader psShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            s_SharedParticleMaterial = new Material(psShader);
            if (s_SharedParticleMaterial.HasProperty("_BaseMap"))
                s_SharedParticleMaterial.SetTexture("_BaseMap", s_SharedSparkTexture);
            if (s_SharedParticleMaterial.HasProperty("_MainTex"))
                s_SharedParticleMaterial.SetTexture("_MainTex", s_SharedSparkTexture);
            if (s_SharedParticleMaterial.HasProperty("_Surface"))
                s_SharedParticleMaterial.SetFloat("_Surface", 1f); // Transparent
        }
    }

    private static Mesh GeneratePentacleMesh(float radius, float tubeRadius)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();

        // 1. Vòng tròn ngoài
        int ringSegments = 36;
        for (int i = 0; i < ringSegments; i++)
        {
            float a0 = (i / (float)ringSegments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)ringSegments) * Mathf.PI * 2f;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
            Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
            AddTubeSegment(p0, p1, tubeRadius, 6, verts, norms, tris);
        }

        // 2. Ngôi sao 5 cánh nội tiếp (nối đỉnh i -> i + 2)
        Vector3[] starPts = new Vector3[5];
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI * 0.5f + i * (Mathf.PI * 2f / 5f);
            starPts[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < 5; i++)
        {
            Vector3 start = starPts[i];
            Vector3 end = starPts[(i + 2) % 5];
            AddTubeSegment(start, end, tubeRadius * 0.85f, 6, verts, norms, tris);
        }

        Mesh mesh = new Mesh();
        mesh.name = "ProceduralPentacleMesh";
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
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

            tris.Add(a);
            tris.Add(c);
            tris.Add(b);

            tris.Add(b);
            tris.Add(c);
            tris.Add(d);
        }
    }

    private static Texture2D CreateChainLinkTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color glow = new Color(1.0f, 0.25f, 0.15f, 1.0f);
        Color core = new Color(1.0f, 0.72f, 0.55f, 1.0f);

        for (int y = 0; y < height; y++)
        {
            float ny = (y / (float)(height - 1)) * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                // 2 mắt xích nối tiếp trong 1 ô lặp: 1 mắt ngang + 1 thanh nối giữa
                float u = x / (float)width;
                float ringU = (u * 2f) % 1f * 2f - 1f;
                float distToOval = Mathf.Abs(Mathf.Sqrt(ringU * ringU * 0.65f + ny * ny) - 0.62f);

                // Thanh nối giữa các mắt xích
                float barDist = Mathf.Abs(ny);
                bool isBar = (u > 0.36f && u < 0.64f) || u < 0.14f || u > 0.86f;

                float alpha = 0f;
                if (distToOval < 0.22f)
                {
                    alpha = Mathf.Clamp01(1f - distToOval / 0.22f);
                }
                else if (isBar && barDist < 0.18f)
                {
                    alpha = Mathf.Clamp01(1f - barDist / 0.18f);
                }

                Color c = clear;
                if (alpha > 0.01f)
                {
                    c = Color.Lerp(glow, core, alpha * 0.7f);
                    c.a = alpha;
                }
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D CreateSoftGlowTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }
}
