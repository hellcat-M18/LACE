using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Lace.Editor
{
    /// <summary>
    /// ブレンドシェイプを複数選択するためのポップアップウィンドウ。
    /// 複数の SkinnedMeshRenderer のシェイプキーの和集合を表示する。
    /// 検索フィルター、全選択/全解除、チェックリストを提供する。
    /// </summary>
    public class BlendShapePickerWindow : EditorWindow
    {
        private SerializedObject _serializedObject;
        private SerializedProperty _blendShapeNames;
        private List<SkinnedMeshRenderer> _renderers;

        private string _searchFilter = "";
        private Vector2 _scrollPos;

        // キャッシュ
        private string[] _allShapeNames;
        private HashSet<string> _selected;
        // シェイプキー → それを持つレンダラー名のリスト
        private Dictionary<string, List<string>> _shapeToRenderers;

        /// <summary>
        /// ウィンドウを開く（複数レンダラー対応）。
        /// </summary>
        public static void Show(
            SerializedObject serializedObject,
            SerializedProperty blendShapeNames,
            List<SkinnedMeshRenderer> renderers)
        {
            var window = GetWindow<BlendShapePickerWindow>(true, "ブレンドシェイプ選択", true);
            window._serializedObject = serializedObject;
            window._blendShapeNames = blendShapeNames;
            window._renderers = renderers;
            window.minSize = new Vector2(300, 400);
            window.CacheShapeNames();
            window.Show();
        }

        private void CacheShapeNames()
        {
            _shapeToRenderers = new Dictionary<string, List<string>>();

            if (_renderers == null || _renderers.Count == 0)
            {
                _allShapeNames = new string[0];
                return;
            }

            // 元のメッシュにおけるインデックス順を維持する
            var orderedNames = new List<string>();

            foreach (var smr in _renderers)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var mesh = smr.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    if (!_shapeToRenderers.TryGetValue(shapeName, out var list))
                    {
                        list = new List<string>();
                        _shapeToRenderers[shapeName] = list;
                        orderedNames.Add(shapeName);
                    }
                    list.Add(smr.name);
                }
            }

            _allShapeNames = orderedNames.ToArray();
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            _selected = new HashSet<string>();
            if (_blendShapeNames == null) return;

            for (int i = 0; i < _blendShapeNames.arraySize; i++)
            {
                var val = _blendShapeNames.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(val))
                    _selected.Add(val);
            }
        }

        private void OnGUI()
        {
            if (_serializedObject == null || _blendShapeNames == null || _renderers == null || _renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("対象が無効です。ウィンドウを閉じてください。", MessageType.Warning);
                return;
            }

            // SerializedObject が破棄されていないか確認
            try { _serializedObject.Update(); }
            catch { Close(); return; }

            // ─── ヘッダー ───
            EditorGUILayout.LabelField($"対象レンダラー: {_renderers.Count} 個", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{_selected.Count} / {_allShapeNames.Length} 選択中");

            EditorGUILayout.Space(4);

            // ─── 検索 ───
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("検索", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            if (GUILayout.Button("×", GUILayout.Width(22)))
                _searchFilter = "";
            EditorGUILayout.EndHorizontal();

            // ─── 全選択/全解除 ───
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("全選択", GUILayout.Width(60)))
            {
                _blendShapeNames.ClearArray();
                for (int i = 0; i < _allShapeNames.Length; i++)
                {
                    _blendShapeNames.InsertArrayElementAtIndex(i);
                    _blendShapeNames.GetArrayElementAtIndex(i).stringValue = _allShapeNames[i];
                }
                _serializedObject.ApplyModifiedProperties();
                RefreshSelected();
            }
            if (GUILayout.Button("全解除", GUILayout.Width(60)))
            {
                _blendShapeNames.ClearArray();
                _serializedObject.ApplyModifiedProperties();
                RefreshSelected();
            }
            // フィルター表示中のみ: フィルター一致を一括選択/解除
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (GUILayout.Button("一致を選択", GUILayout.Width(80)))
                {
                    foreach (var name in _allShapeNames)
                    {
                        if (MatchesFilter(name) && !_selected.Contains(name))
                        {
                            _blendShapeNames.InsertArrayElementAtIndex(_blendShapeNames.arraySize);
                            _blendShapeNames.GetArrayElementAtIndex(_blendShapeNames.arraySize - 1).stringValue = name;
                        }
                    }
                    _serializedObject.ApplyModifiedProperties();
                    RefreshSelected();
                }
                if (GUILayout.Button("一致を解除", GUILayout.Width(80)))
                {
                    foreach (var name in _allShapeNames)
                    {
                        if (MatchesFilter(name) && _selected.Contains(name))
                            RemoveFromArray(name);
                    }
                    _serializedObject.ApplyModifiedProperties();
                    RefreshSelected();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // ─── チェックリスト ───
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _allShapeNames.Length; i++)
            {
                var shapeName = _allShapeNames[i];
                if (!MatchesFilter(shapeName)) continue;

                bool wasSelected = _selected.Contains(shapeName);

                // 複数レンダラーの場合、どのレンダラーに含まれるかをツールチップで表示
                string label = shapeName;
                if (_renderers.Count > 1 && _shapeToRenderers.TryGetValue(shapeName, out var rNames))
                {
                    label = $"{shapeName}  [{string.Join(", ", rNames)}]";
                }
                bool isSelected = EditorGUILayout.ToggleLeft(label, wasSelected);

                if (isSelected != wasSelected)
                {
                    if (isSelected)
                    {
                        _blendShapeNames.InsertArrayElementAtIndex(_blendShapeNames.arraySize);
                        _blendShapeNames.GetArrayElementAtIndex(_blendShapeNames.arraySize - 1).stringValue = shapeName;
                        _selected.Add(shapeName);
                    }
                    else
                    {
                        RemoveFromArray(shapeName);
                        _selected.Remove(shapeName);
                    }
                    _serializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesFilter(string shapeName)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return shapeName.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RemoveFromArray(string shapeName)
        {
            for (int j = _blendShapeNames.arraySize - 1; j >= 0; j--)
            {
                if (_blendShapeNames.GetArrayElementAtIndex(j).stringValue == shapeName)
                {
                    _blendShapeNames.DeleteArrayElementAtIndex(j);
                    break;
                }
            }
        }
    }
}
