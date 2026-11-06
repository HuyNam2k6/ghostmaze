using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CemeteryMapManager))]
public class CemeteryMapManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CemeteryMapManager manager = (CemeteryMapManager)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("Quản lý thông số bản đồ & hệ thống Collider. Chỉnh sửa thông số trong Config bên dưới hoặc bấm nút để cập nhật tức thì mà không cần fix cứng trong code.", MessageType.Info);
        EditorGUILayout.Space(4);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Thao Tác Nhanh (Không cần code lại)", EditorStyles.boldLabel);

        // Nút 1: Cập nhật Collider tức thì
        GUI.backgroundColor = new Color(0.4f, 0.85f, 1.0f);
        if (GUILayout.Button("⚡ Cập Nhật Kích Thước & Rào Chắn Collider (Tức thì)", GUILayout.Height(32)))
        {
            manager.ApplyColliderSettings();
            EditorUtility.SetDirty(manager.gameObject);
        }

        // Nút 2: Cập nhật Ánh sáng & Khí quyển tức thì
        GUI.backgroundColor = new Color(1.0f, 0.75f, 0.35f);
        if (GUILayout.Button("🌙 Áp Dụng Ánh Sáng & Sương Mù (Tức thì)", GUILayout.Height(30)))
        {
            manager.ApplyAtmosphereSettings();
        }

        // Nút 3: Dựng lại toàn bộ map theo cấu hình mới
        GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
        if (GUILayout.Button("🔄 Dựng Lại Toàn Bộ Map Theo Config", GUILayout.Height(34)))
        {
            if (EditorUtility.DisplayDialog("Dựng lại bản đồ?", "Thao tác này sẽ tái tạo toàn bộ Map dựa theo file CemeteryMapConfig hiện tại. Bạn có chắc muốn tiếp tục?", "Dựng lại ngay", "Hủy"))
            {
                HorrorCemeteryBuilder.BuildHorrorCemeteryMap(manager.config);
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(6);
    }
}

