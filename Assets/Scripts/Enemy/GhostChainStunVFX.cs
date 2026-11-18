using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hiệu ứng Xích Trói Ma Thuật sử dụng trực tiếp 3D Model Asset (Shackle.fbx):
/// - Không dùng mesh xích tự vẽ bằng code nữa.
/// - Khi kích hoạt chế độ trừ tà đỏ (crimsonExorcism = true), nhuộm toàn bộ model Shackle
///   thành màu đỏ rực lửa phát sáng bập bùng kèm tia lửa cháy & ánh sáng đỏ xung quanh con ma.
/// </summary>
public class GhostChainStunVFX : MonoBehaviour
{
    [Tooltip("Prefab xích 3D tải từ Asset (để trống sẽ tự load Resources/VFX/Shackle)")]
    public GameObject customBindingChainsPrefab;
    [Tooltip("Kích thước mục tiêu của vòng xích bao quanh thân ma (m)")]
    public float chainTargetSize = 1.15f;
    [Tooltip("Cường độ đèn phát sáng cơ bản của xích trói")]
    public float glowLightIntensity = 5.0f;
    [Tooltip("Bán kính chiếu sáng của đèn xích trói (m)")]
    public float glowLightRange = 5.0f;

    private static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static Material s_FieryRedShackleMaterial;
    private static Material s_CyanShackleMaterial;
    private static Material s_SharedSparkMaterial;
    private static Texture2D s_SharedSparkTexture;

    private GameObject chainsRoot;
    private GameObject assetChainsInstance;
    private readonly List<Renderer> assetRenderers = new List<Renderer>();
    private readonly List<Material[]> cachedCrimsonMatArrays = new List<Material[]>();
    private readonly List<Material[]> cachedCyanMatArrays = new List<Material[]>();
    private MaterialPropertyBlock emissionPropertyBlock;
    private ParticleSystem fierySparks;
    private Light bindingLight;
    private Coroutine activeStunRoutine;
    private Animator targetAnimator;
    private float originalAnimSpeed = 1f;
    private bool currentCrimsonMode;

    public bool IsStunned { get; private set; }

    /// <summary>
    /// Kích hoạt xích Shackle từ Asset và khóa cứng mục tiêu trong duration giây.
    /// </summary>
    public static GhostChainStunVFX ApplyChainStun(GameObject target, float duration, Vector3 pushDir, float pushDist, Animator anim = null, bool crimsonExorcism = false)
    {
        if (target == null) return null;

        GhostChainStunVFX comp = target.GetComponent<GhostChainStunVFX>();
        if (comp == null)
        {
            comp = target.AddComponent<GhostChainStunVFX>();
        }

        comp.TriggerStun(duration, pushDir, pushDist, anim, crimsonExorcism);
        return comp;
    }

    public void TriggerStun(float duration, Vector3 pushDir, float pushDist, Animator anim, bool crimsonExorcism = false)
    {
        if (anim != null) targetAnimator = anim;
        else if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);

        currentCrimsonMode = crimsonExorcism;

        if (activeStunRoutine != null)
        {
            StopCoroutine(activeStunRoutine);
        }

        activeStunRoutine = StartCoroutine(RoutineChainStun(duration, pushDir, pushDist));
    }

    private IEnumerator RoutineChainStun(float duration, Vector3 pushDir, float pushDist)
    {
        bool wasAlreadyStunned = IsStunned;
        IsStunned = true;
        EnsureChainsCreated();
        ApplyVisualStyle();

        if (chainsRoot != null)
        {
            chainsRoot.SetActive(true);
            chainsRoot.transform.localScale = currentCrimsonMode ? new Vector3(1f, 0.25f, 1f) : Vector3.one;
        }
        if (bindingLight != null)
        {
            bindingLight.enabled = true;
        }
        if (fierySparks != null)
        {
            if (currentCrimsonMode && !fierySparks.isPlaying) fierySparks.Play();
            else if (!currentCrimsonMode && fierySparks.isPlaying) fierySparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Khóa đứng yên Animator mục tiêu (chỉ lưu tốc độ gốc khi chưa bị choáng trước đó và tránh kẹt ở 1.8f của đòn đánh dở)
        if (targetAnimator != null)
        {
            if (!wasAlreadyStunned && targetAnimator.speed > 0.01f && targetAnimator.speed <= 1.25f)
            {
                originalAnimSpeed = targetAnimator.speed;
            }
            else if (!wasAlreadyStunned)
            {
                originalAnimSpeed = 1f;
            }
            targetAnimator.speed = 0f;
            try
            {
                targetAnimator.Play("OutOfBreath", 0, 0.15f);
            }
            catch { }
        }

        float knockTime = 0.16f;
        if (pushDist > 0.001f)
        {
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
        }
        else if (currentCrimsonMode && chainsRoot != null)
        {
            // Hiệu ứng xích Shackle bật lên siết chặt vào thân con ma
            float elapsed = 0f;
            while (elapsed < knockTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / knockTime);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                chainsRoot.transform.localScale = new Vector3(1f, Mathf.Lerp(0.25f, 1f, eased), 1f);
                yield return null;
            }
            chainsRoot.transform.localScale = Vector3.one;
        }

        // Rung siết + nhấp nháy rực lửa đỏ bập bùng suốt thời gian bị trói
        float remaining = Mathf.Max(0.1f, duration - knockTime);
        float timer = 0f;
        Vector3 baseLocalPos = chainsRoot != null ? chainsRoot.transform.localPosition : Vector3.up * 0.95f;

        while (timer < remaining)
        {
            timer += Time.deltaTime;
            if (chainsRoot != null)
            {
                float shake = Mathf.Sin(timer * 25f) * 0.012f;
                chainsRoot.transform.localPosition = baseLocalPos + new Vector3(shake, Mathf.Cos(timer * 20f) * 0.008f, shake);
            }

            if (currentCrimsonMode)
            {
                float firePulse = 4.6f + Mathf.Sin(timer * 14f) * 1.1f + Mathf.Cos(timer * 23f) * 0.5f;
                if (emissionPropertyBlock == null) emissionPropertyBlock = new MaterialPropertyBlock();
                emissionPropertyBlock.SetColor(s_EmissionColorId, new Color(1.0f, 0.14f, 0.02f) * firePulse);
                for (int i = 0; i < assetRenderers.Count; i++)
                {
                    Renderer r = assetRenderers[i];
                    if (r != null) r.SetPropertyBlock(emissionPropertyBlock);
                }
                if (bindingLight != null)
                {
                    bindingLight.intensity = (glowLightIntensity * 0.9f) + Mathf.Sin(timer * 16f) * 1.4f;
                }
            }
            yield return null;
        }

        ReleaseStun();
    }

    public void ReleaseStun()
    {
        IsStunned = false;
        if (chainsRoot != null)
        {
            chainsRoot.transform.localScale = Vector3.one;
            chainsRoot.SetActive(false);
        }
        if (bindingLight != null)
        {
            bindingLight.enabled = false;
        }
        if (fierySparks != null && fierySparks.isPlaying)
        {
            fierySparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    private void OnDisable()
    {
        if (IsStunned)
        {
            ReleaseStun();
        }
    }

    private void EnsureChainsCreated()
    {
        EnsureSharedMaterials();
        if (chainsRoot != null) return;

        Transform existing = transform.Find("GhostBindingChains_Root");
        if (existing != null)
        {
            Transform assetT = existing.Find("BindingChainsAsset");
            if (assetT != null)
            {
                chainsRoot = existing.gameObject;
                bindingLight = chainsRoot.GetComponentInChildren<Light>(true);
                fierySparks = chainsRoot.GetComponentInChildren<ParticleSystem>(true);
                assetChainsInstance = assetT.gameObject;
                CacheAssetRenderers();
                return;
            }
            Destroy(existing.gameObject);
        }

        chainsRoot = new GameObject("GhostBindingChains_Root");
        chainsRoot.transform.SetParent(transform, false);
        chainsRoot.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        GameObject prefabToUse = customBindingChainsPrefab != null
            ? customBindingChainsPrefab
            : (Resources.Load<GameObject>("VFX/BindingChainsPrefab") ?? Resources.Load<GameObject>("VFX/Shackle"));

        if (prefabToUse != null)
        {
            assetChainsInstance = Instantiate(prefabToUse, chainsRoot.transform, false);
            assetChainsInstance.name = "BindingChainsAsset";
            assetChainsInstance.transform.localPosition = Vector3.zero;
            assetChainsInstance.transform.localRotation = Quaternion.identity;

            foreach (Collider col in assetChainsInstance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            CacheAssetRenderers();

            // Tự động căn chỉnh tỷ lệ model Shackle vừa vặn bao quanh thân con ma (~1.15m)
            if (assetRenderers.Count > 0)
            {
                Bounds b = assetRenderers[0].bounds;
                for (int i = 1; i < assetRenderers.Count; i++)
                {
                    b.Encapsulate(assetRenderers[i].bounds);
                }
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 2.5f || maxDim < 0.35f)
                {
                    float targetSize = Mathf.Max(0.25f, chainTargetSize);
                    float s = targetSize / Mathf.Max(0.001f, maxDim);
                    assetChainsInstance.transform.localScale = assetChainsInstance.transform.localScale * s;
                }
            }
        }
        else
        {
            Debug.LogWarning("[GhostChainStunVFX] Không tìm thấy Prefab/Model tại Resources/VFX/Shackle hoặc BindingChainsPrefab!");
        }

        CreateFierySparks();

        GameObject lightObj = new GameObject("ChainGlowLight");
        lightObj.transform.SetParent(chainsRoot.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        bindingLight = lightObj.AddComponent<Light>();
        bindingLight.type = LightType.Point;
        bindingLight.color = new Color(1.0f, 0.20f, 0.04f);
        bindingLight.intensity = glowLightIntensity;
        bindingLight.range = glowLightRange;
        bindingLight.shadows = LightShadows.None;

        chainsRoot.SetActive(false);
    }

    private void CacheAssetRenderers()
    {
        assetRenderers.Clear();
        cachedCrimsonMatArrays.Clear();
        cachedCyanMatArrays.Clear();
        if (assetChainsInstance == null) return;

        EnsureSharedMaterials();
        foreach (Renderer r in assetChainsInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            assetRenderers.Add(r);

            int matCount = Mathf.Max(1, r.sharedMaterials.Length);
            Material[] crimsonArr = new Material[matCount];
            Material[] cyanArr = new Material[matCount];
            for (int m = 0; m < matCount; m++)
            {
                crimsonArr[m] = s_FieryRedShackleMaterial;
                cyanArr[m] = s_CyanShackleMaterial;
            }
            cachedCrimsonMatArrays.Add(crimsonArr);
            cachedCyanMatArrays.Add(cyanArr);
        }
    }

    private void ApplyVisualStyle()
    {
        if (bindingLight != null)
        {
            bindingLight.color = currentCrimsonMode
                ? new Color(1.0f, 0.18f, 0.03f)
                : new Color(0.25f, 0.85f, 1.0f);
            bindingLight.intensity = glowLightIntensity;
            bindingLight.range = glowLightRange;
        }

        for (int i = 0; i < assetRenderers.Count; i++)
        {
            Renderer r = assetRenderers[i];
            if (r == null) continue;
            r.SetPropertyBlock(null);
            if (currentCrimsonMode && i < cachedCrimsonMatArrays.Count)
            {
                r.sharedMaterials = cachedCrimsonMatArrays[i];
            }
            else if (!currentCrimsonMode && i < cachedCyanMatArrays.Count)
            {
                r.sharedMaterials = cachedCyanMatArrays[i];
            }
        }
    }

    private static void EnsureSharedMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Texture2D shackleTex = Resources.Load<Texture2D>("VFX/Shackle_Diff");

        if (s_FieryRedShackleMaterial == null)
        {
            s_FieryRedShackleMaterial = new Material(urpLit);
            s_FieryRedShackleMaterial.name = "M_FieryRedShackle";
            if (shackleTex != null)
            {
                if (s_FieryRedShackleMaterial.HasProperty("_BaseMap"))
                    s_FieryRedShackleMaterial.SetTexture("_BaseMap", shackleTex);
                if (s_FieryRedShackleMaterial.HasProperty("_MainTex"))
                    s_FieryRedShackleMaterial.SetTexture("_MainTex", shackleTex);
            }
            Color moltenRed = new Color(0.95f, 0.10f, 0.03f, 1f);
            s_FieryRedShackleMaterial.color = moltenRed;
            if (s_FieryRedShackleMaterial.HasProperty("_BaseColor"))
                s_FieryRedShackleMaterial.SetColor("_BaseColor", moltenRed);
            s_FieryRedShackleMaterial.EnableKeyword("_EMISSION");
            s_FieryRedShackleMaterial.SetColor("_EmissionColor", new Color(1.0f, 0.15f, 0.02f) * 5.2f);
            s_FieryRedShackleMaterial.SetFloat("_Metallic", 0.75f);
            s_FieryRedShackleMaterial.SetFloat("_Smoothness", 0.78f);
        }

        if (s_CyanShackleMaterial == null)
        {
            s_CyanShackleMaterial = new Material(urpLit);
            s_CyanShackleMaterial.name = "M_CyanShackle";
            if (shackleTex != null)
            {
                if (s_CyanShackleMaterial.HasProperty("_BaseMap"))
                    s_CyanShackleMaterial.SetTexture("_BaseMap", shackleTex);
                if (s_CyanShackleMaterial.HasProperty("_MainTex"))
                    s_CyanShackleMaterial.SetTexture("_MainTex", shackleTex);
            }
            Color cyanBase = new Color(0.18f, 0.65f, 0.95f, 1f);
            s_CyanShackleMaterial.color = cyanBase;
            if (s_CyanShackleMaterial.HasProperty("_BaseColor"))
                s_CyanShackleMaterial.SetColor("_BaseColor", cyanBase);
            s_CyanShackleMaterial.EnableKeyword("_EMISSION");
            s_CyanShackleMaterial.SetColor("_EmissionColor", new Color(0.20f, 0.88f, 1.0f) * 3.5f);
            s_CyanShackleMaterial.SetFloat("_Metallic", 0.80f);
            s_CyanShackleMaterial.SetFloat("_Smoothness", 0.75f);
        }
    }

    private void CreateFierySparks()
    {
        if (s_SharedSparkTexture == null)
        {
            s_SharedSparkTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            float half = 15.5f;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    s_SharedSparkTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            s_SharedSparkTexture.Apply();
        }

        if (s_SharedSparkMaterial == null)
        {
            Shader psShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            s_SharedSparkMaterial = new Material(psShader);
            if (s_SharedSparkMaterial.HasProperty("_BaseMap"))
                s_SharedSparkMaterial.SetTexture("_BaseMap", s_SharedSparkTexture);
            if (s_SharedSparkMaterial.HasProperty("_MainTex"))
                s_SharedSparkMaterial.SetTexture("_MainTex", s_SharedSparkTexture);
            if (s_SharedSparkMaterial.HasProperty("_Surface"))
                s_SharedSparkMaterial.SetFloat("_Surface", 1f);
        }

        GameObject psGo = new GameObject("FieryBindingSparks");
        psGo.transform.SetParent(chainsRoot.transform, false);
        psGo.transform.localPosition = Vector3.zero;
        fierySparks = psGo.AddComponent<ParticleSystem>();
        fierySparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = fierySparks.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.gravityModifier = -0.35f; // Tàn lửa bốc bay lên trên như lửa cháy
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.10f, 0.02f, 1f),
            new Color(1.0f, 0.55f, 0.08f, 0.95f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;

        var emission = fierySparks.emission;
        emission.rateOverTime = 45f;

        var shape = fierySparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.58f;

        var sizeOverLifetime = fierySparks.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f)
        ));

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.sharedMaterial = s_SharedSparkMaterial;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
