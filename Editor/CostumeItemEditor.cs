using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Lace.Runtime;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Lace.Editor
{
    [CustomEditor(typeof(CostumeItem))]
    public class CostumeItemEditor : UnityEditor.Editor
    {
        // Menu settings
        private SerializedProperty _parameterName;
        private SerializedProperty _generateMenuItem;
        private SerializedProperty _defaultValue;
        private SerializedProperty _parameterSynced;
        private SerializedProperty _parameterSaved;
        private SerializedProperty _menuPath;
        private SerializedProperty _installTargetMenu;
        private SerializedProperty _icon;

        // Target
        private SerializedProperty _targetObjects;
        private SerializedProperty _target;
        private SerializedProperty _blendShapeNames;

        // Condition
        private SerializedProperty _condition;

        // Match / Unmatch
        private SerializedProperty _matchValue;
        private SerializedProperty _matchActive;
        private SerializedProperty _unmatchValue;
        private SerializedProperty _unmatchActive;

        // UI state
        private bool _showConditionHelp = false;
        private bool _expandAdditionalCondition = false;
        private bool _expandParameterOptions = false;

        private void OnEnable()
        {
            _parameterName = serializedObject.FindProperty("parameterName");
            _generateMenuItem = serializedObject.FindProperty("generateMenuItem");
            _defaultValue = serializedObject.FindProperty("defaultValue");
            _parameterSynced = serializedObject.FindProperty("parameterSynced");
            _parameterSaved = serializedObject.FindProperty("parameterSaved");
            _menuPath = serializedObject.FindProperty("menuPath");
            _installTargetMenu = serializedObject.FindProperty("installTargetMenu");
            _icon = serializedObject.FindProperty("icon");

            _targetObjects = serializedObject.FindProperty("targetObjects");
            _target = serializedObject.FindProperty("target");
            _blendShapeNames = serializedObject.FindProperty("blendShapeNames");

            _condition = serializedObject.FindProperty("condition");

            _matchValue = serializedObject.FindProperty("matchValue");
            _matchActive = serializedObject.FindProperty("matchActive");
            _unmatchValue = serializedObject.FindProperty("unmatchValue");
            _unmatchActive = serializedObject.FindProperty("unmatchActive");

            // ターゲットオブジェクトリストを最初から展開
            _targetObjects.isExpanded = true;
        }

        // ─── セクション描画ヘルパー ───

        /// <summary>背景付きボックスでセクションを囲む開始</summary>
        private static void BeginSection(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        /// <summary>背景付きボックスでセクションを囲む終了</summary>
        private static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ─── ヘッダー ───
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("LACE - Costume Item", headerStyle);

            // ─── メニュー設定 ───
            BeginSection("1. メニュー設定");
            EditorGUILayout.PropertyField(_parameterName, new GUIContent("パラメータ名"));
            EditorGUILayout.PropertyField(_generateMenuItem, new GUIContent("メニュー生成"));

            if (_generateMenuItem.boolValue)
            {
                EditorGUI.indentLevel++;

                // ─── パラメータ設定（折りたたみ / デフォルト閉） ───
                _expandParameterOptions = EditorGUILayout.Foldout(
                    _expandParameterOptions,
                    "パラメータ設定",
                    true);

                if (_expandParameterOptions)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_defaultValue, new GUIContent("初期値"));
                    EditorGUILayout.PropertyField(_parameterSynced, new GUIContent("同期"));
                    EditorGUILayout.PropertyField(_parameterSaved, new GUIContent("保持"));
                    EditorGUI.indentLevel--;
                }

                // ─── インストール先メニュー（階層ドロップダウン） ───
                DrawMenuInstallTarget();

                EditorGUILayout.PropertyField(
                    _menuPath,
                    new GUIContent("サブメニューパス", "インストール先メニュー直下を基準に指定します（例: Upper/Inner）"));
                EditorGUILayout.PropertyField(_icon, new GUIContent("アイコン"));
                EditorGUI.indentLevel--;
            }
            EndSection();

            // ─── 制御対象 ───
            BeginSection("2. 制御対象");
            // 対象タイプ 2択ボタン
            {
                int newTargetIdx = GUILayout.Toolbar(
                    _target.enumValueIndex,
                    new[] { "GameObject", "BlendShape" });
                if (newTargetIdx != _target.enumValueIndex)
                    _target.enumValueIndex = newTargetIdx;
            }
            EditorGUILayout.PropertyField(_targetObjects, new GUIContent("オブジェクト"), true);

            var targetType = (RuleTarget)_target.enumValueIndex;
            if (targetType == RuleTarget.BlendShape)
            {
                EditorGUI.indentLevel++;
                DrawBlendShapeSelector();
                EditorGUI.indentLevel--;
            }
            EndSection();

            // ─── 条件式（展開式） ───
            BeginSection("3. 条件式");

            // 条件式サマリーを先頭に表示
            DrawConditionSummary();
            EditorGUILayout.Space(6);

            _expandAdditionalCondition = EditorGUILayout.Foldout(
                _expandAdditionalCondition,
                "条件式を編集",
                true);

            if (_expandAdditionalCondition)
            {
                // 折りたたみ可能なヘルプ
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_showConditionHelp ? "ヘルプを閉じる" : "?", GUILayout.Width(_showConditionHelp ? 100 : 24)))
                    _showConditionHelp = !_showConditionHelp;
                EditorGUILayout.EndHorizontal();

                if (_showConditionHelp)
                {
                    EditorGUILayout.HelpBox(
                        "空の場合、メニュー生成が有効なときは自身のパラメータ名のみで ON/OFF します。\n" +
                        "メニュー生成が無効なときは、ここで指定した条件のみで制御されます。\n" +
                        "メニュー生成が有効かつ条件を設定した場合は、自身のパラメータ AND 追加条件 になります。",
                        MessageType.Info);
                }

                DrawSelfParamReadOnly();
                DrawConditionEditor();
            }
            EndSection();

            // ─── 表示条件 / 発動条件 ───
            BeginSection("4. " + (targetType == RuleTarget.GameObject ? "表示条件" : "発動条件"));
            if (targetType == RuleTarget.GameObject)
            {
                // matchActive=true/unmatchActive=false → 「条件式が真」のとき発動(index 0)
                // matchActive=false/unmatchActive=true → 「条件式が偽」のとき発動(index 1)
                int triggerIdx = _matchActive.boolValue ? 0 : 1;
                int newTrigger = GUILayout.Toolbar(triggerIdx, new[] { "条件式が真", "条件式が偽" });
                if (newTrigger != triggerIdx)
                {
                    _matchActive.boolValue   = (newTrigger == 0);
                    _unmatchActive.boolValue = (newTrigger == 1);
                }
            }
            else
            {
                int triggerIdx = (_matchValue.floatValue > _unmatchValue.floatValue) ? 0 : 1;
                int newTrigger = GUILayout.Toolbar(triggerIdx, new[] { "条件式が真", "条件式が偽" });
                if (newTrigger != triggerIdx)
                {
                    // 真偽を入れ替え
                    float tmp = _matchValue.floatValue;
                    _matchValue.floatValue   = _unmatchValue.floatValue;
                    _unmatchValue.floatValue = tmp;
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("真の時の値");
                _matchValue.floatValue = EditorGUILayout.Slider(_matchValue.floatValue, 0f, 100f);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("偽の時の値");
                _unmatchValue.floatValue = EditorGUILayout.Slider(_unmatchValue.floatValue, 0f, 100f);
                EditorGUILayout.EndHorizontal();
            }
            EndSection();

            serializedObject.ApplyModifiedProperties();
        }

        // ─── 条件式サマリー ───

        /// <summary>
        /// 有効条件式（自身パラメータ AND 追加条件）を人間が読みやすいテキストで表示する。
        /// </summary>
        private void DrawConditionSummary()
        {
            var item = (CostumeItem)target;
            var effectiveCondition = AnimatorGenerator.GetEffectiveCondition(item);

            if (effectiveCondition == null)
            {
                EditorGUILayout.LabelField("このアイテムは常に動作します（条件なし）", EditorStyles.miniLabel);
            }
            else
            {
                var summary = ConditionToString(effectiveCondition);
                var trimmed = summary.TrimEnd();
                if (trimmed.EndsWith("の")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
                var richStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
                EditorGUILayout.LabelField(trimmed, richStyle);
            }
        }

        /// <summary>Condition ツリーを自然な日本語に変換する。</summary>
        private static string ConditionToString(Condition cond)
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
                        andParts.Add(WrapIfComplex(child, ConditionType.AND));
                    return string.Join(" かつ ", andParts);

                case ConditionType.OR:
                    if (cond.children == null || cond.children.Count == 0) return "（条件なし）";
                    var orParts = new List<string>();
                    foreach (var child in cond.children)
                        orParts.Add(WrapIfComplex(child, ConditionType.OR));
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
                    return $"<b>{ConditionToString(notChild)}ではない</b>";

                default:
                    return "?";
            }
        }

        private static string WrapIfComplex(Condition child, ConditionType parentType)
        {
            var s = ConditionToString(child);
            if (parentType == ConditionType.AND && child.type == ConditionType.OR)
                return $"( {s} )";
            return s;
        }

        // ─── 自身パラメータ（読み取り専用） ───

        /// <summary>
        /// メニュー生成が有効な場合、自身のパラメータを暗黙 AND として
        /// 読み取り専用で表示する。
        /// </summary>
        private void DrawSelfParamReadOnly()
        {
            var item = (CostumeItem)target;
            if (!item.generateMenuItem || string.IsNullOrEmpty(item.parameterName))
                return;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(item.parameterName, GUILayout.MinWidth(60));
                GUILayout.Label("が", GUILayout.Width(14));
                GUILayout.Toolbar(0, new[] { "ON", "OFF" }, GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            // 追加条件がある場合のみ「かつ」セパレータを表示
            bool hasCondition = item.condition != null
                && !(item.condition.type == ConditionType.Param
                     && string.IsNullOrEmpty(item.condition.parameterName));
            if (hasCondition)
            {
                EditorGUILayout.LabelField("── かつ ──",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        // ─── 条件式エディタ ───

        /// <summary>条件ツリーの描画エントリポイント。</summary>
        private void DrawConditionEditor()
        {
            if (_condition == null) return;

            var typeProp = _condition.FindPropertyRelative("type");
            var childrenProp = _condition.FindPropertyRelative("children");
            var condType = (ConditionType)typeProp.enumValueIndex;

            // AND/OR/NOT で子が 0 → 空の Param に正規化
            if (condType != ConditionType.Param
                && (childrenProp == null || childrenProp.arraySize == 0))
            {
                typeProp.enumValueIndex = (int)ConditionType.Param;
                _condition.FindPropertyRelative("parameterName").stringValue = "";
                _condition.FindPropertyRelative("expectedValue").boolValue = true;
                condType = ConditionType.Param;
            }

            if (condType == ConditionType.Param)
            {
                // ルートが単一 Param（デフォルト空 or 単一条件）
                var paramNameProp = _condition.FindPropertyRelative("parameterName");
                bool isEmpty = string.IsNullOrEmpty(paramNameProp.stringValue);

                if (!isEmpty)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawParamRowInline(_condition, false, -1, null);
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField("（追加条件なし）",
                        EditorStyles.centeredGreyMiniLabel);
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 条件を追加", GUILayout.Width(100)))
                {
                    if (isEmpty)
                    {
                        typeProp.enumValueIndex = (int)ConditionType.AND;
                        childrenProp.ClearArray();
                        childrenProp.InsertArrayElementAtIndex(0);
                        InitParamChild(childrenProp.GetArrayElementAtIndex(0));
                    }
                    else
                    {
                        // 単一 Param → AND(既存, 新規) に変換
                        string savedName = paramNameProp.stringValue;
                        bool savedExpected =
                            _condition.FindPropertyRelative("expectedValue").boolValue;

                        typeProp.enumValueIndex = (int)ConditionType.AND;
                        paramNameProp.stringValue = "";

                        childrenProp.ClearArray();
                        childrenProp.InsertArrayElementAtIndex(0);
                        var first = childrenProp.GetArrayElementAtIndex(0);
                        first.FindPropertyRelative("type").enumValueIndex =
                            (int)ConditionType.Param;
                        first.FindPropertyRelative("parameterName").stringValue =
                            savedName;
                        first.FindPropertyRelative("expectedValue").boolValue =
                            savedExpected;
                        first.FindPropertyRelative("children").ClearArray();

                        childrenProp.InsertArrayElementAtIndex(1);
                        InitParamChild(childrenProp.GetArrayElementAtIndex(1));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawContainerNode(_condition, 0);
            }
        }

        /// <summary>AND/OR/NOT コンテナの描画。</summary>
        private void DrawContainerNode(SerializedProperty condProp, int depth)
        {
            var typeProp = condProp.FindPropertyRelative("type");
            var childrenProp = condProp.FindPropertyRelative("children");
            var condType = (ConditionType)typeProp.enumValueIndex;

            // 階層に応じた左マージン
            float indent = depth * 16f;

            // タイプ選択（フレンドリー名）
            var labels = new[] { "すべて満たす (AND)", "いずれか満たす (OR)", "否定 (NOT)" };
            int typeIdx = condType == ConditionType.AND ? 0
                        : condType == ConditionType.OR  ? 1 : 2;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            EditorGUILayout.BeginVertical();
            int newTypeIdx = EditorGUILayout.Popup("結合モード", typeIdx, labels);
            if (newTypeIdx != typeIdx)
            {
                var newType = newTypeIdx == 0 ? ConditionType.AND
                            : newTypeIdx == 1 ? ConditionType.OR
                            : ConditionType.NOT;
                typeProp.enumValueIndex = (int)newType;
                condType = newType;

                if (condType == ConditionType.NOT && childrenProp.arraySize > 1)
                {
                    while (childrenProp.arraySize > 1)
                        childrenProp.DeleteArrayElementAtIndex(childrenProp.arraySize - 1);
                }
            }

            int maxChildren = condType == ConditionType.NOT ? 1 : int.MaxValue;
            string separator = condType == ConditionType.AND ? "かつ"
                             : condType == ConditionType.OR  ? "または" : "";

            EditorGUILayout.Space(2);

            if (childrenProp != null)
            {
                bool deleted = false;
                for (int i = 0; i < childrenProp.arraySize; i++)
                {
                    // セパレータ（2 番目以降）
                    if (i > 0 && !string.IsNullOrEmpty(separator))
                    {
                        EditorGUILayout.LabelField($"── {separator} ──",
                            EditorStyles.centeredGreyMiniLabel);
                    }

                    var child = childrenProp.GetArrayElementAtIndex(i);
                    var childType =
                        (ConditionType)child.FindPropertyRelative("type").enumValueIndex;

                    if (childType == ConditionType.Param)
                    {
                        // コンパクトなパラメータ行
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        if (DrawParamRowInline(child, true, i, childrenProp))
                            deleted = true;
                        EditorGUILayout.EndVertical();
                        if (deleted) break;
                    }
                    else
                    {
                        // ネストされたグループ
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("グループ", EditorStyles.miniBoldLabel);
                        DrawContainerNode(child, depth + 1);

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("グループ削除", GUILayout.Width(90)))
                        {
                            childrenProp.DeleteArrayElementAtIndex(i);
                            deleted = true;
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(10);
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(10);
                        if (deleted) break;
                    }
                }

                // 追加ボタン
                if (!deleted && childrenProp.arraySize < maxChildren)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ 条件", GUILayout.Width(60)))
                    {
                        childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                        InitParamChild(
                            childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1));
                    }
                    if (condType != ConditionType.NOT
                        && GUILayout.Button("+ グループ", GUILayout.Width(90)))
                    {
                        childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                        var ng = childrenProp.GetArrayElementAtIndex(
                            childrenProp.arraySize - 1);
                        ng.FindPropertyRelative("type").enumValueIndex =
                            (int)ConditionType.AND;
                        var nc = ng.FindPropertyRelative("children");
                        nc.ClearArray();
                        nc.InsertArrayElementAtIndex(0);
                        InitParamChild(nc.GetArrayElementAtIndex(0));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>パラメータ条件を1行で表示。削除された場合 true を返す。</summary>
        private bool DrawParamRowInline(
            SerializedProperty condProp, bool showDelete,
            int index, SerializedProperty parentArray)
        {
            var paramNameProp = condProp.FindPropertyRelative("parameterName");
            var expectedProp  = condProp.FindPropertyRelative("expectedValue");

            EditorGUILayout.BeginHorizontal();
            DrawParamDropdownInline(paramNameProp);
            GUILayout.Label("が", GUILayout.Width(14));

            int valIdx = expectedProp.boolValue ? 0 : 1;
            int newIdx = GUILayout.Toolbar(valIdx, new[] { "ON", "OFF" },
                GUILayout.Width(80));
            if (newIdx != valIdx) expectedProp.boolValue = (newIdx == 0);

            bool deleted = false;
            if (showDelete && GUILayout.Button("×", GUILayout.Width(22)))
            {
                parentArray.DeleteArrayElementAtIndex(index);
                deleted = true;
            }
            EditorGUILayout.EndHorizontal();
            return deleted;
        }

        /// <summary>新しい空の Param 子を初期化。</summary>
        private static void InitParamChild(SerializedProperty child)
        {
            child.FindPropertyRelative("type").enumValueIndex = (int)ConditionType.Param;
            child.FindPropertyRelative("parameterName").stringValue = "";
            child.FindPropertyRelative("expectedValue").boolValue = true;
            child.FindPropertyRelative("children").ClearArray();
        }

        // ─── ブレンドシェイプ選択 ───

        /// <summary>
        /// targetObjects 内の全 SkinnedMeshRenderer からシェイプキーの和集合を収集し、
        /// 選択ボタンと選択済み一覧をコンパクトに表示する。
        /// </summary>
        private void DrawBlendShapeSelector()
        {
            var item = (CostumeItem)target;
            var renderers = item.GetTargetRenderers();

            if (renderers.Count == 0)
            {
                EditorGUILayout.HelpBox("ターゲットに SkinnedMeshRenderer を持つオブジェクトを追加するとシェイプキーを選択できます。", MessageType.Info);
                return;
            }

            // 全レンダラーのシェイプキーの和集合を算出
            var allShapes = new HashSet<string>();
            foreach (var smr in renderers)
            {
                if (smr.sharedMesh == null) continue;
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    allShapes.Add(smr.sharedMesh.GetBlendShapeName(i));
            }

            int shapeCount = allShapes.Count;
            if (shapeCount == 0)
            {
                EditorGUILayout.HelpBox("ターゲットのメッシュにブレンドシェイプがありません。", MessageType.Info);
                return;
            }

            int selectedCount = _blendShapeNames.arraySize;

            // ボタン + 選択数
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ブレンドシェイプ", GUILayout.Width(EditorGUIUtility.labelWidth));
            if (GUILayout.Button($"選択... ({selectedCount}/{shapeCount})", GUILayout.MinWidth(100)))
            {
                BlendShapePickerWindow.Show(serializedObject, _blendShapeNames, renderers);
            }
            EditorGUILayout.EndHorizontal();

            // レンダラー情報
            EditorGUILayout.LabelField($"対象レンダラー: {renderers.Count} 個", EditorStyles.miniLabel);

            // 選択済みシェイプキーをコンパクト表示
            if (selectedCount > 0)
            {
                EditorGUI.indentLevel++;
                var names = new List<string>();
                for (int i = 0; i < selectedCount; i++)
                    names.Add(_blendShapeNames.GetArrayElementAtIndex(i).stringValue);
                EditorGUILayout.LabelField(string.Join(", ", names), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }
        }

        // ─── パラメータ選択ドロップダウン（インライン） ───

        /// <summary>
        /// ラベルなしコンパクトパラメータ名ドロップダウン。
        /// 水平レイアウト内で使用する。
        /// </summary>
        private void DrawParamDropdownInline(SerializedProperty paramNameProp)
        {
            var item = (CostumeItem)target;
            var avatar = FindAvatarDescriptor(item.transform);

            var paramNames = new List<string>();
            if (avatar != null)
            {
                var allItems = avatar.GetComponentsInChildren<CostumeItem>(true);
                foreach (var ci in allItems)
                {
                    if (ci == item) continue;
                    if (!string.IsNullOrEmpty(ci.parameterName)
                        && !paramNames.Contains(ci.parameterName))
                        paramNames.Add(ci.parameterName);
                }
                paramNames.Sort();
            }

            var current = paramNameProp.stringValue;
            if (!string.IsNullOrEmpty(current) && !paramNames.Contains(current))
                paramNames.Insert(0, $"(未登録) {current}");
            else
                paramNames.Insert(0, "(未選択)");

            if (paramNames.Count == 1)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup(0, new[] { "(候補なし)" });
                return;
            }

            int selected = 0;
            for (int i = 1; i < paramNames.Count; i++)
            {
                if (paramNames[i] == current) { selected = i; break; }
            }
            int newSel = EditorGUILayout.Popup(selected, paramNames.ToArray());
            if (newSel != selected && newSel > 0)
                paramNameProp.stringValue = paramNames[newSel];
        }

        // ─── メニュー階層ピッカー ───

        /// <summary>
        /// アバターの VRCExpressionsMenu 階層からドロップダウンで選択する。
        /// </summary>
        private void DrawMenuInstallTarget()
        {
            var item = (CostumeItem)target;
            var avatar = FindAvatarDescriptor(item.transform);

            if (avatar == null || avatar.expressionsMenu == null)
            {
                EditorGUILayout.PropertyField(_installTargetMenu,
                    new GUIContent("インストール先"));
                return;
            }

            // メニュー階層をフラット化
            var entries = new List<MenuEntry>();
            entries.Add(new MenuEntry { Path = "(ルート)", Menu = null });
            CollectMenuHierarchy(avatar.expressionsMenu, "", entries);

            // 現在選択中のインデックスを探す
            var currentMenu = (VRCExpressionsMenu)_installTargetMenu.objectReferenceValue;
            int selectedIndex = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Menu == currentMenu)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // ドロップダウン表示
            var labels = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                labels[i] = entries[i].Path;

            int newIndex = EditorGUILayout.Popup("インストール先", selectedIndex, labels);
            if (newIndex != selectedIndex)
            {
                _installTargetMenu.objectReferenceValue = entries[newIndex].Menu;
            }
        }

        private struct MenuEntry
        {
            public string Path;
            public VRCExpressionsMenu Menu;
        }

        /// <summary>
        /// VRCExpressionsMenu を再帰的にたどって全サブメニューを収集する。
        /// </summary>
        private static void CollectMenuHierarchy(
            VRCExpressionsMenu menu, string parentPath,
            List<MenuEntry> entries, int depth = 0)
        {
            if (menu == null || depth > 10) return; // 無限再帰防止

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

        /// <summary>
        /// Transform から親をたどって VRCAvatarDescriptor を探す。
        /// </summary>
        private static VRCAvatarDescriptor FindAvatarDescriptor(Transform t)
        {
            while (t != null)
            {
                var desc = t.GetComponent<VRCAvatarDescriptor>();
                if (desc != null) return desc;
                t = t.parent;
            }
            return null;
        }
    }
}
