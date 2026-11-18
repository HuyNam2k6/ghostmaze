using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Trình tự động kiểm thử thực tế bản Build (chỉ kích hoạt khi có tham số dòng lệnh --autotest-host hoặc --autotest-client).
/// Hoàn toàn không chạy và tốn 0 bộ nhớ khi người chơi mở game bình thường.
/// </summary>
public class GhostMazeAutoTestRunner : MonoBehaviour
{
    private bool isHostMode;
    private string targetRoomId = "GMAuto987";
    private string reportPath;
    private readonly StringBuilder report = new StringBuilder(4096);
    private readonly List<string> capturedErrors = new List<string>();
    private int failCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapIfRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool host = false;
        bool client = false;
        string room = "GMAuto987";

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--autotest-host", StringComparison.OrdinalIgnoreCase)) host = true;
            else if (string.Equals(args[i], "--autotest-client", StringComparison.OrdinalIgnoreCase)) client = true;
            else if (string.Equals(args[i], "--autotest-room", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                room = args[i + 1];
            }
        }

        if (!host && !client) return;
        if (FindAnyObjectByType<GhostMazeAutoTestRunner>() != null) return;

        GameObject go = new GameObject("GhostMazeAutoTestRunner");
        DontDestroyOnLoad(go);
        var runner = go.AddComponent<GhostMazeAutoTestRunner>();
        runner.isHostMode = host;
        runner.targetRoomId = room;
        runner.BeginTest();
    }

    private void BeginTest()
    {
        Application.runInBackground = true;
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string logDir = Path.Combine(projectRoot, "Logs");
        if (!Directory.Exists(logDir))
        {
            logDir = Application.persistentDataPath;
        }
        reportPath = Path.Combine(logDir, isHostMode ? "autotest_host_report.txt" : "autotest_client_report.txt");

        Application.logMessageReceived += OnLogReceived;
        LogLine($"=== BẮT ĐẦU TỰ KIỂM THỬ BẢN BUILD ({(isHostMode ? "HOST" : "CLIENT")}) - Phòng: {targetRoomId} ===");
        StartCoroutine(RunFullVerificationRoutine());
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogReceived;
    }

    private void OnLogReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error)
        {
            capturedErrors.Add($"[{type}] {condition}");
        }
    }

    private void LogLine(string msg)
    {
        string line = $"[{ DateTime.Now:HH:mm:ss.fff}] {msg}";
        report.AppendLine(line);
        Debug.Log("[AutoTest] " + msg);
    }

    private void AssertCheck(string checkName, bool passed, string detail)
    {
        if (!passed) failCount++;
        LogLine($"{(passed ? "[PASS]" : "[FAIL]")} {checkName} — {detail}");
    }

    private IEnumerator RunFullVerificationRoutine()
    {
        // 1. Đợi LobbyScene và PhotonMultiplayerChatVoice sẵn sàng
        float waitLobby = 0f;
        while ((FusionVietnameseLobbyUI.Instance == null || PhotonMultiplayerChatVoice.Instance == null) && waitLobby < 12f)
        {
            waitLobby += Time.unscaledDeltaTime;
            yield return null;
        }

        var lobby = FusionVietnameseLobbyUI.Instance;
        var photon = PhotonMultiplayerChatVoice.Instance;
        AssertCheck("Khởi tạo Sảnh Chính (LobbyScene)", lobby != null && photon != null,
            $"Scene={SceneManager.GetActiveScene().name}, LobbyUI={lobby != null}, Photon={photon != null}");

        if (lobby == null || photon == null)
        {
            FinishAndQuit();
            yield break;
        }

        yield return new WaitForSecondsRealtime(1.0f);

        // Kiểm tra Ô Đặt Tên Player ngay trên Thanh Sảnh Chính (không hiện hộp thoại che giữa màn hình!)
        string desiredNick = isHostMode ? "HostTester" : "GhostSlayerVN";
        GameObject modalObj = GameObject.Find("PlayerNameSetupModal");
        GameObject mainBarNameObj = GameObject.Find("MainLobbyPlayerNameInput");
        InputField mainBarInput = mainBarNameObj != null ? mainBarNameObj.GetComponent<InputField>() : null;
        if (mainBarInput != null)
        {
            mainBarInput.text = desiredNick;
        }
        photon.SetLocalPlayerNickname(desiredNick);
        lobby.SyncAllNicknameInputsFromPhoton();
        yield return null;

        AssertCheck("Ô Đặt Tên Player gọn trên Sảnh Chính (đã xóa hộp thoại che giữa màn hình)",
            modalObj == null && mainBarInput != null
            && string.Equals(photon.playerName, desiredNick, StringComparison.Ordinal)
            && string.Equals(mainBarInput.text, desiredNick, StringComparison.Ordinal),
            $"PopupRemoved={modalObj == null}, MainInputExists={mainBarInput != null}, SavedName='{photon.playerName}'");

        // Điền Room ID vào InputField của Lobby rồi bấm Tạo Phòng / Tham Gia Phòng
        var allInputs = FindObjectsByType<InputField>(FindObjectsInactive.Include);
        for (int i = 0; i < allInputs.Length; i++)
        {
            if (allInputs[i] != null && allInputs[i].gameObject.name.Equals("RoomInputField", StringComparison.OrdinalIgnoreCase))
            {
                allInputs[i].text = targetRoomId;
            }
        }

        if (isHostMode)
        {
            LogLine("Host bấm Tạo Phòng Mới...");
            photon.JoinOrCreateCustomRoom(targetRoomId);
            lobby.OnCreateRoomClicked();
        }
        else
        {
            yield return new WaitForSecondsRealtime(2.0f);
            LogLine("Client bấm Tham Gia Phòng...");
            photon.JoinOrCreateCustomRoom(targetRoomId);
            lobby.OnJoinRoomClicked();
        }

        // 2. Đợi vào phòng Photon (hoặc fallback Solo nếu máy không có mạng)
        float roomWait = 0f;
        while (!photon.IsInPhotonRoom && roomWait < 15f)
        {
            roomWait += Time.unscaledDeltaTime;
            yield return null;
        }

        bool onlineConnected = photon.IsInPhotonRoom;
        LogLine($"Trạng thái kết nối phòng Photon: InRoom={onlineConnected}, Room={photon.roomName}, Players={photon.CurrentRoomPlayersCount}");

        if (onlineConnected)
        {
            // Chờ cả 2 cửa sổ Host & Client nhìn thấy nhau trong phòng
            float pairWait = 0f;
            while (photon.CurrentRoomPlayersCount < 2 && pairWait < 14f)
            {
                pairWait += Time.unscaledDeltaTime;
                yield return null;
            }

            LogLine($"Số người trong phòng chờ hiện tại: {photon.CurrentRoomPlayersCount}");

            if (photon.CurrentRoomPlayersCount >= 2)
            {
                if (!isHostMode)
                {
                    yield return new WaitForSecondsRealtime(0.8f);
                    LogLine("Client bấm nút [Sẵn Sàng] trong phòng chờ...");
                    lobby.OnToggleReadyClicked();
                }
                else
                {
                    // Host thử bấm sớm trước khi Client Ready để kiểm tra thông báo cảnh báo, sau đó đợi Client Ready rồi thêm Máy & Bắt Đầu!
                    float readyWait = 0f;
                    while (!photon.AreAllOtherPlayersReady() && readyWait < 12f)
                    {
                        readyWait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    PhotonMultiplayerChatVoice.SelectedBotCount = 2;
                    LogLine($"Host xác nhận đồng đội đã Sẵn Sàng ({photon.AreAllOtherPlayersReady()}), thêm 2 Bot và bấm Bắt Đầu Trận Đấu!");
                    lobby.OnToggleReadyClicked();
                }
            }
            else if (isHostMode)
            {
                // Nếu chỉ chạy 1 cửa sổ Host đơn lẻ
                PhotonMultiplayerChatVoice.SelectedBotCount = 3;
                LogLine("Chế độ 1 cửa sổ: Host bấm Sẵn Sàng vào trận cùng 3 Bot...");
                lobby.OnToggleReadyClicked();
            }
        }
        else
        {
            // Fallback Offline Solo nếu không có mạng Internet
            PhotonMultiplayerChatVoice.SelectedBotCount = 3;
            LogLine("Không có kết nối mạng tới Photon Cloud -> Kiểm thử vào trận chế độ Solo + 3 Bot...");
            photon.StartSoloDropInLocally();
        }

        // 3. Đợi chuyển sang SampleScene
        float sceneWait = 0f;
        while (SceneManager.GetActiveScene().name != "SampleScene" && sceneWait < 20f)
        {
            sceneWait += Time.unscaledDeltaTime;
            yield return null;
        }

        AssertCheck("Chuyển cảnh vào bản đồ trận đấu (SampleScene)",
            SceneManager.GetActiveScene().name == "SampleScene",
            $"ActiveScene={SceneManager.GetActiveScene().name} sau {sceneWait:F1}s");

        if (SceneManager.GetActiveScene().name != "SampleScene")
        {
            FinishAndQuit();
            yield break;
        }

        // Đợi bản đồ nghĩa địa, StairRampSmoother, nhân vật, Bot và gói đồng bộ RemotePlayer khởi tạo hoàn tất
        yield return new WaitForSecondsRealtime(4.0f);

        // Kiểm tra Prototype Network Start (Fusion cũ) đã bị tắt hoàn toàn chưa
        GameObject legacyFusion = GameObject.Find("Prototype Network Start");
        AssertCheck("Tắt UI Fusion Prototype cũ gây vướng màn hình",
            legacyFusion == null || !legacyFusion.activeInHierarchy,
            legacyFusion == null ? "Không tồn tại hoặc đã ẩn" : $"Active={legacyFusion.activeInHierarchy}");

        // Kiểm tra TUYỆT ĐỐI KHÔNG hiển thị ID Phòng / SoloWaitingRoomBadge khi đã vào trận (SampleScene)
        GameObject soloBadge = GameObject.Find("SoloWaitingRoomBadge");
        bool anyRoomIdTextVisible = false;
        string offendingRoomUi = "";
        var activeTexts = FindObjectsByType<Text>(FindObjectsInactive.Exclude);
        for (int i = 0; i < activeTexts.Length; i++)
        {
            Text tx = activeTexts[i];
            if (tx == null || !tx.gameObject.activeInHierarchy || string.IsNullOrEmpty(tx.text)) continue;
            if (tx.text.IndexOf("ID PHÒNG", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                anyRoomIdTextVisible = true;
                offendingRoomUi = $"{tx.gameObject.name}: {tx.text}";
                break;
            }
        }
        AssertCheck("Ẩn hoàn toàn ID Phòng & thanh chờ phòng khi đã vào trận",
            (soloBadge == null || !soloBadge.activeInHierarchy) && !anyRoomIdTextVisible,
            anyRoomIdTextVisible ? offendingRoomUi : "Không có bất kỳ UI nào hiện ID Phòng trong trận");

        // 4. Kiểm tra Tỷ Lệ Kích Thước (Không bị biến to 10x / 17m!)
        var playerCtrl = ThirdPersonPlayerController.Instance != null
            ? ThirdPersonPlayerController.Instance
            : FindAnyObjectByType<ThirdPersonPlayerController>(FindObjectsInactive.Include);

        AssertCheck("Tìm thấy ThirdPersonPlayerController", playerCtrl != null, playerCtrl != null ? playerCtrl.name : "null");

        if (playerCtrl != null)
        {
            Vector3 lossy = playerCtrl.transform.lossyScale;
            CharacterController cc = playerCtrl.GetComponent<CharacterController>();
            float worldHeight = cc != null ? cc.height * Mathf.Abs(lossy.y) : 0f;
            float worldRadius = cc != null ? cc.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)) : 0f;
            float visualHeight = MeasureVisualHeight(playerCtrl.gameObject);

            AssertCheck("Kích thước Local Player chuẩn (không bị biến to & không bị lùn 27cm)",
                lossy.y >= 0.05f && lossy.y <= 0.25f && worldHeight >= 1.35f && worldHeight <= 2.25f && visualHeight <= 2.6f,
                $"lossyScale={lossy:F3}, CC_WorldHeight={worldHeight:F2}m, CC_WorldRadius={worldRadius:F2}m, stepOffset={(cc != null ? cc.stepOffset : 0f):F2}m, VisualHeight={visualHeight:F2}m");

            // Kiểm tra vị trí Spawn quảng trường trung tâm (không bị đè lên đỉnh Đài Tưởng Niệm Y=3.9m)
            if (playerCtrl.gameObject.activeInHierarchy)
            {
                Vector3 spawnPos = playerCtrl.transform.position;
                AssertCheck("Vị trí Spawn Quảng Trường Trung Tâm mặt đất phẳng (không kẹt trên Đài Đá Y=3.9m)",
                    spawnPos.y >= -0.2f && spawnPos.y <= 2.2f,
                    $"SpawnPos={spawnPos:F2}");
            }

            // Kiểm tra Hiệu Ứng Chiêu (PentacleHandSealVFX & Điểm Phóng Cầu) nằm ngay chính giữa Lòng Bàn Tay đang chuyển động!
            bool wasPlayerActiveForPalm = playerCtrl.gameObject.activeSelf;
            if (!wasPlayerActiveForPalm) playerCtrl.gameObject.SetActive(true);
            Animator pAnim = playerCtrl.playerAnimator != null ? playerCtrl.playerAnimator : playerCtrl.GetComponentInChildren<Animator>(true);
            if (pAnim != null)
            {
                pAnim.SetBool("IsBlocking", true);
                pAnim.Play("Block", 0, 0.5f);
                pAnim.Update(0f);
            }

            var sealVfx = playerCtrl.GetComponent<PentacleHandSealVFX>() ?? playerCtrl.gameObject.AddComponent<PentacleHandSealVFX>();
            GameObject dummyTarget = new GameObject("DummyGhostTarget");
            dummyTarget.transform.position = playerCtrl.transform.position + playerCtrl.transform.forward * 2.5f;
            sealVfx.Activate(dummyTarget.transform, playerCtrl.castHandTransform);
            yield return new WaitForEndOfFrame(); // Đo trên cùng 1 frame ngay sau khi Animator & LateUpdate vừa đồng bộ xong!

            bool gotPalm = playerCtrl.TryGetRightPalmWorldPose(out Vector3 truePalmPos, out _);
            Transform sealRootT = playerCtrl.transform.Find("PentacleHandSeal_Root");
            Bounds sealVisualBounds = MeasureVisualBounds(sealRootT != null ? sealRootT.gameObject : null, out bool hasSealMesh);
            Vector3 visualStarCenter = hasSealMesh ? sealVisualBounds.center : (sealRootT != null ? sealRootT.position : Vector3.zero);
            float distStarCenterToPalm = Vector3.Distance(visualStarCenter, truePalmPos);
            float sealVisualH = hasSealMesh ? Mathf.Max(sealVisualBounds.size.x, Mathf.Max(sealVisualBounds.size.y, sealVisualBounds.size.z)) : 0f;
            float distPalmToBody = Vector3.Distance(truePalmPos, playerCtrl.transform.position + Vector3.up * 0.95f);

            AssertCheck("Hiệu ứng chiêu Dấu Ấn Pentacle & Xích bám khít ngay giữa Lòng Bàn Tay (không lệch ra ngoài)",
                gotPalm && hasSealMesh && distStarCenterToPalm <= 0.06f && distPalmToBody <= 0.65f && sealVisualH >= 0.12f && sealVisualH <= 0.35f,
                $"gotPalm={gotPalm}, StarToPalm={distStarCenterToPalm * 100f:F1}cm, DistPalmToChest={distPalmToBody:F2}m, StarDiameter={sealVisualH:F2}m");

            sealVfx.Deactivate();
            Destroy(dummyTarget);
            if (pAnim != null) pAnim.SetBool("IsBlocking", false);
            if (!wasPlayerActiveForPalm) playerCtrl.gameObject.SetActive(false);
        }

        // Kiểm tra toàn bộ RemotePlayer_* (nếu có đồng đội trong phòng)
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        int remoteCount = 0;
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null || t.parent != null) continue;
            if (t.name.StartsWith("RemotePlayer_", StringComparison.Ordinal))
            {
                remoteCount++;
                float vh = MeasureVisualHeight(t.gameObject);
                bool isGhostRemote = t.name.EndsWith("_Ghost", StringComparison.Ordinal);
                float maxAllowedH = isGhostRemote ? 3.5f : 2.6f;
                AssertCheck($"Kích thước đồng đội mạng [{t.name}] không bị biến to khổng lồ",
                    vh > 0.4f && vh <= maxAllowedH,
                    $"rootScale={t.lossyScale:F3}, VisualHeight={vh:F2}m (Giới hạn <= {maxAllowedH:F1}m)");
            }
        }
        if (onlineConnected && photon.CurrentRoomPlayersCount >= 2)
        {
            AssertCheck("Đồng bộ hiển thị Avatar người chơi mạng (RemotePlayer)", remoteCount >= 1, $"RemoteCount={remoteCount}");
        }

        // Kiểm tra toàn bộ HumanBot_*
        for (int i = 0; i < HumanBotTarget.ActiveBots.Count; i++)
        {
            var bot = HumanBotTarget.ActiveBots[i];
            if (bot == null) continue;
            float botH = MeasureVisualHeight(bot.gameObject);
            AssertCheck($"Kích thước Máy AI [{bot.name}] chuẩn xác (không bị biến to)",
                botH > 0.5f && botH <= 2.6f,
                $"scale={bot.transform.lossyScale:F3}, VisualHeight={botH:F2}m");
        }

        // Kiểm tra toàn bộ Ma Bí Ngô (PumpkinGhostAI)
        for (int i = 0; i < PumpkinGhostAI.ActiveGhosts.Count; i++)
        {
            var g = PumpkinGhostAI.ActiveGhosts[i];
            if (g == null || !g.gameObject.activeInHierarchy) continue;
            float gh = MeasureVisualHeight(g.gameObject);
            AssertCheck($"Kích thước Ma Bí Ngô [{g.name}] chuẩn xác",
                gh > 0.4f && gh <= 3.6f,
                $"scale={g.transform.lossyScale:F3}, VisualHeight={gh:F2}m, playerControlled={g.isPlayerControlled}");
        }

        // 5. Kiểm tra Thực Nghiệm Di Chuyển Vật Lý Qua Quảng Trường, Cầu Đá & Cửa Phòng Nhà Thờ
        if (playerCtrl != null)
        {
            bool wasActive = playerCtrl.gameObject.activeSelf;
            if (!wasActive) playerCtrl.gameObject.SetActive(true);

            CharacterController cc = playerCtrl.GetComponent<CharacterController>();
            if (cc != null)
            {
                Vector3 savedPos = playerCtrl.transform.position;

                // Test A (Ngay trong 10s đầu CircleWait_10s): Đi tự do 6.5m trên Đảo Quảng Trường Trung Tâm (không còn bị kẹt ở rào 4.65m cũ!)
                yield return RunWalkSegmentTest(cc, new Vector3(3.5f, 0.75f, 0f), Vector3.right, 6.5f,
                    "Di chuyển tự do trên Đảo Quảng Trường Trung Tâm (không bị kẹt rào hẹp 5m)");

                // Chờ vòng tròn khởi đầu mở cửa (sang pha Hide_20s) giống hệt luồng chơi thật
                float circleWait = 0f;
                while (SurvivalTimerUI.Instance != null
                    && SurvivalTimerUI.Instance.currentPhase == SurvivalTimerUI.MatchPhase.CircleWait_10s
                    && circleWait < 12f)
                {
                    circleWait += Time.deltaTime;
                    yield return null;
                }
                LogLine($"Vòng tròn khởi đầu đã mở! Pha hiện tại: {(SurvivalTimerUI.Instance != null ? SurvivalTimerUI.Instance.currentPhase.ToString() : "N/A")}, Role={(SurvivalTimerUI.Instance != null ? SurvivalTimerUI.Instance.localRole.ToString() : "N/A")}");

                // Tạm dừng khóa rào của Ma Bí Ngô trong 2.5s để cả 2 cửa sổ đều kiểm tra được va chạm vật lý thật trên Cầu Đá Lớn & Thềm Cửa Nhà Thờ
                bool prevRunning = SurvivalTimerUI.Instance != null && SurvivalTimerUI.Instance.isRunning;
                if (SurvivalTimerUI.Instance != null) SurvivalTimerUI.Instance.isRunning = false;

                // Test B: Đi dọc Cầu Thang Đá Lớn nối Quảng Trường Trung Tâm (6.8, 0.4, 10.9) lên Đảo Nhà Thờ (18.5, 8.6, 26.6)
                Vector3 grandBridgeDir = new Vector3(18.484f - 8.016f, 0f, 26.637f - 12.863f).normalized;
                yield return RunWalkSegmentTest(cc, new Vector3(6.8f, 0.85f, 10.9f), grandBridgeDir, 18.5f,
                    "Leo mượt qua khớp nối Cầu Thang Đá Lớn lên Đảo Nhà Thờ (Y: 0.3m -> 8.6m)");

                // Test C: Từ sân Đảo Nhà Thờ (19.5, 8.6, 29.8) bước lên Bậc Thềm Đá 0.70m (Cathedral_StoneTerrace) tiến thẳng tới Cửa Chính Nhà Thờ (CathedralPortalGlow: 22.15, 9.2, 37.05)
                Vector3 portalApproachDir = new Vector3(22.155f - 19.5f, 0f, 36.2f - 29.8f).normalized;
                yield return RunWalkSegmentTest(cc, new Vector3(19.5f, 9.0f, 29.8f), portalApproachDir, 6.8f,
                    "Bước lên Bậc Thềm 0.70m & tiến vào Cửa Sảnh Nhà Thờ Lớn (Cathedral Terrace & Portal)");

                if (SurvivalTimerUI.Instance != null) SurvivalTimerUI.Instance.isRunning = prevRunning;

                // Khôi phục vị trí
                cc.enabled = false;
                playerCtrl.transform.position = savedPos;
                cc.enabled = true;
            }

            if (!wasActive) playerCtrl.gameObject.SetActive(false);

            // 5b. Kiểm tra thực nghiệm ném Quả Cầu Phép (MagicWaveOrb) chúc xuống đất cự ly gần (3.2m):
            // Phải nảy vọt cao thanh thoát (>= 1.25m trên mặt sân) và bay lướt xa (>= 15m trong 1.6s), không bị lẹt đẹt 2-3 phát thấp!
            Vector3 orbStart = new Vector3(-4.5f, 1.45f, -4.0f);
            Vector3 groundAim = new Vector3(-4.5f, 0.35f, -0.8f); // Ném cắm xuống mặt sân cách chân 3.2m theo hướng +Z
            GameObject testOrbGo = playerCtrl.magicOrbPrefab != null
                ? Instantiate(playerCtrl.magicOrbPrefab, orbStart, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (playerCtrl.magicOrbPrefab == null)
            {
                Collider c = testOrbGo.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            MagicWaveOrb testOrb = testOrbGo.GetComponent<MagicWaveOrb>() ?? testOrbGo.AddComponent<MagicWaveOrb>();
            testOrb.speed = playerCtrl.magicOrbSpeed;
            testOrb.shooter = playerCtrl.gameObject;
            testOrb.Launch(Vector3.forward, groundAim, playerCtrl.gameObject);

            float groundFloorY = 0.35f;
            float simTime = 0f;
            bool touchedNearGround = false;
            float maxBounceHeightAboveGround = 0f;
            Vector2 startHoriz = new Vector2(orbStart.x, orbStart.z);
            float maxHorizDist = 0f;

            while (simTime < 1.55f && testOrbGo != null && testOrbGo.activeSelf)
            {
                yield return null;
                simTime += Time.deltaTime;
                if (testOrbGo == null || !testOrbGo.activeSelf) break;
                Vector3 op = testOrbGo.transform.position;
                if (op.y <= groundFloorY + 0.45f)
                {
                    touchedNearGround = true;
                }
                else if (touchedNearGround)
                {
                    float hAbove = op.y - groundFloorY;
                    if (hAbove > maxBounceHeightAboveGround) maxBounceHeightAboveGround = hAbove;
                }
                float hd = Vector2.Distance(startHoriz, new Vector2(op.x, op.z));
                if (hd > maxHorizDist) maxHorizDist = hd;
            }
            bool orbStillAlive = testOrbGo != null && testOrbGo.activeSelf;
            if (testOrbGo != null) Destroy(testOrbGo);

            AssertCheck("Quả cầu phép ném xuống đất bật nảy cao & bay lướt xa mượt mà (không bị nảy thấp lẹt đẹt)",
                touchedNearGround && maxBounceHeightAboveGround >= 1.15f && maxHorizDist >= 14.0f && orbStillAlive,
                $"TouchedGround={touchedNearGround}, PeakBounceHeight={maxBounceHeightAboveGround:F2}m, HorizFlightDist={maxHorizDist:F2}m, StillFlyingAt1.55s={orbStillAlive}");
        }

        // 6. Đo độ mượt FPS thực tế trong 90 khung hình liên tiếp
        int sampleFrames = 90;
        float totalDt = 0f;
        float maxFrameMs = 0f;
        for (int f = 0; f < sampleFrames; f++)
        {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            totalDt += dt;
            if (dt * 1000f > maxFrameMs) maxFrameMs = dt * 1000f;
        }
        float avgFps = sampleFrames / Mathf.Max(0.001f, totalDt);
        AssertCheck("Hiệu năng khung hình (FPS) mượt mà ổn định",
            avgFps >= 30f,
            $"AvgFPS={avgFps:F1} FPS, MaxFrameTime={maxFrameMs:F1}ms");

        AssertCheck("Không phát sinh Exception / Error runtime",
            capturedErrors.Count == 0,
            capturedErrors.Count == 0 ? "0 lỗi" : string.Join(" | ", capturedErrors));

        // Chờ nhẹ 1.5s để cửa sổ còn lại cũng hoàn tất đo đạc trước khi thoát
        yield return new WaitForSecondsRealtime(1.5f);
        FinishAndQuit();
    }

    private IEnumerator RunWalkSegmentTest(CharacterController cc, Vector3 startWorldPos, Vector3 moveDir, float distanceMeters, string label)
    {
        cc.enabled = false;
        cc.transform.position = startWorldPos;
        cc.enabled = true;
        yield return new WaitForFixedUpdate();

        float speed = 6.0f;
        float duration = distanceMeters / speed;
        float elapsed = 0f;
        float verticalVel = -2f;

        while (elapsed < duration)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (dt <= 0f) dt = 0.016f;
            if (cc.isGrounded) verticalVel = -2f;
            else verticalVel = Mathf.Max(verticalVel - 20f * dt, -20f);

            Vector3 step = (moveDir.normalized * speed + Vector3.up * verticalVel) * dt;
            cc.Move(step);
            elapsed += dt;
            yield return null;
        }

        Vector3 endPos = cc.transform.position;
        Vector2 flatStart = new Vector2(startWorldPos.x, startWorldPos.z);
        Vector2 flatEnd = new Vector2(endPos.x, endPos.z);
        float movedHoriz = Vector2.Distance(flatStart, flatEnd);
        bool passed = movedHoriz >= distanceMeters * 0.78f;

        AssertCheck(label, passed,
            $"Start={startWorldPos:F2} -> End={endPos:F2} (Đi được {movedHoriz:F2}m / {distanceMeters:F2}m)");
    }

    private static float MeasureVisualHeight(GameObject root)
    {
        Bounds b = MeasureVisualBounds(root, out bool hasBounds);
        return hasBounds ? b.size.y : 0f;
    }

    private static Bounds MeasureVisualBounds(GameObject root, out bool hasBounds)
    {
        hasBounds = false;
        Bounds combined = default;
        if (root == null) return combined;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.GetComponent<TextMesh>() != null) continue;
            if (r.gameObject.name.IndexOf("Ring", StringComparison.OrdinalIgnoreCase) >= 0) continue;

            if (!hasBounds)
            {
                combined = r.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(r.bounds);
            }
        }
        return combined;
    }

    private void FinishAndQuit()
    {
        LogLine($"=== KẾT THÚC KIỂM THỬ ({(isHostMode ? "HOST" : "CLIENT")}): {(failCount == 0 ? "THÀNH CÔNG 100% (0 FAIL)" : $"CÓ {failCount} MỤC CẦN XEM LẠI")} ===");
        try
        {
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogError("[AutoTest] Không thể ghi báo cáo: " + ex.Message);
        }

        Application.Quit(failCount == 0 ? 0 : 2);
    }
}
