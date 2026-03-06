using UnityEditor;
using UnityEngine;
using Lace.Runtime;

namespace Lace.Editor
{
    [CustomEditor(typeof(CostumeItem))]
    public class CostumeItemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var item = (CostumeItem)target;

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("LACE - Costume Item", headerStyle);
            EditorGUILayout.Space(4);

            // 簡易サマリー
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("パラメータ名", item.parameterName);
                EditorGUILayout.EnumPopup("制御対象", item.target);
            }

            EditorGUILayout.Space(8);

            if (GUILayout.Button("ダッシュボードで編集", GUILayout.Height(28)))
            {
                EditorApplication.ExecuteMenuItem("Tools/LACE/Dashboard");
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "全ての設定は LACE Dashboard で編集できます。\nTools → LACE → Dashboard",
                MessageType.Info);
        }
    }
}
