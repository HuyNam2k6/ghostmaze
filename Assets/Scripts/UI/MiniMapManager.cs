using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GhostMaze.UI
{
    /// <summary>
    /// Hệ thống Mini-map (Bản đồ thu nhỏ) góc nhìn từ trên cao xuống cho GhostMaze:
    /// - Camera trực giao (Orthographic) chuyên dụng kết xuất thời gian thực vào RenderTexture 512x512.
    /// - Khung La Bàn Gothic u ám huyền bí với 4 hướng Đông - Tây - Nam - Bắc (N, S, E, W).
    /// - Mũi tên ngọc bích chỉ hướng người chơi luôn ở chính giữa.
    /// - Radar quái ma: Cảnh báo vị trí quái ma bí ngô (PumpkinGhostAI) bằng đốm đỏ rực khi áp sát.
    /// - Phím M: Phóng to giữa màn hình (Full Map) / Thu nhỏ lại góc phải (Mini Map).
    /// - Phím N: Đổi chế độ xoay bản đồ theo hướng nhìn nhân vật (Compass Mode / North-Up).
    /// - Nút [+] và [-] phóng to / thu nhỏ tầm nhìn bản đồ.
    /// - Tối ưu bộ nhớ: Zero-allocation trong Update, Object pooling tái sử dụng đốm radar.
    /// </summary>
    public class MiniMapManager : MonoBehaviour
    {
        public static MiniMapManager Instance { get; private set; }

        [Header("1. Mục Tiêu Theo Dõi (Player Target)")]
        [Tooltip("Transform người chơi mà Mini Map bám theo. Tự động tìm Player nếu để trống.")]
        public Transform targetPlayer;

        [Header("2. Cấu Hình Camera Mini Map")]
        [Tooltip("Độ cao Camera Mini Map so với người chơi (mét)")]
        [Range(20f, 150f)] public float cameraHeight = 75f;

        [Tooltip("Tầm nhìn bản đồ mặc định (Orthographic Size - mét)")]
        [Range(15f, 100f)] public float defaultZoom = 38f;

        [Tooltip("Tầm nhìn gần nhất khi zoom vào (mét)")]
        public float minZoom = 18f;

        [Tooltip("Tầm nhìn xa nhất khi zoom ra (mét)")]
        public float maxZoom = 80f;

        [Tooltip("Bước nhảy khi nhấn nút phóng to/thu nhỏ")]
        public float zoomStep = 8f;

        [Tooltip("Tùy chọn: Xoay bản đồ theo hướng nhìn nhân vật (true) hoặc cố định hướng Bắc ở trên (false)")]
        public bool rotateMapWithPlayer = false;

        [Header("3. Radar Quét Quái Ma (Enemy Radar)")]
        [Tooltip("Hiển thị đốm đỏ cảnh báo quái ma bí ngô trên Mini Map")]
        public bool showEnemiesOnRadar = true;

        [Tooltip("Bán kính tối đa hiển thị quái ma quanh người chơi (mét)")]
        [Range(15f, 120f)] public float radarRange = 60f;

        [Header("4. Phím Tắt Tiện Ích")]
        [Tooltip("Phím mở rộng toàn màn hình / thu nhỏ Mini Map")]
        public KeyCode toggleFullscreenKey = KeyCode.M;

        [Tooltip("Phím đổi chế độ xoay bản đồ")]
        public KeyCode toggleRotateModeKey = KeyCode.N;

        // Thành phần Camera & Texture
        private Camera miniMapCamera;
        private RenderTexture miniMapRT;
        private float currentZoom;

        // Giao diện UI
        private GameObject miniMapRoot;
        private RectTransform mapContainerRect;
        private RawImage mapRawImage;
        private RectTransform playerMarkerRect;
        private RectTransform compassRingRect;
        private Text zoomLevelText;
        private Text modeBadgeText;
        private bool isFullscreen = false;
        private bool isMapVisible = true;

        // Object Pool cho Radar Quái Ma (Zero GC allocation)
        private const int MAX_RADAR_BLIPS = 16;
        private readonly List<RectTransform> ghostBlipPool = new List<RectTransform>(MAX_RADAR_BLIPS);
        private Transform blipContainer;
        private Button settingsGearBtn;
        public Button SettingsGearButton => settingsGearBtn;
        private static Sprite gearIconSprite;
        private static Sprite expandIconSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance == null && FindAnyObjectByType<MiniMapManager>() == null)
            {
                GameObject go = new GameObject("MiniMap_System");
                go.AddComponent<MiniMapManager>();
                DontDestroyOnLoad(go);
            }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/GhostMaze/Tạo Mini Map trong Scene")]
        public static void CreateMiniMapInScene()
        {
            var existing = FindAnyObjectByType<MiniMapManager>();
            if (existing != null)
            {
                UnityEditor.Selection.activeGameObject = existing.gameObject;
                Debug.Log("<color=#00FF88>[MiniMap] Đã có sẵn MiniMap_System trong Scene!</color>");
                return;
            }
            GameObject go = new GameObject("MiniMap_System");
            go.AddComponent<MiniMapManager>();
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create MiniMap System");
            UnityEditor.Selection.activeGameObject = go;
            Debug.Log("<color=#00FF88>[MiniMap] Đã tạo MiniMap_System trong Scene thành công!</color>");
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            currentZoom = defaultZoom;
            SetupRenderTextureAndCamera();
            BuildMiniMapUI();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            FindTargetPlayer();
            UpdateSceneVisibilityAndLinks();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            targetPlayer = null;
            FindTargetPlayer();
            UpdateSceneVisibilityAndLinks();
        }

        private void UpdateSceneVisibilityAndLinks()
        {
            bool isLobby = SceneManager.GetActiveScene().name == "LobbyScene";
            if (miniMapRoot != null) miniMapRoot.SetActive(!isLobby && isMapVisible);
            if (miniMapCamera != null) miniMapCamera.enabled = !isLobby && isMapVisible;

            if (!isLobby)
            {
                var gsm = GameSettingsManager.Instance != null ? GameSettingsManager.Instance : FindAnyObjectByType<GameSettingsManager>();
                if (gsm != null && gsm.gearCornerButton != null)
                {
                    // Ẩn nút bánh răng cũ ở góc (-25,-25) đang bị MiniMap đè lên để dùng nút Cài Đặt [⚙] gọn đẹp ngay dưới MiniMap
                    gsm.gearCornerButton.gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (miniMapRT != null)
            {
                if (miniMapCamera != null) miniMapCamera.targetTexture = null;
                miniMapRT.Release();
                Destroy(miniMapRT);
                miniMapRT = null;
            }
            ghostBlipPool.Clear();
            if (Instance == this) Instance = null;
        }

        private void SetupRenderTextureAndCamera()
        {
            // 1. Tạo RenderTexture 512x512 sắc nét nhưng tiết kiệm VRAM (tránh quá tải GPU trên Android & khi mở nhiều cửa sổ test)
            miniMapRT = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "MiniMap_RenderTexture",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp
            };
            miniMapRT.Create();

            // 2. Tạo Camera trực giao nhìn thẳng từ trên xuống
            GameObject camGo = new GameObject("MiniMap_Camera");
            camGo.transform.SetParent(transform, false);

            miniMapCamera = camGo.AddComponent<Camera>();
            miniMapCamera.orthographic = true;
            miniMapCamera.orthographicSize = currentZoom;
            miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
            miniMapCamera.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            miniMapCamera.nearClipPlane = 0.5f;
            miniMapCamera.farClipPlane = 250f;
            miniMapCamera.targetTexture = miniMapRT;

            // Loại bỏ UI layer khỏi Camera Mini Map
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                miniMapCamera.cullingMask = ~(1 << uiLayer);
            }

            // Tối ưu hóa URP (Tắt Post-Processing và Shadow để camera chạy siêu nhẹ)
            var urpCam = camGo.GetComponent<UniversalAdditionalCameraData>();
            if (urpCam == null) urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            if (urpCam != null)
            {
                urpCam.renderPostProcessing = false;
                urpCam.renderShadows = false;
            }

            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void FindTargetPlayer()
        {
            if (targetPlayer != null) return;

            var playerCtrl = ThirdPersonPlayerController.Instance;
            if (playerCtrl != null)
            {
                targetPlayer = playerCtrl.transform;
                return;
            }

            GameObject pGo = GameObject.FindWithTag("Player");
            if (pGo != null)
            {
                targetPlayer = pGo.transform;
            }
        }

        private void Update()
        {
            HandleInputs();
        }

        private void LateUpdate()
        {
            if (miniMapCamera == null || mapContainerRect == null) return;

            if (targetPlayer == null)
            {
                FindTargetPlayer();
                if (targetPlayer == null) return;
            }

            Vector3 targetPos = targetPlayer.position;

            // 1. Cập nhật vị trí Camera Mini Map
            miniMapCamera.transform.position = new Vector3(targetPos.x, targetPos.y + cameraHeight, targetPos.z);
            miniMapCamera.orthographicSize = currentZoom;

            float playerYaw = targetPlayer.eulerAngles.y;

            // 2. Xoay Camera hoặc Xoay Icon theo chế độ
            if (rotateMapWithPlayer)
            {
                miniMapCamera.transform.rotation = Quaternion.Euler(90f, playerYaw, 0f);
                if (playerMarkerRect != null) playerMarkerRect.localRotation = Quaternion.identity;
                if (compassRingRect != null) compassRingRect.localRotation = Quaternion.Euler(0f, 0f, playerYaw);
            }
            else
            {
                miniMapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                if (playerMarkerRect != null) playerMarkerRect.localRotation = Quaternion.Euler(0f, 0f, -playerYaw);
                if (compassRingRect != null) compassRingRect.localRotation = Quaternion.identity;
            }

            // 3. Cập nhật Radar Quái Ma
            UpdateEnemyRadarBlips(targetPos, playerYaw);
        }

        private void HandleInputs()
        {
            if (ThirdPersonCamera.isUIPanelOpen) return;

            bool toggleFullscreen = false;
            bool toggleRotate = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                toggleFullscreen = kb.mKey.wasPressedThisFrame;
                toggleRotate = kb.nKey.wasPressedThisFrame;
            }
#else
            toggleFullscreen = Input.GetKeyDown(toggleFullscreenKey);
            toggleRotate = Input.GetKeyDown(toggleRotateModeKey);
#endif

            if (toggleFullscreen)
            {
                ToggleFullscreen();
            }
            if (toggleRotate)
            {
                ToggleRotateMode();
            }
        }

        public void ToggleVisibility()
        {
            isMapVisible = !isMapVisible;
            UpdateSceneVisibilityAndLinks();
        }

        public void OnClickSettingsButton()
        {
            if (isFullscreen)
            {
                isFullscreen = false;
                ApplyLayout();
            }

            var gsm = GameSettingsManager.Instance != null ? GameSettingsManager.Instance : FindAnyObjectByType<GameSettingsManager>();
            if (gsm != null)
            {
                gsm.ToggleDashboard();
                return;
            }

            if (PhotonMultiplayerChatVoice.Instance != null)
            {
                PhotonMultiplayerChatVoice.Instance.ToggleVoiceSettingsDialog();
            }
        }

        public void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;
            ApplyLayout();
        }

        public void ToggleRotateMode()
        {
            rotateMapWithPlayer = !rotateMapWithPlayer;
            if (modeBadgeText != null)
            {
                modeBadgeText.text = rotateMapWithPlayer ? "XOAY: THEO HƯỚNG" : "XOAY: HƯỚNG BẮC";
            }
        }

        public void ZoomIn()
        {
            currentZoom = Mathf.Clamp(currentZoom - zoomStep, minZoom, maxZoom);
            UpdateZoomText();
        }

        public void ZoomOut()
        {
            currentZoom = Mathf.Clamp(currentZoom + zoomStep, minZoom, maxZoom);
            UpdateZoomText();
        }

        private void UpdateZoomText()
        {
            if (zoomLevelText != null)
            {
                zoomLevelText.text = $"-{Mathf.RoundToInt(currentZoom)}m";
            }
        }

        private void ApplyLayout()
        {
            if (mapContainerRect == null) return;

            if (isFullscreen)
            {
                // Toàn màn hình trung tâm (Center Screen)
                mapContainerRect.anchorMin = new Vector2(0.5f, 0.5f);
                mapContainerRect.anchorMax = new Vector2(0.5f, 0.5f);
                mapContainerRect.pivot = new Vector2(0.5f, 0.5f);
                mapContainerRect.anchoredPosition = Vector2.zero;
                mapContainerRect.sizeDelta = new Vector2(520f, 520f);
            }
            else
            {
                // Thu nhỏ góc trên bên phải (Top-Right HUD)
                mapContainerRect.anchorMin = new Vector2(1f, 1f);
                mapContainerRect.anchorMax = new Vector2(1f, 1f);
                mapContainerRect.pivot = new Vector2(1f, 1f);
                mapContainerRect.anchoredPosition = new Vector2(-22f, -22f);
                mapContainerRect.sizeDelta = new Vector2(230f, 230f);
            }
        }

        // =========================================================================
        // RADAR CẢNH BÁO QUÁI MA (ZERO GC ALLOCATION)
        // =========================================================================
        private void UpdateEnemyRadarBlips(Vector3 playerPos, float playerYaw)
        {
            if (!showEnemiesOnRadar || blipContainer == null || mapContainerRect == null)
            {
                for (int i = 0; i < ghostBlipPool.Count; i++)
                {
                    if (ghostBlipPool[i] != null && ghostBlipPool[i].gameObject != null && ghostBlipPool[i].gameObject.activeSelf)
                    {
                        ghostBlipPool[i].gameObject.SetActive(false);
                    }
                }
                return;
            }

            var ghosts = PumpkinGhostAI.ActiveGhosts;
            int count = ghosts != null ? ghosts.Count : 0;
            int blipIndex = 0;

            float mapRadiusPixels = (mapContainerRect.sizeDelta.x * 0.5f) - 18f;
            float worldRadius = currentZoom;

            for (int i = 0; i < count; i++)
            {
                var ghost = ghosts[i];
                if (ghost == null || !ghost.gameObject.activeInHierarchy) continue;

                Vector3 ghostPos = ghost.transform.position;
                Vector3 diff = ghostPos - playerPos;
                float dist = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

                if (dist > radarRange && dist > worldRadius * 1.25f) continue;

                Vector2 offset;
                if (rotateMapWithPlayer)
                {
                    float rad = -playerYaw * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    float rx = diff.x * cos - diff.z * sin;
                    float rz = diff.x * sin + diff.z * cos;
                    offset = new Vector2(rx, rz);
                }
                else
                {
                    offset = new Vector2(diff.x, diff.z);
                }

                float uiX = (offset.x / worldRadius) * mapRadiusPixels;
                float uiY = (offset.y / worldRadius) * mapRadiusPixels;

                Vector2 uiPos = new Vector2(uiX, uiY);
                if (uiPos.sqrMagnitude > mapRadiusPixels * mapRadiusPixels)
                {
                    uiPos = uiPos.normalized * mapRadiusPixels;
                }

                if (blipIndex < ghostBlipPool.Count)
                {
                    RectTransform blip = ghostBlipPool[blipIndex++];
                    if (blip != null && blip.gameObject != null)
                    {
                        blip.anchoredPosition = uiPos;
                        if (!blip.gameObject.activeSelf) blip.gameObject.SetActive(true);
                    }
                }
            }

            for (int i = blipIndex; i < ghostBlipPool.Count; i++)
            {
                if (ghostBlipPool[i] != null && ghostBlipPool[i].gameObject != null && ghostBlipPool[i].gameObject.activeSelf)
                {
                    ghostBlipPool[i].gameObject.SetActive(false);
                }
            }
        }

        // =========================================================================
        // DỰNG GIAO DIỆN UI MINI MAP PHONG CÁCH LA BÀN GOTHIC
        // =========================================================================
        private void BuildMiniMapUI()
        {
            if (gearIconSprite == null) gearIconSprite = CreateGearSprite(64);
            if (expandIconSprite == null) expandIconSprite = CreateExpandSprite(64);

            // Tạo Canvas Mini Map riêng gắn vào DontDestroyOnLoad để hoạt động ổn định từ LobbyScene sang SampleScene
            GameObject cGo = new GameObject("Canvas_MiniMap");
            cGo.transform.SetParent(transform, false);
            Canvas canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 45;

            CanvasScaler scaler = cGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 2.5f;
            scaler.referencePixelsPerUnit = 100f;
            cGo.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 14);

            // Khung chứa chính (MiniMap_Container)
            miniMapRoot = new GameObject("MiniMap_Container");
            miniMapRoot.transform.SetParent(canvas.transform, false);
            mapContainerRect = miniMapRoot.AddComponent<RectTransform>();
            ApplyLayout();

            // 1. Nền tối phía sau bản đồ
            Image bgShadow = miniMapRoot.AddComponent<Image>();
            bgShadow.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);

            // 2. Màn hình hiển thị RenderTexture của Camera Mini Map
            GameObject mapDisplayObj = new GameObject("MapDisplay");
            mapDisplayObj.transform.SetParent(miniMapRoot.transform, false);
            RectTransform displayRect = mapDisplayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.one;
            displayRect.offsetMin = new Vector2(8f, 8f);
            displayRect.offsetMax = new Vector2(-8f, -8f);

            mapRawImage = mapDisplayObj.AddComponent<RawImage>();
            mapRawImage.texture = miniMapRT;
            mapRawImage.color = Color.white;
            mapRawImage.raycastTarget = false;

            // 3. Vòng la bàn chỉ hướng Đông - Tây - Nam - Bắc (Compass Dial)
            GameObject compassObj = new GameObject("CompassRing");
            compassObj.transform.SetParent(miniMapRoot.transform, false);
            compassRingRect = compassObj.AddComponent<RectTransform>();
            compassRingRect.anchorMin = Vector2.zero;
            compassRingRect.anchorMax = Vector2.one;
            compassRingRect.offsetMin = Vector2.zero;
            compassRingRect.offsetMax = Vector2.zero;

            CreateCompassLabel(compassObj, "Label_N", "N", new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Color(1f, 0.32f, 0.32f, 1f), defaultFont, 14);
            CreateCompassLabel(compassObj, "Label_S", "S", new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);
            CreateCompassLabel(compassObj, "Label_E", "E", new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);
            CreateCompassLabel(compassObj, "Label_W", "W", new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Color(0.85f, 0.9f, 1f, 0.9f), defaultFont, 12);

            // 4. Khung viền Cyan bao quanh bản đồ
            GameObject borderObj = new GameObject("CompassBorder");
            borderObj.transform.SetParent(miniMapRoot.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(3f, 3f);
            borderRect.offsetMax = new Vector2(-3f, -3f);

            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0f, 0f, 0f, 0f);
            borderImg.raycastTarget = false;

            Outline borderOutline = borderObj.AddComponent<Outline>();
            borderOutline.effectColor = new Color(0.22f, 0.72f, 0.98f, 0.85f);
            borderOutline.effectDistance = new Vector2(2f, 2f);

            // 5. Thùng chứa các Blip Radar Quái Ma
            GameObject blipGroup = new GameObject("RadarEnemyBlips");
            blipGroup.transform.SetParent(miniMapRoot.transform, false);
            RectTransform blipGroupRect = blipGroup.AddComponent<RectTransform>();
            blipGroupRect.anchorMin = new Vector2(0.5f, 0.5f);
            blipGroupRect.anchorMax = new Vector2(0.5f, 0.5f);
            blipGroupRect.anchoredPosition = Vector2.zero;
            blipGroupRect.sizeDelta = Vector2.zero;
            blipContainer = blipGroup.transform;

            for (int i = 0; i < MAX_RADAR_BLIPS; i++)
            {
                GameObject blip = new GameObject($"GhostBlip_{i}");
                blip.transform.SetParent(blipContainer, false);
                RectTransform bRect = blip.AddComponent<RectTransform>();
                bRect.sizeDelta = new Vector2(12f, 12f);

                Image bImg = blip.AddComponent<Image>();
                bImg.color = new Color(1f, 0.28f, 0.18f, 0.95f);
                bImg.raycastTarget = false;

                Outline bOutline = blip.AddComponent<Outline>();
                bOutline.effectColor = new Color(1f, 0.9f, 0.2f, 0.8f);
                bOutline.effectDistance = new Vector2(1.5f, 1.5f);

                blip.SetActive(false);
                ghostBlipPool.Add(bRect);
            }

            // 6. Icon Người Chơi ở chính giữa
            GameObject playerMarker = new GameObject("PlayerMarker");
            playerMarker.transform.SetParent(miniMapRoot.transform, false);
            playerMarkerRect = playerMarker.AddComponent<RectTransform>();
            playerMarkerRect.anchorMin = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchorMax = new Vector2(0.5f, 0.5f);
            playerMarkerRect.anchoredPosition = Vector2.zero;
            playerMarkerRect.sizeDelta = new Vector2(16f, 20f);

            Image markerImg = playerMarker.AddComponent<Image>();
            markerImg.color = new Color(0.2f, 0.98f, 0.85f, 1f);
            markerImg.raycastTarget = false;

            Outline markerOutline = playerMarker.AddComponent<Outline>();
            markerOutline.effectColor = new Color(0.05f, 0.35f, 0.45f, 0.9f);
            markerOutline.effectDistance = new Vector2(1.5f, 1.5f);

            // 7. Thanh nút bấm ngay bên dưới Mini Map: [ - ] | [ + ] | [ ⚙ Cài Đặt ] | [ ⛶ Phóng To ]
            GameObject toolRow = new GameObject("MiniMapToolBar");
            toolRow.transform.SetParent(miniMapRoot.transform, false);
            RectTransform toolRect = toolRow.AddComponent<RectTransform>();
            toolRect.anchorMin = new Vector2(0f, 0f);
            toolRect.anchorMax = new Vector2(1f, 0f);
            toolRect.pivot = new Vector2(0.5f, 1f);
            toolRect.anchoredPosition = new Vector2(0f, -6f);
            toolRect.sizeDelta = new Vector2(0f, 34f);

            CreateAnchoredToolButton(toolRow, "Btn_ZoomOut", new Vector2(0f, 0f), new Vector2(0.23f, 1f), "−", 18, null, Color.white, defaultFont, ZoomOut);
            CreateAnchoredToolButton(toolRow, "Btn_ZoomIn", new Vector2(0.255f, 0f), new Vector2(0.485f, 1f), "+", 18, null, Color.white, defaultFont, ZoomIn);
            settingsGearBtn = CreateAnchoredToolButton(toolRow, "Btn_Settings", new Vector2(0.51f, 0f), new Vector2(0.74f, 1f), "", 16, gearIconSprite, new Color(1.0f, 0.84f, 0.30f, 1f), defaultFont, OnClickSettingsButton);
            CreateAnchoredToolButton(toolRow, "Btn_Fullscreen", new Vector2(0.765f, 0f), new Vector2(1f, 1f), "", 16, expandIconSprite, new Color(0.75f, 0.94f, 1f, 1f), defaultFont, ToggleFullscreen);

            // 8. Hàng thông số & chế độ xoay ngay dưới thanh nút
            GameObject infoRow = new GameObject("MiniMapInfoRow");
            infoRow.transform.SetParent(miniMapRoot.transform, false);
            RectTransform infoRect = infoRow.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, 0f);
            infoRect.anchorMax = new Vector2(1f, 0f);
            infoRect.pivot = new Vector2(0.5f, 1f);
            infoRect.anchoredPosition = new Vector2(0f, -44f);
            infoRect.sizeDelta = new Vector2(0f, 18f);

            GameObject zoomTextObj = new GameObject("ZoomText");
            zoomTextObj.transform.SetParent(infoRow.transform, false);
            RectTransform zRect = zoomTextObj.AddComponent<RectTransform>();
            zRect.anchorMin = new Vector2(0f, 0f);
            zRect.anchorMax = new Vector2(0.38f, 1f);
            zRect.offsetMin = new Vector2(2f, 0f);
            zRect.offsetMax = Vector2.zero;

            zoomLevelText = zoomTextObj.AddComponent<Text>();
            zoomLevelText.font = defaultFont;
            zoomLevelText.fontSize = 11;
            zoomLevelText.fontStyle = FontStyle.Bold;
            zoomLevelText.alignment = TextAnchor.MiddleLeft;
            zoomLevelText.color = new Color(0.8f, 0.95f, 1f, 0.92f);
            zoomLevelText.raycastTarget = false;
            UpdateZoomText();

            GameObject hintObj = new GameObject("RotateModeButton");
            hintObj.transform.SetParent(infoRow.transform, false);
            RectTransform hRect = hintObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.38f, 0f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.offsetMin = Vector2.zero;
            hRect.offsetMax = new Vector2(-2f, 0f);

            Image hintBg = hintObj.AddComponent<Image>();
            hintBg.color = new Color(0f, 0f, 0f, 0f);
            Button rotateBtn = hintObj.AddComponent<Button>();
            rotateBtn.onClick.AddListener(ToggleRotateMode);

            // Tạo GameObject con riêng cho Text (vì 1 GameObject UI chỉ chứa được 1 thành phần Graphic: Image hoặc Text)
            GameObject hintTxtObj = new GameObject("Text");
            hintTxtObj.transform.SetParent(hintObj.transform, false);
            RectTransform hTxtRect = hintTxtObj.AddComponent<RectTransform>();
            hTxtRect.anchorMin = Vector2.zero;
            hTxtRect.anchorMax = Vector2.one;
            hTxtRect.offsetMin = Vector2.zero;
            hTxtRect.offsetMax = Vector2.zero;

            modeBadgeText = hintTxtObj.AddComponent<Text>();
            modeBadgeText.font = defaultFont;
            modeBadgeText.fontSize = 10;
            modeBadgeText.fontStyle = FontStyle.Bold;
            modeBadgeText.alignment = TextAnchor.MiddleRight;
            modeBadgeText.color = new Color(0.65f, 0.85f, 0.98f, 0.9f);
            modeBadgeText.raycastTarget = false;
            modeBadgeText.text = rotateMapWithPlayer ? "XOAY: THEO HƯỚNG" : "XOAY: HƯỚNG BẮC";
        }

        private static void CreateCompassLabel(GameObject parent, string name, string text, Vector2 anchor, Vector2 pos, Color col, Font font, int size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(24f, 24f);

            Text t = obj.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = col;
            t.text = text;
            t.raycastTarget = false;

            Outline o = obj.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1f, 1f);
        }

        private static Button CreateAnchoredToolButton(
            GameObject parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label,
            int fontSize,
            Sprite iconSprite,
            Color tint,
            Font font,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = btnObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.14f, 0.25f, 0.94f);

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.65f, 0.95f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            if (iconSprite != null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(btnObj.transform, false);
                RectTransform irt = iconObj.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(22f, 22f);

                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = iconSprite;
                iconImg.color = tint;
                iconImg.raycastTarget = false;
            }

            if (!string.IsNullOrEmpty(label))
            {
                GameObject txtObj = new GameObject("Text");
                txtObj.transform.SetParent(btnObj.transform, false);
                RectTransform trt = txtObj.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                Text t = txtObj.AddComponent<Text>();
                t.font = font;
                t.fontSize = fontSize;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = tint;
                t.text = label;
                t.raycastTarget = false;
            }

            return btn;
        }

        private static Sprite CreateGearSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float outerBodyR = size * 0.27f;
            float toothOuterR = size * 0.39f;
            float innerHoleR = size * 0.13f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c;
                    float dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);

                    // 8 răng bánh răng sắc nét
                    float toothWave = Mathf.Cos(ang * 8f);
                    float maxR = toothWave > 0.25f ? toothOuterR : outerBodyR;
                    if (toothWave > -0.15f && toothWave <= 0.25f)
                    {
                        float t = (toothWave + 0.15f) / 0.40f;
                        maxR = Mathf.Lerp(outerBodyR, toothOuterR, t);
                    }

                    float outerAlpha = Mathf.Clamp01(maxR - r + 0.8f);
                    float holeAlpha = Mathf.Clamp01(r - innerHoleR + 0.8f);
                    float alpha = outerAlpha * holeAlpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateExpandSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float pad = size * 0.22f;
            float end = size - 1 - pad;
            float len = size * 0.22f;
            float thick = Mathf.Max(2.2f, size * 0.065f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dxMin = Mathf.Abs(x - pad);
                    float dxMax = Mathf.Abs(x - end);
                    float dyMin = Mathf.Abs(y - pad);
                    float dyMax = Mathf.Abs(y - end);

                    bool inXSpan = (x >= pad - thick * 0.5f && x <= pad + len) || (x <= end + thick * 0.5f && x >= end - len);
                    bool inYSpan = (y >= pad - thick * 0.5f && y <= pad + len) || (y <= end + thick * 0.5f && y >= end - len);

                    bool hBar = (dyMin <= thick * 0.5f || dyMax <= thick * 0.5f) && inXSpan;
                    bool vBar = (dxMin <= thick * 0.5f || dxMax <= thick * 0.5f) && inYSpan;

                    float a = (hBar || vBar) ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}

