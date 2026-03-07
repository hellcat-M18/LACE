using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Lace.Runtime;

[assembly: ExportsPlugin(typeof(Lace.Editor.LacePlugin))]

namespace Lace.Editor
{
    /// <summary>
    /// LACE の NDMF プラグインエントリポイント。
    /// BuildPhase.Generating で CostumeItem を処理し、
    /// Modular Avatar 経由で Expression Menu・Parameters・Animator Controller を生成する。
    /// </summary>
    public class LacePlugin : Plugin<LacePlugin>
    {
        public override string DisplayName => "LACE";
        public override string QualifiedName => "tool.hellcat.lace";

        /// <summary>
        /// NDMF ErrorReport 用のダミー Localizer。
        /// キーをそのままメッセージとして表示する。
        /// </summary>
        internal static readonly Localizer L = new Localizer("ja-JP",
            () => new List<(string, Func<string, string>)>
            {
                ("ja-JP", key => key),
                ("en-US", key => key),
            });

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).Run("LACE: Generate Costume System", ctx =>
            {
                var avatarRoot = ctx.AvatarRootObject;
                var items = avatarRoot.GetComponentsInChildren<CostumeItem>(true);

                if (items.Length == 0) return;

                // バリデーション
                bool hasErrors = ValidateItems(items);
                if (hasErrors) return;

                // コンテナ GameObject を作成
                var container = new GameObject("LACE Generated");
                container.transform.SetParent(avatarRoot.transform, false);

                // expressionsMenu 階層のルート基準パス（SubMenu名の連結）を収集
                var avatarDesc = avatarRoot.GetComponent<VRCAvatarDescriptor>();
                var menuPathLookup = BuildMenuPathLookup(avatarDesc != null ? avatarDesc.expressionsMenu : null);

                // Modular Avatar でメニュー生成
                GenerateMenuItems(container.transform, items, menuPathLookup);

                // Modular Avatar でパラメータ宣言
                GenerateParameters(container, items);

                // Animator Controller 生成
                var controller = AnimatorGenerator.Generate(
                    items, avatarRoot.transform, ctx.AssetContainer);

                // Modular Avatar MergeAnimator でマージ
                var mergeAnimator = container.AddComponent<ModularAvatarMergeAnimator>();
                mergeAnimator.animator = controller;
                mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
                mergeAnimator.matchAvatarWriteDefaults = true;
                mergeAnimator.deleteAttachedAnimator = true;

                // ─── LACE コンポーネントのクリーンアップ ───
                // 処理済みの CostumeItem を破棄する。
                // これにより AAO Trace and Optimize の HasUnsupportedComponents チェックで
                // 「サポート外コンポーネント」と判定されるのを防ぎ、
                // 自動メッシュマージが正常に機能する。
                foreach (var item in items)
                    UnityEngine.Object.DestroyImmediate(item);
            });
        }

        /// <summary>
        /// generateMenuItem=true の CostumeItem から MA メニュー階層を作成する。
        /// </summary>
        private static void GenerateMenuItems(
            Transform container,
            CostumeItem[] items,
            Dictionary<int, string> menuPathLookup)
        {
            // installTargetMenu ごとにグルーピングして MenuInstaller を作る
            var groups = new Dictionary<int, Transform>(); // instanceID → Transform

            foreach (var item in items)
            {
                if (!item.generateMenuItem) continue;
                if (string.IsNullOrEmpty(item.parameterName)) continue;

                // installTargetMenu ごとのコンテナを取得/作成
                var groupRoot = GetOrCreateInstallGroup(
                    container, item.installTargetMenu, groups);

                // menuPath に基づいて親を探す／作る
                var normalizedPath = NormalizeMenuPath(
                    item.menuPath,
                    item.installTargetMenu,
                    menuPathLookup);
                var parent = GetOrCreateMenuParent(groupRoot, normalizedPath);

                // メニューアイテム用 GameObject
                var menuGo = new GameObject(item.parameterName);
                menuGo.transform.SetParent(parent, false);

                var menuItem = menuGo.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = item.parameterName,
                    },
                    value = 1,
                    icon = item.icon,
                };
                menuItem.isDefault = item.defaultValue;
                menuItem.isSynced = item.parameterSynced;
                menuItem.isSaved = item.parameterSaved;
            }
        }

        /// <summary>
        /// installTargetMenu ごとの MenuInstaller コンテナを取得/作成する。
        /// </summary>
        private static Transform GetOrCreateInstallGroup(
            Transform container,
            VRCExpressionsMenu targetMenu,
            Dictionary<int, Transform> groups)
        {
            // null → 0 をキーに、それ以外は instanceID
            int key = targetMenu != null ? targetMenu.GetInstanceID() : 0;
            if (groups.TryGetValue(key, out var existing))
                return existing;

            // 新しいグループコンテナを作成
            var groupName = targetMenu != null ? $"Menu ({targetMenu.name})" : "Menu (Root)";
            var groupGo = new GameObject(groupName);
            groupGo.transform.SetParent(container, false);

            // ModularAvatarMenuInstaller でインストール先を指定
            var installer = groupGo.AddComponent<ModularAvatarMenuInstaller>();
            installer.installTargetMenu = targetMenu; // null = ルートメニュー

            // ModularAvatarMenuGroup で子をメニューソースにする
            groupGo.AddComponent<ModularAvatarMenuGroup>();

            groups[key] = groupGo.transform;
            return groupGo.transform;
        }

        /// <summary>
        /// menuPath（例: "Costume/Upper"）に対応する親 Transform を取得／作成する。
        /// 各パスセグメントは SubMenu 型の ModularAvatarMenuItem になる。
        /// </summary>
        private static Transform GetOrCreateMenuParent(Transform root, string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return root;

            var parts = menuPath.Split('/');
            var current = root;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                var child = current.Find(part);
                if (child == null)
                {
                    var go = new GameObject(part);
                    go.transform.SetParent(current, false);

                    var subMenu = go.AddComponent<ModularAvatarMenuItem>();
                    subMenu.Control = new VRCExpressionsMenu.Control
                    {
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    };
                    subMenu.MenuSource = SubmenuSource.Children;

                    child = go.transform;
                }
                current = child;
            }

            return current;
        }

        /// <summary>
        /// menuPath を「インストール先メニュー基準」として扱う。
        /// 互換性のため、menuPath がルート基準で書かれており、先頭がインストール先のパス
        /// （例: installTarget=Costume, menuPath="Costume/Upper"）を含む場合はその接頭辞を除去する。
        /// </summary>
        private static string NormalizeMenuPath(
            string menuPath,
            VRCExpressionsMenu installTargetMenu,
            Dictionary<int, string> menuPathLookup)
        {
            if (string.IsNullOrEmpty(menuPath)) return string.Empty;

            var normalized = menuPath.Trim().Trim('/');
            if (string.IsNullOrEmpty(normalized)) return string.Empty;

            if (installTargetMenu == null) return normalized; // ルートインストールならそのまま
            if (menuPathLookup == null) return normalized;

            var installId = installTargetMenu.GetInstanceID();
            if (!menuPathLookup.TryGetValue(installId, out var installPath))
                return normalized;

            installPath = (installPath ?? string.Empty).Trim().Trim('/');
            if (string.IsNullOrEmpty(installPath)) return normalized;

            // 完全一致（install先と同じパス）が来た場合は「直下」扱い
            if (normalized == installPath) return string.Empty;

            // "Costume/Upper" のように installPath を先頭に含むなら剥がす
            var prefix = installPath + "/";
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                return normalized.Substring(prefix.Length).Trim('/');
            }

            return normalized;
        }

        /// <summary>
        /// expressionsMenu のサブメニュー階層を走査して、各メニューアセットのルート基準パスを返す。
        /// 例: "Costume/Upper"。
        /// </summary>
        private static Dictionary<int, string> BuildMenuPathLookup(VRCExpressionsMenu rootMenu)
        {
            var lookup = new Dictionary<int, string>();
            CollectMenuPaths(rootMenu, string.Empty, lookup, 0);
            return lookup;
        }

        private static void CollectMenuPaths(
            VRCExpressionsMenu menu,
            string parentPath,
            Dictionary<int, string> lookup,
            int depth)
        {
            if (menu == null || depth > 10) return;

            // ルート自体のパスは空文字として登録（ただしルートは installTarget に使われない場合が多い）
            var menuId = menu.GetInstanceID();
            if (!lookup.ContainsKey(menuId))
                lookup[menuId] = parentPath;

            foreach (var control in menu.controls)
            {
                if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                if (control.subMenu == null) continue;

                var path = string.IsNullOrEmpty(parentPath)
                    ? control.name
                    : parentPath + "/" + control.name;

                var subId = control.subMenu.GetInstanceID();
                if (!lookup.ContainsKey(subId))
                    lookup[subId] = path;

                CollectMenuPaths(control.subMenu, path, lookup, depth + 1);
            }
        }

        /// <summary>
        /// 全 CostumeItem（generateMenuItem=true）のパラメータを MA に登録する。
        /// </summary>
        private static void GenerateParameters(GameObject container, CostumeItem[] items)
        {
            var maParams = container.AddComponent<ModularAvatarParameters>();
            maParams.parameters = new List<ParameterConfig>();

            var added = new HashSet<string>();

            foreach (var item in items)
            {
                if (!item.generateMenuItem) continue;
                if (string.IsNullOrEmpty(item.parameterName)) continue;
                if (!added.Add(item.parameterName)) continue; // 重複スキップ

                maParams.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = item.parameterName,
                    syncType = ParameterSyncType.Bool,
                    defaultValue = item.defaultValue ? 1f : 0f,
                    saved = item.parameterSaved,
                    hasExplicitDefaultValue = true,
                    internalParameter = false,
                    isPrefix = false,
                    localOnly = !item.parameterSynced,
                });
            }
        }

        // ─── バリデーション ───

        /// <summary>
        /// CostumeItem のバリデーションを行い、問題があれば NDMF ErrorReport に出力する。
        /// </summary>
        /// <returns>致命的エラーがある場合 true</returns>
        private static bool ValidateItems(CostumeItem[] items)
        {
            bool hasErrors = false;

            // パラメータ名ごとの defaultValue 不整合チェック用
            var paramDefaults = new Dictionary<string, bool>();

            // パラメータ名ごとの synced/saved 不整合チェック用
            var paramSynced = new Dictionary<string, bool>();
            var paramSaved = new Dictionary<string, bool>();

            // 重複制御チェック用: GameObject → それを targetObjects に含む CostumeItem リスト
            var targetOwnerMap = new Dictionary<int, List<CostumeItem>>();

            foreach (var item in items)
            {
                // パラメータ名チェック
                if (item.generateMenuItem && string.IsNullOrEmpty(item.parameterName))
                {
                    ErrorReport.ReportError(L, ErrorSeverity.Error,
                        $"[LACE] {item.name}: メニュー生成が有効ですがパラメータ名が空です", item);
                    hasErrors = true;
                }

                // targetObjects の null チェック＋重複制御マップ構築
                if (item.targetObjects != null)
                {
                    for (int i = item.targetObjects.Count - 1; i >= 0; i--)
                    {
                        if (item.targetObjects[i] == null)
                        {
                            ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                $"[LACE] {item.name}: ターゲットリストに空のエントリがあります（インデックス {i}）", item);
                            item.targetObjects.RemoveAt(i);
                        }
                        else
                        {
                            var targetId = item.targetObjects[i].GetInstanceID();
                            if (!targetOwnerMap.TryGetValue(targetId, out var owners))
                            {
                                owners = new List<CostumeItem>();
                                targetOwnerMap[targetId] = owners;
                            }
                            owners.Add(item);
                        }
                    }

                    if (item.targetObjects.Count == 0)
                    {
                        ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                            $"[LACE] {item.name}: ターゲットオブジェクトが指定されていません", item);
                    }
                }
                else
                {
                    ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                        $"[LACE] {item.name}: ターゲットオブジェクトが指定されていません", item);
                }

                // BlendShape バリデーション
                if (item.target == RuleTarget.BlendShape)
                {
                    var renderers = item.GetTargetRenderers();
                    if (renderers.Count == 0)
                    {
                        ErrorReport.ReportError(L, ErrorSeverity.Error,
                            $"[LACE] {item.name}: BlendShape モードですがターゲットに SkinnedMeshRenderer が見つかりません", item);
                        hasErrors = true;
                    }
                    else if (item.blendShapeNames == null || item.blendShapeNames.Count == 0)
                    {
                        ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                            $"[LACE] {item.name}: BlendShape モードですがシェイプキーが選択されていません", item);
                    }
                    else
                    {
                        // 存在しないシェイプキー名チェック（全レンダラーに存在しない場合のみ警告）
                        foreach (var shapeName in item.blendShapeNames)
                        {
                            if (string.IsNullOrEmpty(shapeName)) continue;
                            bool foundInAny = false;
                            foreach (var smr in renderers)
                            {
                                if (smr.sharedMesh != null && smr.sharedMesh.GetBlendShapeIndex(shapeName) >= 0)
                                {
                                    foundInAny = true;
                                    break;
                                }
                            }
                            if (!foundInAny)
                            {
                                ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                    $"[LACE] {item.name}: シェイプキー「{shapeName}」がどのターゲットレンダラーにも存在しません", item);
                            }
                        }
                    }
                }

                // 同一パラメータの defaultValue 不整合チェック
                if (!string.IsNullOrEmpty(item.parameterName) && item.generateMenuItem)
                {
                    if (paramDefaults.TryGetValue(item.parameterName, out var existingDefault))
                    {
                        if (existingDefault != item.defaultValue)
                        {
                            ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                $"[LACE] パラメータ「{item.parameterName}」の初期値が不整合です。" +
                                $"{item.name} では {(item.defaultValue ? "ON" : "OFF")} ですが、" +
                                $"他のアイテムでは {(existingDefault ? "ON" : "OFF")} です", item);
                        }
                    }
                    else
                    {
                        paramDefaults[item.parameterName] = item.defaultValue;
                    }

                    if (paramSynced.TryGetValue(item.parameterName, out var existingSynced))
                    {
                        if (existingSynced != item.parameterSynced)
                        {
                            ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                $"[LACE] パラメータ「{item.parameterName}」の同期設定が不整合です。" +
                                $"{item.name} では {(item.parameterSynced ? "同期" : "非同期")} ですが、" +
                                $"他のアイテムでは {(existingSynced ? "同期" : "非同期")} です", item);
                        }
                    }
                    else
                    {
                        paramSynced[item.parameterName] = item.parameterSynced;
                    }

                    if (paramSaved.TryGetValue(item.parameterName, out var existingSaved))
                    {
                        if (existingSaved != item.parameterSaved)
                        {
                            ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                $"[LACE] パラメータ「{item.parameterName}」の保持設定が不整合です。" +
                                $"{item.name} では {(item.parameterSaved ? "保持" : "非保持")} ですが、" +
                                $"他のアイテムでは {(existingSaved ? "保持" : "非保持")} です", item);
                        }
                    }
                    else
                    {
                        paramSaved[item.parameterName] = item.parameterSaved;
                    }
                }

                // 条件式の参照パラメータが存在するかチェック
                if (item.condition != null)
                {
                    ValidateConditionParams(item, item.condition, items);
                }
            }

            // ─── 重複制御の警告 ───
            // 同一 GameObject が複数の CostumeItem の targetObjects に含まれている場合に警告
            // ただし BlendShape モード同士でシェイプキーが重複していなければ問題ないためスキップ
            foreach (var kvp in targetOwnerMap)
            {
                if (kvp.Value.Count <= 1) continue;

                var targetObj = EditorUtility.InstanceIDToObject(kvp.Key) as GameObject;

                // BlendShape モード同士の場合: 実際にシェイプキーが重複しているペアのみ警告
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    for (int j = i + 1; j < kvp.Value.Count; j++)
                    {
                        var a = kvp.Value[i];
                        var b = kvp.Value[j];

                        if (a.target == RuleTarget.BlendShape && b.target == RuleTarget.BlendShape)
                        {
                            // 両方 BlendShape → シェイプキーの重複を確認
                            if (a.blendShapeNames == null || b.blendShapeNames == null) continue;
                            var overlapping = a.blendShapeNames.Intersect(b.blendShapeNames).ToList();
                            if (overlapping.Count > 0)
                            {
                                ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                    $"[LACE] オブジェクト「{targetObj?.name}」のシェイプキー「{string.Join(", ", overlapping)}」が " +
                                    $"CostumeItem「{a.name}」と「{b.name}」で重複して制御されています",
                                    a);
                            }
                        }
                        else
                        {
                            // いずれかが GameObject モード → 同一ターゲットは競合の可能性
                            ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                                $"[LACE] オブジェクト「{targetObj?.name}」が CostumeItem「{a.name}」と「{b.name}」のターゲットに " +
                                $"登録されています。意図しない重複の可能性があります",
                                a);
                        }
                    }
                }
            }

            return hasErrors;
        }

        /// <summary>
        /// 条件式内で参照されるパラメータ名が、いずれかの CostumeItem で定義されているか確認する。
        /// </summary>
        private static void ValidateConditionParams(CostumeItem owner, Condition condition, CostumeItem[] allItems)
        {
            if (condition == null) return;

            if (condition.type == ConditionType.Param)
            {
                if (!string.IsNullOrEmpty(condition.parameterName))
                {
                    bool found = false;
                    foreach (var item in allItems)
                    {
                        if (item.parameterName == condition.parameterName)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        ErrorReport.ReportError(L, ErrorSeverity.NonFatal,
                            $"[LACE] {owner.name}: 条件式で参照されるパラメータ「{condition.parameterName}」を持つ CostumeItem が見つかりません", owner);
                    }
                }
            }
            else if (condition.children != null)
            {
                foreach (var child in condition.children)
                    ValidateConditionParams(owner, child, allItems);
            }
        }
    }
}
