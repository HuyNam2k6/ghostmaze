using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class PumpkinGhostSetup
{
    public const string FBX_PATH = "Assets/Enemy/HalloweenGhostKid/source/gaminHalloween-12-testshapeKeysNLA.fbx";
    public const string TEX_PATH = "Assets/Enemy/HalloweenGhostKid/textures/gaminHalloween_tex01_002.png";
    public const string MAT_PATH = "Assets/Enemy/HalloweenGhostKid/M_PumpkinGhost.mat";
    public const string CONTROLLER_PATH = "Assets/Enemy/HalloweenGhostKid/PumpkinGhost_Animator.controller";
    public const string CONFIG_PATH = "Assets/Settings/PumpkinGhostConfig.asset";
    public const string PREFAB_PATH = "Assets/Enemy/HalloweenGhostKid/PumpkinGhost_Enemy.prefab";

    [MenuItem("Tools/Setup Pumpkin Ghost (Move & Chase Player - No Hardcode)")]
    public static void SetupPumpkinGhost()
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 1. Đảm bảo Animation Clips có thiết lập Loop
        EnsureFBXAnimationLoops();

        // 2. Tạo Material chuẩn URP Lit
        Material ghostMat = EnsureGhostMaterial();

        // 3. Tạo Animator Controller với đủ 7 clips từ FBX
        AnimatorController animController = BuildAnimatorController();

        // 4. Tạo hoặc tải ScriptableObject cấu hình không fix cứng code
        PumpkinGhostConfig config = EnsureGhostConfig();

        // 5. Tìm hoặc tạo Player
        GameObject playerGo = GameObject.Find("Player_ThirdPerson");
        if (playerGo == null)
        {
            playerGo = PlayerSpawner.SpawnPlayer();
        }

        // 6. Tạo Prefab PumpkinGhost
        GameObject ghostInstance = BuildGhostPrefabAndInstance(ghostMat, animController, config, playerGo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=#2ECC71>[PumpkinGhostSetup] Đã thiết lập thành công Con Ma Bí Ngô! Mặc định săn đuổi Player, không fix cứng bất kỳ thông số Collider hay cảm biến nào!</color>");
    }

    private static void EnsureFBXAnimationLoops()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FBX_PATH) as ModelImporter;
        if (importer == null) return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            string n = clips[i].name.ToUpperInvariant();
            bool shouldLoop = n.Contains("IDLE") || n.Contains("WALK") || n.Contains("RUN") || n.Contains("OUTOFBREATH");
            if (clips[i].loopTime != shouldLoop)
            {
                clips[i].loopTime = shouldLoop;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static Material EnsureGhostMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_PATH);

        if (mat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, MAT_PATH);
        }

        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", tex);
            mat.SetColor("_EmissionColor", new Color(0.40f, 0.44f, 0.52f, 1f));
        }
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.20f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static AnimatorController BuildAnimatorController()
    {
        if (File.Exists(CONTROLLER_PATH))
        {
            AssetDatabase.DeleteAsset(CONTROLLER_PATH);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);

        // Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsChasing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hide", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("UnHide", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stun", AnimatorControllerParameterType.Trigger);

        // Load clips
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
                if (kv.Key.StartsWith("gaminArmature|") && kv.Key.ToUpperInvariant().Contains(keyword.ToUpperInvariant()))
                {
                    return kv.Value;
                }
            }
            foreach (var kv in clipMap)
            {
                if (kv.Key.ToUpperInvariant().Contains(keyword.ToUpperInvariant()))
                {
                    return kv.Value;
                }
            }
            return null;
        }

        AnimationClip clipIdle   = FindClip("IDLE");
        AnimationClip clipWalk   = FindClip("WALK");
        AnimationClip clipRun    = FindClip("RUN");
        AnimationClip clipAttack = FindClip("ATTACK");
        AnimationClip clipJump   = FindClip("JUMP");
        AnimationClip clipHide   = FindClip("HIDE");
        AnimationClip clipBreath = FindClip("OUTOFBREATH");

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState stIdle   = sm.AddState("Idle", new Vector3(260, 60, 0));
        AnimatorState stWalk   = sm.AddState("Walk", new Vector3(260, 160, 0));
        AnimatorState stRun    = sm.AddState("Run", new Vector3(520, 110, 0));
        AnimatorState stAttack = sm.AddState("Attack", new Vector3(520, -20, 0));
        AnimatorState stJump   = sm.AddState("Jump", new Vector3(520, 240, 0));
        AnimatorState stBreath = sm.AddState("OutOfBreath", new Vector3(770, 240, 0));
        AnimatorState stHide   = sm.AddState("Hide", new Vector3(260, -60, 0));

        stIdle.motion   = clipIdle;
        stWalk.motion   = clipWalk;
        stRun.motion    = clipRun;
        stAttack.motion = clipAttack;
        stJump.motion   = clipJump;
        stBreath.motion = clipBreath;
        stHide.motion   = clipHide;

        sm.defaultState = stIdle;

        // Transitions
        var tIdleToWalk = stIdle.AddTransition(stWalk);
        tIdleToWalk.hasExitTime = false;
        tIdleToWalk.duration = 0.15f;
        tIdleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var tWalkToIdle = stWalk.AddTransition(stIdle);
        tWalkToIdle.hasExitTime = false;
        tWalkToIdle.duration = 0.15f;
        tWalkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var tWalkToRun = stWalk.AddTransition(stRun);
        tWalkToRun.hasExitTime = false;
        tWalkToRun.duration = 0.15f;
        tWalkToRun.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");

        var tIdleToRun = stIdle.AddTransition(stRun);
        tIdleToRun.hasExitTime = false;
        tIdleToRun.duration = 0.15f;
        tIdleToRun.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");

        var tRunToIdle = stRun.AddTransition(stIdle);
        tRunToIdle.hasExitTime = false;
        tRunToIdle.duration = 0.15f;
        tRunToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var anyToAttack = sm.AddAnyStateTransition(stAttack);
        anyToAttack.hasExitTime = false;
        anyToAttack.duration = 0.1f;
        anyToAttack.canTransitionToSelf = false;
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        var attackToRun = stAttack.AddTransition(stRun);
        attackToRun.hasExitTime = true;
        attackToRun.exitTime = 0.90f;
        attackToRun.duration = 0.15f;

        var anyToJump = sm.AddAnyStateTransition(stJump);
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.1f;
        anyToJump.canTransitionToSelf = false;
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        var jumpToRun = stJump.AddTransition(stRun);
        jumpToRun.hasExitTime = true;
        jumpToRun.exitTime = 0.90f;
        jumpToRun.duration = 0.15f;

        var anyToStun = sm.AddAnyStateTransition(stBreath);
        anyToStun.hasExitTime = false;
        anyToStun.duration = 0.15f;
        anyToStun.canTransitionToSelf = false;
        anyToStun.AddCondition(AnimatorConditionMode.If, 0, "Stun");

        var stunToRun = stBreath.AddTransition(stRun);
        stunToRun.hasExitTime = false;
        stunToRun.duration = 0.2f;
        stunToRun.AddCondition(AnimatorConditionMode.Greater, 1.0f, "Speed");

        var anyToHide = sm.AddAnyStateTransition(stHide);
        anyToHide.hasExitTime = false;
        anyToHide.duration = 0.15f;
        anyToHide.canTransitionToSelf = false;
        anyToHide.AddCondition(AnimatorConditionMode.If, 0, "Hide");

        var hideToIdle = stHide.AddTransition(stIdle);
        hideToIdle.hasExitTime = false;
        hideToIdle.duration = 0.15f;
        hideToIdle.AddCondition(AnimatorConditionMode.If, 0, "UnHide");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static PumpkinGhostConfig EnsureGhostConfig()
    {
        if (!Directory.Exists("Assets/Settings"))
        {
            Directory.CreateDirectory("Assets/Settings");
        }

        PumpkinGhostConfig config = AssetDatabase.LoadAssetAtPath<PumpkinGhostConfig>(CONFIG_PATH);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<PumpkinGhostConfig>();
            AssetDatabase.CreateAsset(config, CONFIG_PATH);
        }

        config.defaultChasePlayer = true;
        config.playerTag = "Player";
        config.patrolSpeed = 2.4f;
        config.chaseSpeed = 4.8f;
        config.rotationSpeed = 10.0f;
        config.acceleration = 12.0f;
        config.stoppingDistance = 1.6f;

        // Cấu hình Collider không fix cứng
        config.colliderRadius = 0.50f;
        config.colliderHeight = 1.92f;
        config.colliderCenter = new Vector3(0f, 0.96f, 0f);
        config.slopeLimit = 48.0f;
        config.stepOffset = 0.40f;

        // Cảm biến vật cản & mặt đất
        config.obstacleCheckOffset = new Vector3(0f, 0.85f, 0f);
        config.obstacleCheckRadius = 0.35f;
        config.obstacleAvoidanceDistance = 2.2f;
        config.avoidanceAngles = new float[] { 35f, -35f, 65f, -65f, 95f, -95f };
        config.obstacleLayers = ~0;

        config.groundCheckOffset = new Vector3(0f, 0.15f, 0f);
        config.groundCheckDistance = 0.35f;
        config.groundLayers = ~0;
        config.gravity = 20.0f;
        config.abyssThresholdY = -15.0f;

        // Tầm nhìn
        config.visionEyeOffset = new Vector3(0f, 1.45f, 0f);
        config.playerTargetEyeOffset = new Vector3(0f, 1.40f, 0f);
        config.detectionRadius = 35.0f;
        config.fieldOfViewAngle = 180.0f;
        config.closeProximityDistance = 7.5f;
        config.losePlayerDistance = 45.0f;

        // Kỹ năng
        config.attackRange = 2.0f;
        config.attackDamage = 20.0f;
        config.attackCooldown = 1.4f;
        config.attackHitDelay = 0.32f;
        config.attackTotalDuration = 0.75f;

        config.enableLeap = true;
        config.leapMinDistance = 4.0f;
        config.leapMaxDistance = 8.5f;
        config.leapSpeed = 9.2f;
        config.leapArcHeight = 1.8f;
        config.leapDuration = 0.78f;
        config.leapDamage = 30.0f;
        config.leapCooldown = 9.0f;

        config.outOfBreathDuration = 2.2f;
        config.enableVanishHide = true;
        config.vanishTriggerDistance = 11.0f;
        config.hideDuration = 2.5f;
        config.ambushBehindDistance = 3.2f;
        config.hideCooldown = 16.0f;

        config.patrolRadius = 12.0f;
        config.waypointWaitTime = 1.8f;

        // Tối ưu hóa hiệu năng & va chạm bằng Raycast NonAlloc (0 GC)
        config.useRaycastAttack = true;
        config.attackRaycastRadius = 0.45f;
        config.attackRaycastDistance = 2.4f;
        config.leapRaycastRadius = 0.65f;
        config.playerLayer = ~0;
        config.useRaycastSeparation = true;
        config.separationDistance = 1.35f;
        config.separationForce = 4.5f;
        config.visionCheckInterval = 0.05f;

        EditorUtility.SetDirty(config);
        return config;
    }

    private static GameObject BuildGhostPrefabAndInstance(Material ghostMat, AnimatorController animController, PumpkinGhostConfig config, GameObject playerGo)
    {
        // Xóa thể hiện cũ trong Scene nếu có
        GameObject oldGhost = GameObject.Find("PumpkinGhost_Enemy");
        while (oldGhost != null)
        {
            Object.DestroyImmediate(oldGhost);
            oldGhost = GameObject.Find("PumpkinGhost_Enemy");
        }

        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
        if (fbxPrefab == null)
        {
            Debug.LogError("[PumpkinGhostSetup] Không tìm thấy FBX tại: " + FBX_PATH);
            return null;
        }

        // Đặt tại vị trí trung tâm quảng trường, đối diện Player
        Vector3 spawnPos = new Vector3(5.2f, 0.45f, 11.8f);
        Quaternion spawnRot = Quaternion.Euler(0f, 215f, 0f);

        GameObject ghostRoot = new GameObject("PumpkinGhost_Enemy");
        ghostRoot.transform.position = spawnPos;
        ghostRoot.transform.rotation = spawnRot;
        try { ghostRoot.tag = "Enemy"; } catch { }

        // Model
        GameObject modelInst = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, ghostRoot.transform);
        modelInst.name = "Model";
        modelInst.transform.localPosition = Vector3.zero;
        modelInst.transform.localRotation = Quaternion.identity;
        modelInst.transform.localScale = Vector3.one;

        if (ghostMat != null)
        {
            Renderer[] renderers = modelInst.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = ghostMat;
            }
        }

        Animator animator = modelInst.GetComponent<Animator>();
        if (animator == null) animator = modelInst.AddComponent<Animator>();
        animator.runtimeAnimatorController = animController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // CharacterController
        CharacterController cc = ghostRoot.AddComponent<CharacterController>();
        cc.radius = config.colliderRadius;
        cc.height = config.colliderHeight;
        cc.center = config.colliderCenter;
        cc.slopeLimit = config.slopeLimit;
        cc.stepOffset = config.stepOffset;

        // Đèn mắt ma quái
        Light leftEye = CreateGhostLight("LeftEyeLight", ghostRoot.transform,
            new Vector3(-0.14f, 1.52f, 0.42f), config.aggroEyeColor, config.aggroEyeIntensity, config.eyeLightRange);
        Light rightEye = CreateGhostLight("RightEyeLight", ghostRoot.transform,
            new Vector3(0.14f, 1.52f, 0.42f), config.aggroEyeColor, config.aggroEyeIntensity, config.eyeLightRange);
        Light auraLight = CreateGhostLight("GhostAuraLight", ghostRoot.transform,
            new Vector3(0f, 0.35f, 0.25f), new Color(0.35f, 0.85f, 0.95f), 1.1f, 3.2f);

        // AudioSource
        AudioSource audioSrc = ghostRoot.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 1.0f;
        audioSrc.minDistance = 2.0f;
        audioSrc.maxDistance = 28.0f;
        audioSrc.playOnAwake = false;

        // PumpkinGhostAI
        PumpkinGhostAI ai = ghostRoot.AddComponent<PumpkinGhostAI>();
        ai.config = config;
        ai.currentState = PumpkinGhostAI.GhostState.Chase;
        if (playerGo != null) ai.targetPlayer = playerGo.transform;
        ai.leftEyeLight = leftEye;
        ai.rightEyeLight = rightEye;
        ai.auraLight = auraLight;
        ai.audioSource = audioSrc;

        PrefabUtility.SaveAsPrefabAssetAndConnect(ghostRoot, PREFAB_PATH, InteractionMode.AutomatedAction);

        // Sample pose
        Object[] allClips = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
        foreach (Object asset in allClips)
        {
            if (asset is AnimationClip clip && clip.name.ToUpperInvariant().Contains("IDLE"))
            {
                clip.SampleAnimation(modelInst, 0.2f);
                break;
            }
        }

        return ghostRoot;
    }

    private static Light CreateGhostLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
    {
        GameObject lGo = new GameObject(name);
        lGo.transform.SetParent(parent, false);
        lGo.transform.localPosition = localPos;
        Light l = lGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }
}

