using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Lace.Runtime;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Lace.Editor
{
    /// <summary>
    /// LACE ダッシュボード。アバター内の全 CostumeItem を一覧表示し、
    /// 全設定のインライン編集・新規作成・削除を行える管理ウィンドウ。
    /// </summary>
    public class LaceDashboardWindow : EditorWindow
    {
        private const string ParentName = "LACE Menu Items";

        // ─── アバター選択 ───
        private VRCAvatarDescriptor[] _avatarsInScene;
        private int _avatarIndex;
        private VRCAvatarDescriptor _selectedAvatar;

        // ─── アイテム一覧 ───
        private CostumeItem[] _items;
        private Vector2 _listScroll;
        private CostumeItem _expandedItem;

        // ─── 展開中アイテムの SerializedObject ───
        private SerializedObject _expandedSO;
        private SerializedProperty _sp_parameterName;
        private SerializedProperty _sp_generateMenuItem;
        private SerializedProperty _sp_defaultValue;
        private SerializedProperty _sp_parameterSynced;
        private SerializedProperty _sp_parameterSaved;
        private SerializedProperty _sp_menuPath;
        private SerializedProperty _sp_installTargetMenu;
        private SerializedProperty _sp_icon;
        private SerializedProperty _sp_targetObjects;
        private SerializedProperty _sp_target;
        private SerializedProperty _sp_blendShapeNames;
        private SerializedProperty _sp_matchValue;
        private SerializedProperty _sp_matchActive;
        private SerializedProperty _sp_unmatchValue;
        private SerializedProperty _sp_unmatchActive;

        // ─── 条件エディタの UIステート ───
        private List<List<CondClause>> _conditionGroups;
        private CostumeItem _conditionGroupsOwner;

        // ─── 新規作成フォーム ───
        private bool _showCreateForm;
        private string _newItemName = "";
        private int _newInstallTargetIndex;
        private List<MenuEntry> _menuEntries;

        private struct MenuEntry
        {
            public string Path;
            public VRCExpressionsMenu Menu;
        }

        [MenuItem("Tools/LACE/Dashboard", false, 0)]
        private static void OpenWindow()
        {
            var win = GetWindow<LaceDashboardWindow>("LACE Dashboard");
            win.minSize = new Vector2(520, 340);
            win.Show();
        }

        private void OnEnable()
        {
            RefreshAvatars();
            RefreshItems();
        }

        private void OnFocus()
        {
            RefreshAvatars();
            RefreshItems();
        }

        private void OnHierarchyChange()
        {
            RefreshAvatars();
            RefreshItems();
            Repaint();
        }

        private void OnSelectionChange()
        {
            // Hierarchy で VRCAvatarDescriptor を含む GO を選択した場合、自動でアバターを切り替え
            var sel = Selection.activeGameObject;
            if (sel != null)
            {
                var desc = sel.GetComponentInParent<VRCAvatarDescriptor>();
                if (desc != null && desc != _selectedAvatar)
                {
                    SetAvatar(desc);
                    Repaint();
                }
            }
        }

        // ─── データ更新 ───

        private void RefreshAvatars()
        {
            _avatarsInScene = FindObjectsByType<VRCAvatarDescriptor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (_selectedAvatar == null && _avatarsInScene.Length > 0)
                SetAvatar(_avatarsInScene[0]);

            // 選択中のアバターが消えた場合
            if (_selectedAvatar != null && Array.IndexOf(_avatarsInScene, _selectedAvatar) < 0)
                SetAvatar(_avatarsInScene.Length > 0 ? _avatarsInScene[0] : null);
        }

        private void SetAvatar(VRCAvatarDescriptor avatar)
        {
            _selectedAvatar = avatar;
            _avatarIndex = _selectedAvatar != null
                ? Mathf.Max(0, Array.IndexOf(_avatarsInScene, _selectedAvatar))
                : 0;
            _menuEntries = null; // リビルドさせる
            RefreshItems();
        }

        private void RefreshItems()
        {
            if (_selectedAvatar != null)
                _items = _selectedAvatar.GetComponentsInChildren<CostumeItem>(true);
            else
                _items = Array.Empty<CostumeItem>();
        }

        private List<MenuEntry> GetMenuEntries()
        {
            if (_menuEntries != null) return _menuEntries;
            _menuEntries = new List<MenuEntry>();
            _menuEntries.Add(new MenuEntry { Path = "(ルート)", Menu = null });
            if (_selectedAvatar != null && _selectedAvatar.expressionsMenu != null)
                CollectMenuHierarchy(_selectedAvatar.expressionsMenu, "", _menuEntries);
            return _menuEntries;
        }

        /// <summary>展開中アイテムの SerializedObject とプロパティ群を確保する。</summary>
        private void EnsureExpandedSO()
        {
            if (_expandedItem == null) { _expandedSO = null; return; }
            if (_expandedSO != null && _expandedSO.targetObject == _expandedItem) return;

            _expandedSO = new SerializedObject(_expandedItem);
            _sp_parameterName     = _expandedSO.FindProperty("parameterName");
            _sp_generateMenuItem  = _expandedSO.FindProperty("generateMenuItem");
            _sp_defaultValue      = _expandedSO.FindProperty("defaultValue");
            _sp_parameterSynced   = _expandedSO.FindProperty("parameterSynced");
            _sp_parameterSaved    = _expandedSO.FindProperty("parameterSaved");
            _sp_menuPath          = _expandedSO.FindProperty("menuPath");
            _sp_installTargetMenu = _expandedSO.FindProperty("installTargetMenu");
            _sp_icon              = _expandedSO.FindProperty("icon");
            _sp_targetObjects     = _expandedSO.FindProperty("targetObjects");
            _sp_target            = _expandedSO.FindProperty("target");
            _sp_blendShapeNames   = _expandedSO.FindProperty("blendShapeNames");
            _sp_matchValue        = _expandedSO.FindProperty("matchValue");
            _sp_matchActive       = _expandedSO.FindProperty("matchActive");
            _sp_unmatchValue      = _expandedSO.FindProperty("unmatchValue");
            _sp_unmatchActive     = _expandedSO.FindProperty("unmatchActive");
        }

        // ─── GUI ───

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);

            if (_selectedAvatar == null)
            {
                EditorGUILayout.HelpBox("シーンにアバターが見つかりません。", MessageType.Info);
                return;
            }

            DrawItemList();
            EditorGUILayout.Space(4);
            DrawCreateForm();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // アバター選択ドロップダウン
            if (_avatarsInScene != null && _avatarsInScene.Length > 0)
            {
                var labels = new string[_avatarsInScene.Length];
                for (int i = 0; i < _avatarsInScene.Length; i++)
                    labels[i] = _avatarsInScene[i] != null ? _avatarsInScene[i].gameObject.name : "(null)";

                int newIdx = EditorGUILayout.Popup(_avatarIndex, labels, EditorStyles.toolbarPopup);
                if (newIdx != _avatarIndex && newIdx >= 0 && newIdx < _avatarsInScene.Length)
                    SetAvatar(_avatarsInScene[newIdx]);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("更新", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                RefreshAvatars();
                RefreshItems();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemList()
        {
            // ヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("パラメータ名", EditorStyles.miniBoldLabel, GUILayout.Width(130));
            GUILayout.Label("タイプ", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.Label("インストール先", EditorStyles.miniBoldLabel, GUILayout.Width(120));
            GUILayout.Label("条件サマリー", EditorStyles.miniBoldLabel);
            GUILayout.Label("", GUILayout.Width(52)); // 操作列
            EditorGUILayout.EndHorizontal();

            if (_items == null || _items.Length == 0)
            {
                EditorGUILayout.LabelField("　CostumeItem がありません。",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            // 同名パラメータが複数ある場合のインデックス付け用カウンタ
            var paramNameCount = new Dictionary<string, int>();
            var paramNameSeen  = new Dictionary<string, int>();
            foreach (var it in _items)
            {
                if (it == null) continue;
                var pn = string.IsNullOrEmpty(it.parameterName) ? "(未設定)" : it.parameterName;
                paramNameCount[pn] = paramNameCount.TryGetValue(pn, out int c) ? c + 1 : 1;
            }

            CostumeItem toDelete = null;

            foreach (var item in _items)
            {
                if (item == null) continue;

                bool isExpanded = _expandedItem == item;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // ─── 行ヘッダー ───
                EditorGUILayout.BeginHorizontal();

                // 展開トグル
                var arrow = isExpanded ? "▼" : "▶";
                if (GUILayout.Button(arrow, EditorStyles.miniLabel, GUILayout.Width(16)))
                {
                    _expandedItem = isExpanded ? null : item;
                    _expandedSO = null; // 再キャッシュさせる
                    _conditionGroups = null;
                    _conditionGroupsOwner = null;
                }

                // パラメータ名（同名の場合はインデックス付き）（クリックで Inspector 選択）
                var pnKey = string.IsNullOrEmpty(item.parameterName) ? "(未設定)" : item.parameterName;
                paramNameSeen[pnKey] = paramNameSeen.TryGetValue(pnKey, out int seen) ? seen + 1 : 1;
                var paramLabel = paramNameCount[pnKey] > 1
                    ? $"{pnKey} [{paramNameSeen[pnKey]}]"
                    : pnKey;
                if (GUILayout.Button(paramLabel, EditorStyles.linkLabel, GUILayout.Width(120)))
                {
                    Selection.activeGameObject = item.gameObject;
                    EditorGUIUtility.PingObject(item.gameObject);
                }

                // タイプ
                GUILayout.Label(item.target == RuleTarget.GameObject ? "GameObject" : "BlendShape",
                    GUILayout.Width(80));

                // インストール先
                var installLabel = item.installTargetMenu != null
                    ? item.installTargetMenu.name
                    : "(ルート)";
                GUILayout.Label(installLabel, GUILayout.Width(100));

                // 条件サマリー
                var cond = AnimatorGenerator.GetEffectiveCondition(item);
                var summary = cond != null ? ConditionToStringPlain(cond) : "常に動作";
                GUILayout.Label(summary, EditorStyles.miniLabel);

                // 削除ボタン
                if (GUILayout.Button("×", GUILayout.Width(22)))
                    toDelete = item;

                EditorGUILayout.EndHorizontal();

                // ─── 展開: 全設定エディタ ───
                if (isExpanded)
                    DrawExpandedItemEditor(item);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // 削除実行（ループ外）
            if (toDelete != null)
            {
                if (EditorUtility.DisplayDialog("削除確認",
                    $"CostumeItem「{toDelete.name}」を削除しますか？\n（GameObject ごと削除されます）",
                    "削除", "キャンセル"))
                {
                    Undo.DestroyObjectImmediate(toDelete.gameObject);
                    if (_expandedItem == toDelete) { _expandedItem = null; _expandedSO = null; _conditionGroups = null; _conditionGroupsOwner = null; }
                    RefreshItems();
                }
            }
        }

        // ─── 新規作成フォーム ───

        private void DrawCreateForm()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _showCreateForm = EditorGUILayout.Foldout(_showCreateForm, "新規作成", true);

            if (_showCreateForm)
            {
                EditorGUILayout.Space(2);

                var sel = Selection.gameObjects;
                int targetCount = 0;
                if (sel != null)
                {
                    foreach (var go in sel)
                    {
                        if (go != null && IsDescendantOf(go.transform, _selectedAvatar.transform))
                            targetCount++;
                    }
                }

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.IntField("選択中の対象数", targetCount);

                _newItemName = EditorGUILayout.TextField(
                    new GUIContent("メニュー名", "空欄なら選択オブジェクトの名前を使用します。"),
                    _newItemName);

                // インストール先
                var entries = GetMenuEntries();
                var labels = new string[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                    labels[i] = entries[i].Path;
                _newInstallTargetIndex = EditorGUILayout.Popup(
                    "インストール先",
                    Mathf.Clamp(_newInstallTargetIndex, 0, entries.Count - 1),
                    labels);

                EditorGUILayout.Space(4);

                // バリデーション
                string error = null;
                if (targetCount == 0)
                    error = "Hierarchy でアバター配下のオブジェクトを選択してください。";

                if (!string.IsNullOrEmpty(error))
                    EditorGUILayout.HelpBox(error, MessageType.Info);

                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(error)))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("作成", GUILayout.Width(90)))
                    {
                        CreateItem(sel, targetCount);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void CreateItem(GameObject[] selection, int targetCount)
        {
            if (_selectedAvatar == null || targetCount == 0) return;

            // アバター配下のオブジェクトだけフィルタ
            var targets = new List<GameObject>();
            foreach (var sel in selection)
            {
                if (sel != null && IsDescendantOf(sel.transform, _selectedAvatar.transform))
                    targets.Add(sel);
            }

            var itemName = string.IsNullOrWhiteSpace(_newItemName)
                ? GuessName(targets)
                : _newItemName.Trim();

            var parent = EnsureParent(_selectedAvatar.transform);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create LACE Costume Item");

            var go = new GameObject(itemName);
            Undo.RegisterCreatedObjectUndo(go, "Create LACE Costume Item");
            go.transform.SetParent(parent, false);

            var item = Undo.AddComponent<CostumeItem>(go);
            item.parameterName = go.name;

            // インストール先
            var entries = GetMenuEntries();
            if (entries.Count > 0)
            {
                var entry = entries[Mathf.Clamp(_newInstallTargetIndex, 0, entries.Count - 1)];
                item.installTargetMenu = entry.Menu;
            }

            // 制御対象
            item.target = RuleTarget.GameObject;
            item.targetObjects = targets;

            Undo.CollapseUndoOperations(group);

            // リセット
            _newItemName = "";
            RefreshItems();

            EditorGUIUtility.PingObject(go);
        }

        // ─── 展開エリア: 全設定エディタ ───

        private void DrawExpandedItemEditor(CostumeItem item)
        {
            EnsureExpandedSO();
            if (_expandedSO == null) return;

            _expandedSO.Update();

            EditorGUILayout.Space(4);
            EditorGUI.indentLevel++;

            DrawSection_Menu();
            DrawSection_Target(item);

            // ApplyModifiedProperties before condition section because
            // condition editing uses Undo.RecordObject + direct field write.
            _expandedSO.ApplyModifiedProperties();

            DrawSection_Condition(item);

            // Re-fetch for trigger section
            _expandedSO.Update();
            DrawSection_Trigger();
            _expandedSO.ApplyModifiedProperties();

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        // ─ セクション1: メニュー設定 ─

        private void DrawSection_Menu()
        {
            EditorGUILayout.LabelField("1. メニュー設定", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_sp_parameterName, new GUIContent("パラメータ名"));
            EditorGUILayout.PropertyField(_sp_generateMenuItem, new GUIContent("メニュー生成"));

            if (_sp_generateMenuItem.boolValue)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("パラメータ");
                _sp_defaultValue.boolValue    = GUILayout.Toggle(_sp_defaultValue.boolValue,    "初期ON", "Button", GUILayout.Width(56));
                _sp_parameterSynced.boolValue = GUILayout.Toggle(_sp_parameterSynced.boolValue, "同期",   "Button", GUILayout.Width(40));
                _sp_parameterSaved.boolValue  = GUILayout.Toggle(_sp_parameterSaved.boolValue,  "保持",   "Button", GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();

                DrawMenuInstallTargetInline();
                EditorGUILayout.PropertyField(_sp_menuPath, new GUIContent("サブメニューパス"));
                EditorGUILayout.PropertyField(_sp_icon, new GUIContent("アイコン"));
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawMenuInstallTargetInline()
        {
            var entries = GetMenuEntries();
            var currentMenu = (VRCExpressionsMenu)_sp_installTargetMenu.objectReferenceValue;
            int selectedIndex = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Menu == currentMenu) { selectedIndex = i; break; }
            }

            var labels = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++) labels[i] = entries[i].Path;

            int newIndex = EditorGUILayout.Popup("インストール先", selectedIndex, labels);
            if (newIndex != selectedIndex)
                _sp_installTargetMenu.objectReferenceValue = entries[newIndex].Menu;
        }

        // ─ セクション2: 制御対象 ─

        private void DrawSection_Target(CostumeItem item)
        {
            EditorGUILayout.LabelField("2. 制御対象", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            int newTargetIdx = GUILayout.Toolbar(
                _sp_target.enumValueIndex,
                new[] { "GameObject", "BlendShape" });
            if (newTargetIdx != _sp_target.enumValueIndex)
                _sp_target.enumValueIndex = newTargetIdx;
            EditorGUILayout.EndHorizontal();

            DrawTargetObjectsList();

            if ((RuleTarget)_sp_target.enumValueIndex == RuleTarget.BlendShape)
                DrawBlendShapeSelectorInline(item);

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        private void DrawTargetObjectsList()
        {
            int count = _sp_targetObjects.arraySize;
            for (int i = 0; i < count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var elem = _sp_targetObjects.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(elem, GUIContent.none);
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    _sp_targetObjects.DeleteArrayElementAtIndex(i);
                    // ObjectReference 削除は null 化→再削除が必要な場合がある
                    if (_sp_targetObjects.arraySize == count)
                        _sp_targetObjects.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ オブジェクト追加", GUILayout.Width(130)))
            {
                _sp_targetObjects.InsertArrayElementAtIndex(_sp_targetObjects.arraySize);
                _sp_targetObjects.GetArrayElementAtIndex(_sp_targetObjects.arraySize - 1)
                    .objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBlendShapeSelectorInline(CostumeItem item)
        {
            var renderers = item.GetTargetRenderers();
            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "オブジェクトに SkinnedMeshRenderer があるとシェイプキーを選択できます。",
                    MessageType.Info);
                return;
            }

            var allShapes = new HashSet<string>();
            foreach (var smr in renderers)
            {
                if (smr.sharedMesh == null) continue;
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    allShapes.Add(smr.sharedMesh.GetBlendShapeName(i));
            }

            if (allShapes.Count == 0)
            {
                EditorGUILayout.HelpBox("ブレンドシェイプがありません。", MessageType.Info);
                return;
            }

            int selectedCount = _sp_blendShapeNames.arraySize;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("ブレンドシェイプ");
            if (GUILayout.Button($"選択... ({selectedCount}/{allShapes.Count})", GUILayout.MinWidth(100)))
                BlendShapePickerWindow.Show(_expandedSO, _sp_blendShapeNames, renderers);
            EditorGUILayout.EndHorizontal();

            if (selectedCount > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < selectedCount; i++)
                    names.Add(_sp_blendShapeNames.GetArrayElementAtIndex(i).stringValue);
                EditorGUILayout.LabelField(string.Join(", ", names),
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        // ─ セクション3: 条件（自然言語ビルダー） ─

        private void DrawSection_Condition(CostumeItem item)
        {
            EditorGUILayout.LabelField("3. 条件式", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // 条件サマリー（自然な日本語・リッチテキスト）
            var effectiveCond = AnimatorGenerator.GetEffectiveCondition(item);
            if (effectiveCond == null)
            {
                EditorGUILayout.LabelField("このアイテムは常に動作します（条件なし）", EditorStyles.miniLabel);
            }
            else
            {
                var summary = ConditionToStringJapanese(effectiveCond);
                var trimmed = summary.TrimEnd();
                if (trimmed.EndsWith("の")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                var richStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
                EditorGUILayout.LabelField(trimmed, richStyle);
            }

            EditorGUILayout.Space(4);

            // 他アイテムのパラメータ名一覧を収集（ドロップダウン用）
            var otherParams = new List<string>();
            foreach (var other in _items)
            {
                if (other == null || other == item) continue;
                if (!other.generateMenuItem || string.IsNullOrEmpty(other.parameterName)) continue;
                if (other.parameterName == item.parameterName) continue;
                if (!otherParams.Contains(other.parameterName))
                    otherParams.Add(other.parameterName);
            }
            otherParams.Sort();

            // DNF グループリストをキャッシュ（展開アイテムが変わった時だけ再構築）
            if (_conditionGroups == null || _conditionGroupsOwner != item)
            {
                _conditionGroups = DecomposeToDNF(item.condition);
                _conditionGroupsOwner = item;
            }
            var groups = _conditionGroups;
            bool changed = false;

            for (int gi = 0; gi < groups.Count; gi++)
            {
                // グループ間セパレータ
                if (gi > 0)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(EditorGUI.indentLevel * 15f);
                    EditorGUILayout.LabelField("── または ──",
                        EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var group = groups[gi];

                // グループ内の各条件行を自然言語で表示
                for (int ci = 0; ci < group.Count; ci++)
                {
                    if (ci > 0)
                    {
                        EditorGUILayout.LabelField("── かつ ──",
                            EditorStyles.centeredGreyMiniLabel);
                    }

                    var clause = group[ci];

                    EditorGUILayout.BeginHorizontal();

                    // パラメータ選択ドロップダウン
                    var dropdownItems = new List<string> { "(選択...)" };
                    dropdownItems.AddRange(otherParams);
                    // 未登録パラメータの場合も表示
                    if (!string.IsNullOrEmpty(clause.parameterName)
                        && !otherParams.Contains(clause.parameterName))
                        dropdownItems.Add(clause.parameterName);

                    int selIdx = 0;
                    for (int di = 1; di < dropdownItems.Count; di++)
                    {
                        if (dropdownItems[di] == clause.parameterName) { selIdx = di; break; }
                    }

                    GUILayout.Label("  ", GUILayout.Width(8));
                    int newSelIdx = EditorGUILayout.Popup(selIdx, dropdownItems.ToArray(),
                        GUILayout.Width(130));
                    if (newSelIdx != selIdx && newSelIdx > 0)
                    {
                        clause.parameterName = dropdownItems[newSelIdx];
                        changed = true;
                    }

                    // 「が」
                    GUILayout.Label("が", GUILayout.Width(14));

                    // ON/OFF トグル
                    int valIdx = clause.expectedValue ? 0 : 1;
                    int newValIdx = GUILayout.Toolbar(valIdx, new[] { "ON", "OFF" },
                        GUILayout.Width(80));
                    if (newValIdx != valIdx)
                    {
                        clause.expectedValue = (newValIdx == 0);
                        changed = true;
                    }

                    // 「のとき」（行末ラベル）
                    //GUILayout.Label("のとき", GUILayout.Width(38));

                    // 削除ボタン（グループに2つ以上の条件がある場合、または全体に複数グループある場合）
                    if (group.Count > 1 || groups.Count > 1)
                    {
                        if (GUILayout.Button("×", GUILayout.Width(22)))
                        {
                            group.RemoveAt(ci);
                            if (group.Count == 0)
                                groups.RemoveAt(gi);
                            changed = true;
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                // グループ内に条件を追加
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 条件を追加", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    group.Add(new CondClause { parameterName = "", expectedValue = true });
                    changed = true;
                }
                // グループ削除（複数グループ時のみ + グループ全体を消す）
                if (groups.Count > 1)
                {
                    if (GUILayout.Button("グループ削除", EditorStyles.miniButton, GUILayout.Width(80)))
                    {
                        groups.RemoveAt(gi);
                        changed = true;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                // changed で要素を消した場合ループを抜ける
                if (changed) break;
            }

            // 「または」グループ追加ボタン
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 「または」を追加", EditorStyles.miniButton, GUILayout.Width(130)))
            {
                groups.Add(new List<CondClause>
                {
                    new CondClause { parameterName = "", expectedValue = true }
                });
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            // 変更があれば Condition に書き戻し（空の clause はスキップされるが UI には残る）
            if (changed)
            {
                Undo.RecordObject(item, "Edit LACE Condition");
                item.condition = RecomposeFromDNF(groups);
                EditorUtility.SetDirty(item);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ─── DNF 分解/再構成 ───

        /// <summary>条件1つ分の一時構造。</summary>
        private class CondClause
        {
            public string parameterName = "";
            public bool expectedValue = true;
        }

        /// <summary>
        /// Condition ツリーを DNF（OR of ANDs）に分解する。
        /// 結果は List of groups。各 group は List of CondClause（AND結合）。
        /// </summary>
        private static List<List<CondClause>> DecomposeToDNF(Condition cond)
        {
            var result = new List<List<CondClause>>();

            if (cond == null || IsEmptyCondition(cond))
            {
                // 空条件 → 空の1グループ（UI上 何もない状態から始められる）
                result.Add(new List<CondClause>());
                return result;
            }

            if (cond.type == ConditionType.Param)
            {
                // 単一 Param → 1グループ1条件
                result.Add(new List<CondClause>
                {
                    new CondClause { parameterName = cond.parameterName, expectedValue = cond.expectedValue }
                });
                return result;
            }

            if (cond.type == ConditionType.AND)
            {
                // AND(params...) → 1グループ複数条件
                var group = new List<CondClause>();
                if (cond.children != null)
                {
                    foreach (var child in cond.children)
                    {
                        if (child == null) continue;
                        if (child.type == ConditionType.Param)
                            group.Add(new CondClause { parameterName = child.parameterName, expectedValue = child.expectedValue });
                        else if (child.type == ConditionType.NOT && child.children != null && child.children.Count > 0
                            && child.children[0].type == ConditionType.Param)
                        {
                            // NOT(Param) → Param の反転
                            var inner = child.children[0];
                            group.Add(new CondClause { parameterName = inner.parameterName, expectedValue = !inner.expectedValue });
                        }
                    }
                }
                result.Add(group);
                return result;
            }

            if (cond.type == ConditionType.OR)
            {
                // OR(children...) → 各子を1グループとして分解
                if (cond.children != null)
                {
                    foreach (var child in cond.children)
                    {
                        if (child == null) continue;
                        // 各子を再帰的に分解して最初のグループだけ取る
                        var sub = DecomposeToDNF(child);
                        if (sub.Count > 0)
                            result.Add(sub[0]);
                    }
                }
                if (result.Count == 0)
                    result.Add(new List<CondClause>());
                return result;
            }

            // NOT 単体など → そのまま1条件として扱う（フォールバック）
            if (cond.type == ConditionType.NOT && cond.children != null && cond.children.Count > 0
                && cond.children[0].type == ConditionType.Param)
            {
                var inner = cond.children[0];
                result.Add(new List<CondClause>
                {
                    new CondClause { parameterName = inner.parameterName, expectedValue = !inner.expectedValue }
                });
                return result;
            }

            result.Add(new List<CondClause>());
            return result;
        }

        /// <summary>
        /// DNF グループリストから Condition ツリーを再構築する。
        /// </summary>
        private static Condition RecomposeFromDNF(List<List<CondClause>> groups)
        {
            // 有効な条件を持つグループだけ抽出
            var validGroups = new List<Condition>();
            foreach (var group in groups)
            {
                var validClauses = new List<Condition>();
                foreach (var clause in group)
                {
                    if (!string.IsNullOrEmpty(clause.parameterName))
                        validClauses.Add(Condition.Param(clause.parameterName, clause.expectedValue));
                }

                if (validClauses.Count == 0) continue;
                if (validClauses.Count == 1)
                    validGroups.Add(validClauses[0]);
                else
                    validGroups.Add(Condition.And(validClauses.ToArray()));
            }

            if (validGroups.Count == 0)
                return new Condition(); // 空

            if (validGroups.Count == 1)
                return validGroups[0];

            return Condition.Or(validGroups.ToArray());
        }

        private static bool IsEmptyCondition(Condition cond)
        {
            if (cond == null) return true;
            if (cond.type == ConditionType.Param && string.IsNullOrEmpty(cond.parameterName))
                return true;
            return false;
        }

        // ─ セクション4: 表示条件 / 発動条件 ─

        private void DrawSection_Trigger()
        {
            var targetType = (RuleTarget)_sp_target.enumValueIndex;
            EditorGUILayout.LabelField(
                "4. " + (targetType == RuleTarget.GameObject ? "表示条件" : "発動条件"),
                EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            float triggerIndent = EditorGUI.indentLevel * 15f;

            if (targetType == RuleTarget.GameObject)
            {
                int triggerIdx = _sp_matchActive.boolValue ? 0 : 1;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(triggerIndent);
                int newTrigger = GUILayout.Toolbar(triggerIdx, new[] { "条件式が真", "条件式が偽" });
                EditorGUILayout.EndHorizontal();
                if (newTrigger != triggerIdx)
                {
                    _sp_matchActive.boolValue   = (newTrigger == 0);
                    _sp_unmatchActive.boolValue = (newTrigger == 1);
                }
            }
            else
            {
                int triggerIdx = (_sp_matchValue.floatValue > _sp_unmatchValue.floatValue) ? 0 : 1;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(triggerIndent);
                int newTrigger = GUILayout.Toolbar(triggerIdx, new[] { "条件式が真", "条件式が偽" });
                EditorGUILayout.EndHorizontal();
                if (newTrigger != triggerIdx)
                {
                    float tmp = _sp_matchValue.floatValue;
                    _sp_matchValue.floatValue   = _sp_unmatchValue.floatValue;
                    _sp_unmatchValue.floatValue = tmp;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("真の時の値");
                _sp_matchValue.floatValue = EditorGUILayout.Slider(_sp_matchValue.floatValue, 0f, 100f);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("偽の時の値");
                _sp_unmatchValue.floatValue = EditorGUILayout.Slider(_sp_unmatchValue.floatValue, 0f, 100f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ─── ヘルパー ───

        private static string GuessName(List<GameObject> targets)
        {
            if (targets == null || targets.Count == 0) return "LACE Item";
            foreach (var t in targets)
            {
                if (t != null) return t.name;
            }
            return "LACE Item";
        }

        private static bool IsDescendantOf(Transform child, Transform parent)
        {
            if (child == null || parent == null) return false;
            var t = child;
            while (t != null)
            {
                if (t == parent) return true;
                t = t.parent;
            }
            return false;
        }

        private static Transform EnsureParent(Transform avatarRoot)
        {
            if (avatarRoot == null) return null;

            for (int i = 0; i < avatarRoot.childCount; i++)
            {
                var c = avatarRoot.GetChild(i);
                if (c != null && c.name == ParentName)
                    return c;
            }

            var parentGo = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parentGo, "Create LACE Parent");
            parentGo.transform.SetParent(avatarRoot, false);
            return parentGo.transform;
        }

        /// <summary>Condition ツリーを自然な日本語（リッチテキスト）に変換する。</summary>
        private static string ConditionToStringJapanese(Condition cond)
        {
            if (cond == null) return "?";

            switch (cond.type)
            {
                case ConditionType.Param:
                    if (string.IsNullOrEmpty(cond.parameterName)) return "（パラメータ未設定）";
                    return cond.expectedValue
                        ? $"<b>{cond.parameterName}がON</b>"
                        : $"<b>{cond.parameterName}がOFF</b>";

                case ConditionType.AND:
                    if (cond.children == null || cond.children.Count == 0) return "（条件なし）";
                    var andParts = new List<string>();
                    foreach (var child in cond.children)
                        andParts.Add(WrapJapanese(child, ConditionType.AND));
                    return string.Join(" かつ ", andParts);

                case ConditionType.OR:
                    if (cond.children == null || cond.children.Count == 0) return "（条件なし）";
                    var orParts = new List<string>();
                    foreach (var child in cond.children)
                        orParts.Add(WrapJapanese(child, ConditionType.OR));
                    return string.Join(" または ", orParts);

                case ConditionType.NOT:
                    if (cond.children == null || cond.children.Count == 0) return "（条件なし）";
                    var notChild = cond.children[0];
                    if (notChild != null && notChild.type == ConditionType.Param
                        && !string.IsNullOrEmpty(notChild.parameterName))
                    {
                        return notChild.expectedValue
                            ? $"<b>{notChild.parameterName}がOFF</b>"
                            : $"<b>{notChild.parameterName}がON</b>";
                    }
                    return $"<b>{ConditionToStringJapanese(notChild)}ではない</b>";

                default:
                    return "?";
            }
        }

        private static string WrapJapanese(Condition child, ConditionType parentType)
        {
            var s = ConditionToStringJapanese(child);
            if (parentType == ConditionType.AND && child.type == ConditionType.OR)
                return $"( {s} )";
            return s;
        }

        /// <summary>条件式をプレーンテキストに変換（リッチテキストなし）。一覧行用。</summary>
        private static string ConditionToStringPlain(Condition cond)
        {
            if (cond == null) return "?";

            switch (cond.type)
            {
                case ConditionType.Param:
                    if (string.IsNullOrEmpty(cond.parameterName)) return "(未設定)";
                    return cond.expectedValue
                        ? $"{cond.parameterName}=ON"
                        : $"{cond.parameterName}=OFF";

                case ConditionType.AND:
                    if (cond.children == null || cond.children.Count == 0) return "(なし)";
                    var andParts = new List<string>();
                    foreach (var child in cond.children)
                        andParts.Add(WrapPlain(child, ConditionType.AND));
                    return string.Join(" & ", andParts);

                case ConditionType.OR:
                    if (cond.children == null || cond.children.Count == 0) return "(なし)";
                    var orParts = new List<string>();
                    foreach (var child in cond.children)
                        orParts.Add(WrapPlain(child, ConditionType.OR));
                    return string.Join(" | ", orParts);

                case ConditionType.NOT:
                    if (cond.children == null || cond.children.Count == 0) return "(なし)";
                    var nc = cond.children[0];
                    if (nc != null && nc.type == ConditionType.Param
                        && !string.IsNullOrEmpty(nc.parameterName))
                    {
                        return nc.expectedValue
                            ? $"{nc.parameterName}=OFF"
                            : $"{nc.parameterName}=ON";
                    }
                    return $"!({ConditionToStringPlain(nc)})";

                default:
                    return "?";
            }
        }

        private static string WrapPlain(Condition child, ConditionType parentType)
        {
            var s = ConditionToStringPlain(child);
            if (parentType == ConditionType.AND && child.type == ConditionType.OR)
                return $"({s})";
            return s;
        }

        private static void CollectMenuHierarchy(
            VRCExpressionsMenu menu, string parentPath,
            List<MenuEntry> entries, int depth = 0)
        {
            if (menu == null || depth > 10) return;

            foreach (var control in menu.controls)
            {
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
                    continue;
                if (control.subMenu == null) continue;

                var path = string.IsNullOrEmpty(parentPath)
                    ? control.name
                    : $"{parentPath}/{control.name}";

                entries.Add(new MenuEntry { Path = path, Menu = control.subMenu });
                CollectMenuHierarchy(control.subMenu, path, entries, depth + 1);
            }
        }
    }
}
