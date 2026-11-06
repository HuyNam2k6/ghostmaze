using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class PrincePlayerSetup
{
    public const string FBX_PATH = "Assets/Player/PrinceGreen/PrinceGreen.fbx";
    public const string TEX_PATH = "Assets/Player/PrinceGreen/textures/base bg elf.png";
    public const string MAT_PATH = "Assets/Player/PrinceGreen/M_PrinceGreen.mat";
    public const string CONTROLLER_PATH = "Assets/Player/PrinceGreen/PrinceGreen_Animator.controller";
    public const string ORB_PREFAB_PATH = "Assets/Player/PrinceGreen/MagicWaveOrb.prefab";
    public const string ORB_MAT_PATH = "Assets/Player/PrinceGreen/M_MagicWaveOrb.mat";
    public const string PLAYER_PREFAB_PATH = "Assets/Player/PrinceGreen/Player_PrinceGreen.prefab";

    static PrincePlayerSetup()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += () =>
        {
            ValidateSceneCharacterControllers();

            // Chỉ tự động dựng Prefab lần đầu nếu Prefab chưa tồn tại (tránh ghi đè chỉnh sửa tay của người dùng trên Scene)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH) == null &&
                !SessionState.GetBool("PrinceSetupTriggered_v3", false))
            {
                SessionState.SetBool("PrinceSetupTriggered_v3", true);
                SetupPrincePlayer();
            }
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
        {
            ValidateSceneCharacterControllers();
        }
    }

    /// <summary>
    /// Tự động đồng bộ StepOffset & SkinWidth theo tỷ lệ Transform thực tế (lossyScale) trên Scene
    /// trước khi vào Play Mode, triệt tiêu hoàn toàn lỗi CharacterController.cpp line 599 mà không đổi chiều cao nhân vật.
    /// </summary>
    public static void ValidateSceneCharacterControllers()
    {
        CharacterController[] controllers = Object.FindObjectsByType<CharacterController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController cc = controllers[i];
            if (cc == null) continue;

            Vector3 lossy = cc.transform.lossyScale;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossy.y));
            float scaleXZ = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)));

            float scaledRadius = cc.radius * scaleXZ;
            float scaledCylinderHeight = Mathf.Max(0f, cc.height * scaleY - scaledRadius * 2f);
            float maxAllowedStepOffset = scaledCylinderHeight + scaledRadius * 2f;

            bool modified = false;
            if (cc.stepOffset > maxAllowedStepOffset * 0.9f)
            {
                cc.stepOffset = Mathf.Max(0.01f, maxAllowedStepOffset * 0.75f);
                modified = true;
            }

            if (scaledRadius > 0.005f && cc.skinWidth > scaledRadius * 0.8f)
            {
                cc.skinWidth = Mathf.Max(0.005f, scaledRadius * 0.25f);
                modified = true;
            }

            if (modified && !Application.isPlaying)
            {
                EditorUtility.SetDirty(cc);
            }
        }
    }

    [MenuItem("Tools/Setup Prince Player (Complete Animations & Skills)")]
    public static void SetupPrincePlayer()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. Tạo Material quả cầu ma thuật URP Lit phát sáng
        Material orbMat = EnsureOrbMaterial();

        // 2. Tạo Prefab Quả Cầu Phép Thuật MagicWaveOrb
        GameObject orbPrefab = EnsureOrbPrefab(orbMat);

        // 3. Tạo Animator Controller đầy đủ 7 Animations cho PrinceGreen
        AnimatorController animController = BuildPrinceAnimatorController();

        // 4. Tạo hoặc thay thế Player_ThirdPerson trong Scene
        GameObject playerGo = SetupPlayerInstanceInScene(animController, orbPrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#2ECC71>[PrincePlayerSetup] Đã thay thế thành công Player bằng PrinceGreen với đầy đủ 7 hoạt ảnh, kỹ năng Block (giữ ma 3s) và Rising Wave (ném cầu đẩy lùi 3m & đứng im 5s)!</color>");
    }

    private static Material EnsureOrbMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ORB_MAT_PATH);
        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            mat = new Material(urpLit);
            mat.name = "M_MagicWaveOrb";
            mat.color = new Color(0.25f, 0.95f, 0.75f, 1f);

            // Bật Emission
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.2f, 1.0f, 0.8f) * 3.5f);

            AssetDatabase.CreateAsset(mat, ORB_MAT_PATH);
        }
        return mat;
    }

    private static GameObject EnsureOrbPrefab(Material orbMat)
    {
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ORB_PREFAB_PATH);
        if (existingPrefab != null) return existingPrefab;

        GameObject tempOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tempOrb.name = "MagicWaveOrb";
        tempOrb.transform.localScale = Vector3.one * 0.45f;
        Object.DestroyImmediate(tempOrb.GetComponent<Collider>()); // Dùng SphereCastNonAlloc

        if (orbMat != null)
        {
            tempOrb.GetComponent<MeshRenderer>().sharedMaterial = orbMat;
        }

        // Đèn phát sáng ma thuật
        GameObject lightObj = new GameObject("OrbLight");
        lightObj.transform.SetParent(tempOrb.transform, false);
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.25f, 0.95f, 0.8f);
        l.intensity = 3.5f;
        l.range = 6.5f;
        l.shadows = LightShadows.None;

        // Script MagicWaveOrb
        MagicWaveOrb script = tempOrb.AddComponent<MagicWaveOrb>();
        script.speed = 15f;
        script.knockbackDistance = 3.0f;
        script.stunDuration = 5.0f;
        script.hitRadius = 0.35f;
        script.orbLight = l;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tempOrb, ORB_PREFAB_PATH);
        Object.DestroyImmediate(tempOrb);

        return prefab;
    }

    private static AnimatorController BuildPrinceAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        }

        // Xóa parameters cũ
        while (controller.parameters.Length > 0)
        {
            controller.RemoveParameter(0);
        }

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsBlocking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("RisingWave", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("TakeHit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        // Load clips từ FBX
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
        Dictionary<string, AnimationClip> clipMap = new Dictionary<string, AnimationClip>();

        foreach (Object asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                clipMap[clip.name] = clip;
            }
        }

        AnimationClip FindClip(string keyword)
        {
            foreach (var kv in clipMap)
            {
                if (kv.Key.ToUpperInvariant().Contains(keyword.ToUpperInvariant()))
                {
                    return kv.Value;
                }
            }
            return null;
        }

        AnimationClip clipIdle       = FindClip("Magic Idle") ?? FindClip("Idle");
        AnimationClip clipRun        = FindClip("Run");
        AnimationClip clipJump       = FindClip("Jump");
        AnimationClip clipBlock      = FindClip("Block");
        AnimationClip clipRisingWave = FindClip("rising wave") ?? FindClip("RisingWave");
        AnimationClip clipTakeHit    = FindClip("Take hit") ?? FindClip("TakeHit");
        AnimationClip clipDeath      = FindClip("Death");

        AnimatorStateMachine rootSm = controller.layers[0].stateMachine;

        // Xóa các state cũ
        var existingStates = rootSm.states;
        for (int i = existingStates.Length - 1; i >= 0; i--)
        {
            rootSm.RemoveState(existingStates[i].state);
        }

        AnimatorState stIdle       = rootSm.AddState("Idle", new Vector3(250, 100, 0));
        AnimatorState stRun        = rootSm.AddState("Run", new Vector3(500, 100, 0));
        AnimatorState stJump       = rootSm.AddState("Jump", new Vector3(250, -50, 0));
        AnimatorState stBlock      = rootSm.AddState("Block", new Vector3(250, 250, 0));
        AnimatorState stRisingWave = rootSm.AddState("RisingWave", new Vector3(500, -50, 0));
        AnimatorState stTakeHit    = rootSm.AddState("TakeHit", new Vector3(500, 250, 0));
        AnimatorState stDeath      = rootSm.AddState("Death", new Vector3(750, 100, 0));

        stIdle.motion       = clipIdle;
        stRun.motion        = clipRun;
        stJump.motion       = clipJump;
        stBlock.motion      = clipBlock;
        stRisingWave.motion = clipRisingWave;
        stTakeHit.motion    = clipTakeHit;
        stDeath.motion      = clipDeath;

        rootSm.defaultState = stIdle;

        // Transition: Idle <-> Run
        var tIdleToRun = stIdle.AddTransition(stRun);
        tIdleToRun.hasExitTime = false;
        tIdleToRun.duration = 0.15f;
        tIdleToRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var tRunToIdle = stRun.AddTransition(stIdle);
        tRunToIdle.hasExitTime = false;
        tRunToIdle.duration = 0.15f;
        tRunToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Transition: AnyState -> Jump
        var tAnyToJump = rootSm.AddAnyStateTransition(stJump);
        tAnyToJump.hasExitTime = false;
        tAnyToJump.duration = 0.1f;
        tAnyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        var tJumpToIdle = stJump.AddTransition(stIdle);
        tJumpToIdle.hasExitTime = true;
        tJumpToIdle.exitTime = 0.85f;
        tJumpToIdle.duration = 0.15f;

        // Transition: Block (Khi giữ phím Block)
        var tAnyToBlock = rootSm.AddAnyStateTransition(stBlock);
        tAnyToBlock.hasExitTime = false;
        tAnyToBlock.duration = 0.1f;
        tAnyToBlock.AddCondition(AnimatorConditionMode.If, 0, "IsBlocking");

        var tBlockToIdle = stBlock.AddTransition(stIdle);
        tBlockToIdle.hasExitTime = false;
        tBlockToIdle.duration = 0.15f;
        tBlockToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsBlocking");

        // Transition: Rising Wave
        var tAnyToWave = rootSm.AddAnyStateTransition(stRisingWave);
        tAnyToWave.hasExitTime = false;
        tAnyToWave.duration = 0.1f;
        tAnyToWave.AddCondition(AnimatorConditionMode.If, 0, "RisingWave");

        var tWaveToIdle = stRisingWave.AddTransition(stIdle);
        tWaveToIdle.hasExitTime = true;
        tWaveToIdle.exitTime = 0.9f;
        tWaveToIdle.duration = 0.15f;

        // Transition: Take Hit
        var tAnyToHit = rootSm.AddAnyStateTransition(stTakeHit);
        tAnyToHit.hasExitTime = false;
        tAnyToHit.duration = 0.05f;
        tAnyToHit.AddCondition(AnimatorConditionMode.If, 0, "TakeHit");

        var tHitToIdle = stTakeHit.AddTransition(stIdle);
        tHitToIdle.hasExitTime = true;
        tHitToIdle.exitTime = 0.85f;
        tHitToIdle.duration = 0.15f;

        // Transition: Death
        var tAnyToDeath = rootSm.AddAnyStateTransition(stDeath);
        tAnyToDeath.hasExitTime = false;
        tAnyToDeath.duration = 0.1f;
        tAnyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject SetupPlayerInstanceInScene(AnimatorController animController, GameObject orbPrefab)
    {
        GameObject playerGo = GameObject.Find("Player_ThirdPerson");
        Vector3 spawnPos = new Vector3(2.5f, 0.45f, 7.5f);
        Quaternion spawnRot = Quaternion.Euler(0f, 35f, 0f);

        if (playerGo == null)
        {
            playerGo = new GameObject("Player_ThirdPerson");
            playerGo.transform.position = spawnPos;
            playerGo.transform.rotation = spawnRot;
        }

        playerGo.tag = "Player";

        // Xóa triệt để toàn bộ con cũ (Visuals, Body, Robe, Head, HandheldLantern...)
        for (int i = playerGo.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = playerGo.transform.GetChild(i);
            Object.DestroyImmediate(child.gameObject);
        }

        // Nạp mô hình PrinceGreen FBX
        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
        if (fbxPrefab == null)
        {
            Debug.LogError("[PrincePlayerSetup] Không tìm thấy FBX tại: " + FBX_PATH);
            return playerGo;
        }

        GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, playerGo.transform);
        modelInst.name = "Model";
        modelInst.transform.localPosition = Vector3.zero;
        modelInst.transform.localRotation = Quaternion.identity;
        modelInst.transform.localScale = Vector3.one * 0.72f; // Giảm độ to của Player xuống cân đối đẹp mắt

        // Gán Material URP Lit PrinceGreen
        Material princeMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (princeMat != null)
        {
            Renderer[] renderers = modelInst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = princeMat;
            }
        }

        // Gán Animator
        Animator animator = modelInst.GetComponent<Animator>();
        if (animator == null) animator = modelInst.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // CharacterController tối ưu cho kích thước 0.72 mới (cao 1.55m, bán kính 0.30m)
        CharacterController cc = playerGo.GetComponent<CharacterController>();
        if (cc == null) cc = playerGo.AddComponent<CharacterController>();
        cc.radius = 0.30f;
        cc.height = 1.55f;
        cc.center = new Vector3(0f, 0.775f, 0f);
        cc.slopeLimit = 46f;
        cc.stepOffset = 0.35f;

        // ThirdPersonPlayerController
        ThirdPersonPlayerController controller = playerGo.GetComponent<ThirdPersonPlayerController>();
        if (controller == null) controller = playerGo.AddComponent<ThirdPersonPlayerController>();
        controller.playerScale = 0.72f;
        controller.colliderRadius = 0.30f;
        controller.colliderHeight = 1.55f;
        controller.colliderCenter = new Vector3(0f, 0.775f, 0f);
        controller.slopeLimit = 46f;
        controller.stepOffset = 0.35f;
        controller.playerAnimator = animator;
        controller.respawnPoint = spawnPos;
        controller.magicOrbPrefab = orbPrefab;
        controller.magicOrbKnockback = 3.0f;
        controller.magicOrbStunDuration = 5.0f;
        controller.ghostHoldDuration = 3.0f;
        controller.blockRange = 3.5f;

        // Tạo hiệu ứng ánh sáng khiên đỡ đòn (Aura Shield Light)
        GameObject shieldObj = new GameObject("BlockShieldLight");
        shieldObj.transform.SetParent(playerGo.transform, false);
        shieldObj.transform.localPosition = new Vector3(0f, 1.0f, 0.4f);
        Light shieldLight = shieldObj.AddComponent<Light>();
        shieldLight.type = LightType.Point;
        shieldLight.color = new Color(0.2f, 0.7f, 1.0f);
        shieldLight.intensity = 3.0f;
        shieldLight.range = 4.5f;
        shieldLight.shadows = LightShadows.None;
        shieldLight.enabled = false;
        controller.blockShieldLight = shieldLight;

        // PlayerHealth
        PlayerHealth health = playerGo.GetComponent<PlayerHealth>();
        if (health == null) health = playerGo.AddComponent<PlayerHealth>();

        // AudioSource
        AudioSource audioSource = playerGo.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = playerGo.AddComponent<AudioSource>();

        // Cập nhật targetPlayer cho PumpkinGhostAI nếu có trong Scene
        PumpkinGhostAI ghostAi = Object.FindAnyObjectByType<PumpkinGhostAI>();
        if (ghostAi != null)
        {
            ghostAi.targetPlayer = playerGo.transform;
        }

        // Lưu thành Prefab để tái sử dụng
        PrefabUtility.SaveAsPrefabAssetAndConnect(playerGo, PLAYER_PREFAB_PATH, InteractionMode.AutomatedAction);

        return playerGo;
    }
}
