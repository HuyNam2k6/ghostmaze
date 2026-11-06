using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlayerSpawner
{
    [MenuItem("Tools/Spawn or Reset Third Person Player")]
    public static GameObject SpawnPlayer()
    {
        // 1. Kiểm tra hoặc xóa Player cũ nếu có
        GameObject oldPlayer = GameObject.Find("Player_ThirdPerson");
        if (oldPlayer != null)
        {
            Object.DestroyImmediate(oldPlayer);
        }

        // 2. Tạo gốc Player
        Vector3 spawnPos = new Vector3(2.5f, 0.45f, 7.5f);
        GameObject player = new GameObject("Player_ThirdPerson");
        player.tag = "Player";
        player.transform.position = spawnPos;
        player.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

        // 3. Gắn CharacterController, ThirdPersonPlayerController & PlayerHealth
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.radius = 0.35f;
        cc.height = 1.8f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.slopeLimit = 46f;
        cc.stepOffset = 0.38f;

        ThirdPersonPlayerController controller = player.AddComponent<ThirdPersonPlayerController>();
        controller.respawnPoint = spawnPos;

        player.AddComponent<PlayerHealth>();

        // 4. Tạo Visual Avatar chỉn chu (Body, Head, Cloak, Handheld Lantern)
        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(player.transform, false);

        Material matDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_AncientWood.mat");
        Material matStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_AncientStone.mat");
        Material matGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_GhostGlow.mat");
        Material matLantern = AssetDatabase.LoadAssetAtPath<Material>("Assets/Asset/CemeteryPack/Materials/Mat_ModularLantern.mat");

        // Body Capsule
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(visuals.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.65f, 0.75f, 0.55f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        if (matDark != null) body.GetComponent<MeshRenderer>().sharedMaterial = matDark;

        // Cloak / Robe Rim
        GameObject robe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        robe.name = "RobeBase";
        robe.transform.SetParent(visuals.transform, false);
        robe.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        robe.transform.localScale = new Vector3(0.72f, 0.42f, 0.65f);
        Object.DestroyImmediate(robe.GetComponent<Collider>());
        if (matDark != null) robe.GetComponent<MeshRenderer>().sharedMaterial = matDark;

        // Head Sphere
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visuals.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.58f, 0.05f);
        head.transform.localScale = new Vector3(0.48f, 0.50f, 0.48f);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        if (matStone != null) head.GetComponent<MeshRenderer>().sharedMaterial = matStone;

        // Glowing Visor / Eyes (Cyan Ghost Glow)
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "GlowingVisor";
        visor.transform.SetParent(head.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.05f, 0.45f);
        visor.transform.localScale = new Vector3(0.55f, 0.16f, 0.22f);
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        if (matGlow != null) visor.GetComponent<MeshRenderer>().sharedMaterial = matGlow;

        // Handheld Lantern on right side
        GameObject lanternHolder = new GameObject("HandheldLantern");
        lanternHolder.transform.SetParent(visuals.transform, false);
        lanternHolder.transform.localPosition = new Vector3(0.48f, 0.85f, 0.35f);

        GameObject lanternBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lanternBody.name = "LanternMesh";
        lanternBody.transform.SetParent(lanternHolder.transform, false);
        lanternBody.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
        Object.DestroyImmediate(lanternBody.GetComponent<Collider>());
        if (matLantern != null) lanternBody.GetComponent<MeshRenderer>().sharedMaterial = matLantern;

        // Lantern Point Light
        GameObject lightObj = new GameObject("LanternPointLight");
        lightObj.transform.SetParent(lanternHolder.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        Light pLight = lightObj.AddComponent<Light>();
        pLight.type = LightType.Point;
        pLight.color = new Color(1.0f, 0.65f, 0.22f);
        pLight.intensity = 2.8f;
        pLight.range = 7.5f;
        pLight.shadows = LightShadows.Soft;

        controller.lanternLight = pLight;

        // 5. Cấu hình Main Camera góc nhìn thứ 3
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            controller.cameraTransform = mainCam.transform;

            ThirdPersonCamera tpCam = mainCam.GetComponent<ThirdPersonCamera>();
            if (tpCam == null) tpCam = mainCam.gameObject.AddComponent<ThirdPersonCamera>();

            tpCam.target = player.transform;
            tpCam.targetOffset = new Vector3(0f, 1.45f, 0f);
            tpCam.distance = 4.6f;
            tpCam.minDistance = 1.8f;
            tpCam.maxDistance = 7.5f;

            // Đặt camera ngay phía sau lưng Player nhìn về hướng nhân vật hướng tới
            Vector3 camBackDir = -(Quaternion.Euler(12f, 35f, 0f) * Vector3.forward);
            mainCam.transform.position = spawnPos + new Vector3(0f, 1.45f, 0f) + camBackDir * 4.5f;
            mainCam.transform.LookAt(spawnPos + new Vector3(0f, 1.35f, 0f));
        }

        // Lưu Scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=green>[PlayerSpawner] Đã tạo thành công Player góc nhìn thứ 3 và gắn Camera bám theo!</color>");
        return player;
    }
}
