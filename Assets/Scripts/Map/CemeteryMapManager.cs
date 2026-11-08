using UnityEngine;

[ExecuteAlways]
public class CemeteryMapManager : MonoBehaviour
{
    public static CemeteryMapManager Instance { get; private set; }

    public enum AtmosphereViewMode
    {
        HumanSurvivorDark, // Bên Người: map tối mịt mù, kịch tính, sương dày, ánh trăng mờ le lói
        GhostHunterClear   // Bên Ma: map sáng rõ bao quát, tầm nhìn xa, mắt ma soi tỏ toàn bộ nghĩa địa
    }

    [Header("Map Configuration")]
    [Tooltip("Tệp cấu hình ScriptableObject chứa toàn bộ thông số Collider, Ánh sáng, Quy mô bản đồ")]
    public CemeteryMapConfig config;

    [Header("Atmosphere Mode (Human vs Ghost)")]
    [Tooltip("Chế độ ánh sáng & sương mù hiện tại")]
    [SerializeField] private AtmosphereViewMode currentAtmosphereMode = AtmosphereViewMode.HumanSurvivorDark;
    public AtmosphereViewMode CurrentAtmosphereMode => currentAtmosphereMode;

    private Coroutine atmosphereTransitionRoutine;
    private Light cachedMoonlight;

    private void Awake()
    {
        Instance = this;
        StairRampSmoother.SmoothStairsInternal(0.40f, 0.02f);
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            // Khi vào game, mặc định là góc nhìn Người: áp dụng chế độ Tối kịch tính
            SetAtmosphereMode(AtmosphereViewMode.HumanSurvivorDark, 0f);
        }
    }

    private void OnEnable()
    {
        if (Instance == null) Instance = this;
        StairRampSmoother.SmoothStairsInternal(0.40f, 0.02f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Cập nhật kích thước, độ cao và trạng thái bật/tắt của toàn bộ Collider ngay lập tức (không cần dựng lại map)
    /// </summary>
    [ContextMenu("Apply Collider Settings")]
    public void ApplyColliderSettings()
    {
        if (config == null)
        {
            Debug.LogWarning("[CemeteryMapManager] Chưa gán CemeteryMapConfig!");
            return;
        }

        var colSettings = config.colliders;

        // 1. Rào chắn hàng rào chống rớt vực (Fence Barriers)
        foreach (var curb in GetComponentsInChildren<BoxCollider>(true))
        {
            if (curb.gameObject.name == "FenceCurb")
            {
                curb.enabled = colSettings.enableAllColliders && colSettings.enableFenceBarriers;
                float halfH = colSettings.fenceBarrierHeight * 0.5f;
                curb.center = new Vector3(0f, halfH * 1.6f, 0f);
                curb.size = new Vector3(colSettings.fenceBarrierThickness, colSettings.fenceBarrierHeight * 2.2f, 1.0f);
            }
            else if (curb.gameObject.name == "FencePostCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = colSettings.pillarColliderSize;
            }
            else if (curb.gameObject.name == "TombstoneCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = new Vector3(0.85f, 1.4f, 0.45f) * colSettings.tombstoneColliderMultiplier;
            }
            else if (curb.gameObject.name == "CoffinCollider")
            {
                curb.enabled = colSettings.enableAllColliders;
                curb.size = colSettings.coffinColliderSize;
            }
            else if (curb.gameObject.name.Contains("ObeliskCollider"))
            {
                curb.enabled = colSettings.enableAllColliders;
                Vector3 currentSize = curb.size;
                curb.size = new Vector3(colSettings.obeliskColliderThickness, currentSize.y, colSettings.obeliskColliderThickness);
            }
        }

        // 2. Thân cây cổ thụ (Tree Trunk Colliders)
        foreach (var cap in GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (cap.gameObject.name == "TreeTrunkCollider")
            {
                cap.enabled = colSettings.enableAllColliders;
                cap.height = colSettings.treeTrunkHeight;
                // Bán kính tự điều chỉnh theo cây lớn / thường
                cap.radius = cap.radius > 1.2f ? colSettings.giantTreeTrunkRadius : colSettings.normalTreeTrunkRadius;
            }
        }

        Debug.Log("<color=green>[CemeteryMapManager] Đã cập nhật toàn bộ Collider theo config thành công!</color>");
    }

    // =========================================================================
    // QUẢN LÝ KHÍ QUYỂN & ÁNH SÁNG HAI BÊN (NGƯỜI VS MA)
    // =========================================================================

    /// <summary>
    /// Tìm nguồn sáng Moonlight (Directional Light) trong bản đồ một cách an toàn và bền vững
    /// </summary>
    public Light FindDirectionalMoonlight()
    {
        if (cachedMoonlight != null) return cachedMoonlight;

        var moonGo = GameObject.Find("MoonDirectionalLight");
        if (moonGo != null && moonGo.TryGetComponent<Light>(out var ml))
        {
            cachedMoonlight = ml;
            return cachedMoonlight;
        }

        var dirGo = GameObject.Find("Directional Light");
        if (dirGo != null && dirGo.TryGetComponent<Light>(out var dl))
        {
            cachedMoonlight = dl;
            return cachedMoonlight;
        }

        var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (var l in allLights)
        {
            if (l != null && l.type == LightType.Directional)
            {
                cachedMoonlight = l;
                return cachedMoonlight;
            }
        }
        return null;
    }

    /// <summary>
    /// Đổi chế độ ánh sáng giữa bên Người (Tối kịch tính) và bên Ma (Sáng rõ bao quát)
    /// </summary>
    public void SetAtmosphereMode(AtmosphereViewMode mode, float transitionDuration = 1.2f)
    {
        currentAtmosphereMode = mode;
        if (atmosphereTransitionRoutine != null)
        {
            StopCoroutine(atmosphereTransitionRoutine);
            atmosphereTransitionRoutine = null;
        }

        if (transitionDuration <= 0.01f || !Application.isPlaying)
        {
            ApplyAtmosphereImmediate(mode);
        }
        else
        {
            atmosphereTransitionRoutine = StartCoroutine(RoutineTransitionAtmosphere(mode, transitionDuration));
        }
    }

    public void SwitchToHumanDarkLighting(float duration = 1.2f) => SetAtmosphereMode(AtmosphereViewMode.HumanSurvivorDark, duration);
    public void SwitchToGhostLighting(float duration = 1.2f) => SetAtmosphereMode(AtmosphereViewMode.GhostHunterClear, duration);

    /// <summary>
    /// Áp dụng ngay lập tức các thông số ánh sáng & sương mù theo chế độ
    /// </summary>
    public void ApplyAtmosphereImmediate(AtmosphereViewMode mode)
    {
        if (config == null) return;

        bool isHumanDark = (mode == AtmosphereViewMode.HumanSurvivorDark);
        var dirLight = FindDirectionalMoonlight();

        if (isHumanDark)
        {
            var hAtm = config.humanAtmosphere;
            RenderSettings.fog = hAtm.enableFog;
            RenderSettings.fogColor = hAtm.fogColor;
            RenderSettings.fogDensity = hAtm.fogDensity;

            RenderSettings.ambientSkyColor = hAtm.ambientSkyColor;
            RenderSettings.ambientEquatorColor = hAtm.ambientEquatorColor;
            RenderSettings.ambientGroundColor = hAtm.ambientGroundColor;

            if (dirLight != null)
            {
                dirLight.color = hAtm.moonlightColor;
                dirLight.intensity = hAtm.moonlightIntensity;
            }
        }
        else
        {
            var gAtm = config.atmosphere;
            RenderSettings.fog = gAtm.enableFog;
            RenderSettings.fogColor = gAtm.fogColor;
            RenderSettings.fogDensity = gAtm.fogDensity;

            RenderSettings.ambientSkyColor = gAtm.ambientSkyColor;
            RenderSettings.ambientEquatorColor = gAtm.ambientEquatorColor;
            RenderSettings.ambientGroundColor = gAtm.ambientGroundColor;

            if (dirLight != null)
            {
                dirLight.color = gAtm.moonlightColor;
                dirLight.intensity = gAtm.moonlightIntensity;
            }
        }

        // Đèn lồng & đài tế lễ trên bản đồ
        var baseAtm = config.atmosphere;
        foreach (var l in GetComponentsInChildren<Light>(true))
        {
            if (l.gameObject.name == "LanternPointLight")
            {
                l.color = baseAtm.lanternColor;
                l.intensity = baseAtm.lanternIntensity;
                l.range = baseAtm.lanternRange;
            }
            else if (l.gameObject.name == "AltarRitualLight")
            {
                l.intensity = baseAtm.ritualFireIntensity;
            }
        }
    }

    private System.Collections.IEnumerator RoutineTransitionAtmosphere(AtmosphereViewMode targetMode, float duration)
    {
        if (config == null) yield break;

        var dirLight = FindDirectionalMoonlight();

        // Target settings
        bool isTargetHuman = (targetMode == AtmosphereViewMode.HumanSurvivorDark);
        Color targetMoonColor = isTargetHuman ? config.humanAtmosphere.moonlightColor : config.atmosphere.moonlightColor;
        float targetMoonIntensity = isTargetHuman ? config.humanAtmosphere.moonlightIntensity : config.atmosphere.moonlightIntensity;

        bool targetFog = isTargetHuman ? config.humanAtmosphere.enableFog : config.atmosphere.enableFog;
        Color targetFogColor = isTargetHuman ? config.humanAtmosphere.fogColor : config.atmosphere.fogColor;
        float targetFogDensity = isTargetHuman ? config.humanAtmosphere.fogDensity : config.atmosphere.fogDensity;

        Color targetSky = isTargetHuman ? config.humanAtmosphere.ambientSkyColor : config.atmosphere.ambientSkyColor;
        Color targetEquator = isTargetHuman ? config.humanAtmosphere.ambientEquatorColor : config.atmosphere.ambientEquatorColor;
        Color targetGround = isTargetHuman ? config.humanAtmosphere.ambientGroundColor : config.atmosphere.ambientGroundColor;

        // Start settings
        Color startMoonColor = dirLight != null ? dirLight.color : targetMoonColor;
        float startMoonIntensity = dirLight != null ? dirLight.intensity : targetMoonIntensity;

        Color startFogColor = RenderSettings.fogColor;
        float startFogDensity = RenderSettings.fogDensity;

        Color startSky = RenderSettings.ambientSkyColor;
        Color startEquator = RenderSettings.ambientEquatorColor;
        Color startGround = RenderSettings.ambientGroundColor;

        RenderSettings.fog = targetFog;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (dirLight != null)
            {
                dirLight.color = Color.Lerp(startMoonColor, targetMoonColor, t);
                dirLight.intensity = Mathf.Lerp(startMoonIntensity, targetMoonIntensity, t);
            }

            RenderSettings.fogColor = Color.Lerp(startFogColor, targetFogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);

            RenderSettings.ambientSkyColor = Color.Lerp(startSky, targetSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(startEquator, targetEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(startGround, targetGround, t);

            yield return null;
        }

        ApplyAtmosphereImmediate(targetMode);
        atmosphereTransitionRoutine = null;
    }

    [ContextMenu("Chế độ Người: Tối kịch tính (Human Dark)")]
    public void ContextSwitchToHuman()
    {
        SetAtmosphereMode(AtmosphereViewMode.HumanSurvivorDark, Application.isPlaying ? 1.2f : 0f);
    }

    [ContextMenu("Chế độ Ma: Sáng rõ (Ghost Clear)")]
    public void ContextSwitchToGhost()
    {
        SetAtmosphereMode(AtmosphereViewMode.GhostHunterClear, Application.isPlaying ? 1.2f : 0f);
    }

    /// <summary>
    /// Cập nhật sương mù, màu sắc ánh trăng và đèn lồng trực tiếp trong Inspector hoặc khi đang chạy game
    /// </summary>
    [ContextMenu("Apply Atmosphere & Lighting")]
    public void ApplyAtmosphereSettings()
    {
        ApplyAtmosphereImmediate(currentAtmosphereMode);
        Debug.Log($"<color=green>[CemeteryMapManager] Đã áp dụng thông số Ánh sáng & Khí quyển ({currentAtmosphereMode}) thành công!</color>");
    }

    // =========================================================================
    // CÁC HÀM TIỆN ÍCH DÀNH CHO RAYCAST CỦA PLAYER
    // =========================================================================

    /// <summary>
    /// Bắn Raycast kiểm tra mặt đất bên dưới chân Player
    /// </summary>
    public bool RaycastGround(Vector3 playerFootPos, out RaycastHit hit, float maxDistance = 2.0f)
    {
        int layerMask = LayerMask.GetMask(config != null ? config.colliders.groundLayer : "Default");
        return Physics.Raycast(playerFootPos + Vector3.up * 0.2f, Vector3.down, out hit, maxDistance, layerMask);
    }

    /// <summary>
    /// Bắn Raycast kiểm tra chướng ngại vật trước mặt Player
    /// </summary>
    public bool RaycastForward(Vector3 origin, Vector3 forwardDir, out RaycastHit hit, float maxDistance = 2.5f)
    {
        int layerMask = LayerMask.GetMask(config != null ? config.colliders.obstacleLayer : "Default");
        return Physics.Raycast(origin, forwardDir, out hit, maxDistance, layerMask);
    }
}

