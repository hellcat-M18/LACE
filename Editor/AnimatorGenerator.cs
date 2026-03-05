using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Lace.Runtime;

namespace Lace.Editor
{
    /// <summary>
    /// CostumeItem の条件式（DNF）から Animator Controller の
    /// Layer / State / Transition / AnimationClip を生成する。
    /// </summary>
    public static class AnimatorGenerator
    {
        /// <summary>
        /// 全 CostumeItem を処理して AnimatorController を生成する。
        /// </summary>
        public static AnimatorController Generate(
            CostumeItem[] items,
            Transform avatarRoot,
            UnityEngine.Object assetContainer)
        {
            var controller = new AnimatorController();
            controller.name = "LACE FX";

            // 全条件式からパラメータ名を収集
            var paramNames = new HashSet<string>();
            foreach (var item in items)
            {
                var cond = GetEffectiveCondition(item);
                if (cond != null) CollectParameterNames(cond, paramNames);
            }

            // AnimatorController にパラメータを追加
            foreach (var name in paramNames)
            {
                controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            }

            // 各 CostumeItem に対してレイヤーを作成
            var layers = new List<AnimatorControllerLayer>();
            foreach (var item in items)
            {
                var condition = GetEffectiveCondition(item);
                if (condition == null) continue;

                var layer = CreateLayer(item, condition, avatarRoot, assetContainer);
                if (layer != null)
                    layers.Add(layer);
            }

            controller.layers = layers.ToArray();

            if (assetContainer != null)
                AssetDatabase.AddObjectToAsset(controller, assetContainer);

            return controller;
        }

        /// <summary>
        /// CostumeItem の有効条件式を取得する。
        /// 条件が空（Param タイプでパラメータ名が空）の場合は、
        /// メニュー生成が有効なときのみ自身のパラメータ名を使用する。
        /// </summary>
        public static Condition GetEffectiveCondition(CostumeItem item)
        {
            bool hasSelfToggleParam = item.generateMenuItem && !string.IsNullOrEmpty(item.parameterName);
            bool hasCondition = item.condition != null
                && !(item.condition.type == ConditionType.Param
                     && string.IsNullOrEmpty(item.condition.parameterName));

            if (hasCondition && hasSelfToggleParam)
            {
                // 自身のパラメータを暗黙的に AND で結合
                return Condition.And(
                    Condition.Param(item.parameterName, true),
                    item.condition);
            }

            if (hasCondition)
                return item.condition;

            // 条件が空 → メニュー生成時のみ自身のパラメータで単純トグル
            if (hasSelfToggleParam)
                return Condition.Param(item.parameterName, true);

            return null;
        }

        // ─── Private helpers ───

        private static void CollectParameterNames(Condition c, HashSet<string> names)
        {
            if (c == null) return;

            if (c.type == ConditionType.Param)
            {
                if (!string.IsNullOrEmpty(c.parameterName))
                    names.Add(c.parameterName);
            }
            else if (c.children != null)
            {
                foreach (var child in c.children)
                    CollectParameterNames(child, names);
            }
        }

        private static AnimatorControllerLayer CreateLayer(
            CostumeItem item,
            Condition condition,
            Transform avatarRoot,
            UnityEngine.Object assetContainer)
        {
            // AnimationClip 作成
            var matchClip = new AnimationClip();
            matchClip.name = $"LACE_{item.name}_Match";
            var unmatchClip = new AnimationClip();
            unmatchClip.name = $"LACE_{item.name}_Unmatch";

            bool hasAnyCurves = false;

            if (item.targetObjects != null)
            {
                foreach (var targetGo in item.targetObjects)
                {
                    if (targetGo == null) continue;

                    var targetPath = GetRelativePath(avatarRoot, targetGo.transform);
                    if (targetPath == null)
                    {
                        Debug.LogWarning($"[LACE] {item.name}: ターゲット「{targetGo.name}」がアバタールート配下に存在しません");
                        continue;
                    }

                    if (item.target == RuleTarget.GameObject)
                    {
                        // GameObject ON/OFF
                        matchClip.SetCurve(targetPath, typeof(GameObject), "m_IsActive",
                            new AnimationCurve(new Keyframe(0, item.matchActive ? 1f : 0f)));
                        unmatchClip.SetCurve(targetPath, typeof(GameObject), "m_IsActive",
                            new AnimationCurve(new Keyframe(0, item.unmatchActive ? 1f : 0f)));
                        hasAnyCurves = true;
                    }
                    else // BlendShape
                    {
                        var smr = targetGo.GetComponent<SkinnedMeshRenderer>();
                        if (smr == null || smr.sharedMesh == null) continue;

                        if (item.blendShapeNames != null)
                        {
                            foreach (var shapeName in item.blendShapeNames)
                            {
                                if (string.IsNullOrEmpty(shapeName)) continue;
                                // このレンダラーにそのシェイプキーがなければスキップ
                                if (smr.sharedMesh.GetBlendShapeIndex(shapeName) < 0) continue;

                                matchClip.SetCurve(targetPath, typeof(SkinnedMeshRenderer),
                                    "blendShape." + shapeName,
                                    new AnimationCurve(new Keyframe(0, item.matchValue)));
                                unmatchClip.SetCurve(targetPath, typeof(SkinnedMeshRenderer),
                                    "blendShape." + shapeName,
                                    new AnimationCurve(new Keyframe(0, item.unmatchValue)));
                                hasAnyCurves = true;
                            }
                        }
                    }
                }
            }

            if (!hasAnyCurves)
            {
                Debug.LogWarning($"[LACE] {item.name}: 有効なアニメーションカーブがありません");
                return null;
            }

            SaveAsset(matchClip, assetContainer);
            SaveAsset(unmatchClip, assetContainer);

            // StateMachine 作成
            var sm = new AnimatorStateMachine();
            sm.name = $"LACE_{item.name}";
            SaveAsset(sm, assetContainer);

            // State 作成
            var unmatchState = new AnimatorState();
            unmatchState.name = "Unmatch";
            unmatchState.motion = unmatchClip;
            unmatchState.writeDefaultValues = true;

            var matchState = new AnimatorState();
            matchState.name = "Match";
            matchState.motion = matchClip;
            matchState.writeDefaultValues = true;

            SaveAsset(unmatchState, assetContainer);
            SaveAsset(matchState, assetContainer);

            sm.states = new[]
            {
                new ChildAnimatorState { state = unmatchState, position = new Vector3(250, 0, 0) },
                new ChildAnimatorState { state = matchState, position = new Vector3(250, 120, 0) },
            };
            sm.defaultState = item.defaultValue ? matchState : unmatchState;

            // Forward transitions (unmatch → match): 条件 DNF
            var forwardDnf = DnfConverter.ToDnf(condition);
            unmatchState.transitions = CreateTransitions(matchState, forwardDnf, assetContainer);

            // Backward transitions (match → unmatch): 否定条件 DNF
            var negated = DnfConverter.Negate(condition);
            var backwardDnf = DnfConverter.ToDnf(negated);
            matchState.transitions = CreateTransitions(unmatchState, backwardDnf, assetContainer);

            return new AnimatorControllerLayer
            {
                name = $"LACE: {item.name}",
                stateMachine = sm,
                defaultWeight = 1f,
            };
        }

        private static AnimatorStateTransition[] CreateTransitions(
            AnimatorState destination,
            DnfConverter.Dnf dnf,
            UnityEngine.Object assetContainer)
        {
            var transitions = new List<AnimatorStateTransition>();

            foreach (var conj in dnf.Conjunctions)
            {
                if (conj.Literals.Count == 0) continue;

                var t = new AnimatorStateTransition();
                t.destinationState = destination;
                t.hasExitTime = false;
                t.duration = 0;
                t.canTransitionToSelf = false;

                t.conditions = conj.Literals.Select(lit => new AnimatorCondition
                {
                    mode = lit.IsPositive ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    parameter = lit.ParameterName,
                    threshold = 0,
                }).ToArray();

                SaveAsset(t, assetContainer);
                transitions.Add(t);
            }

            return transitions.ToArray();
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";

            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", parts) : null;
        }

        private static void SaveAsset(UnityEngine.Object obj, UnityEngine.Object container)
        {
            if (obj != null && container != null)
            {
                obj.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(obj, container);
            }
        }
    }
}
