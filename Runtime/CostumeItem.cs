using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace Lace.Runtime
{
    /// <summary>
    /// LACE 統合コンポーネント。
    /// 衣装パーツの Expression Menu トグルと、条件式に基づく制御を1つのコンポーネントで行う。
    /// 任意の GameObject（空オブジェクト推奨）にアタッチし、targetObjects で制御対象を指定する。
    /// 同一 GameObject に複数配置可能（例: 条件別のシェイプキーグループ）。
    /// </summary>
    [AddComponentMenu("LACE/LACE Costume Item")]
    public class CostumeItem : MonoBehaviour, IEditorOnly
    {
        // ─── メニュー設定 ───

        /// <summary>
        /// Expression Parameter 名（例: "Jacket"）。
        /// 他の CostumeItem の条件式からこの名前で参照される。
        /// </summary>
        [Tooltip("Expression Parameter 名。条件式で参照する識別子にもなります")]
        public string parameterName;

        /// <summary>Expression Menu にトグルを生成するか</summary>
        [Tooltip("Expression Menu にトグルを生成するか")]
        public bool generateMenuItem = true;

        /// <summary>初期状態（ON/OFF）</summary>
        [Tooltip("初期状態")]
        public bool defaultValue = true;

        /// <summary>パラメータをネットワーク同期するか（VRC Expression Parameters の Synced）</summary>
        [Tooltip("パラメータをネットワーク同期するか（Synced）")]
        public bool parameterSynced = true;

        /// <summary>パラメータを保持するか（VRC Expression Parameters の Saved）</summary>
        [Tooltip("パラメータを保持するか（Saved）")]
        public bool parameterSaved = true;

        /// <summary>メニュー階層パス（例: "Costume/Upper"）。空ならインストール先直下に配置</summary>
        [Tooltip("メニュー階層パス（例: Costume/Upper）。空ならインストール先直下")]
        public string menuPath;

        /// <summary>インストール先の既存メニュー（null ならルートメニュー）</summary>
        [Tooltip("インストール先のメニュー。空ならアバターのルートメニュー")]
        public VRCExpressionsMenu installTargetMenu;

        /// <summary>メニューアイコン</summary>
        [Tooltip("メニューアイコン")]
        public Texture2D icon;

        // ─── 制御対象 ───

        /// <summary>
        /// 制御対象の GameObject リスト。
        /// RuleTarget.GameObject の場合は各 GO の ON/OFF をトグルする。
        /// RuleTarget.BlendShape の場合は各 GO 上の SkinnedMeshRenderer からシェイプキーを制御する。
        /// </summary>
        [Tooltip("制御対象の GameObject をリストで指定します")]
        public List<GameObject> targetObjects = new List<GameObject>();

        /// <summary>制御対象の種類</summary>
        [Tooltip("制御対象の種類")]
        public RuleTarget target = RuleTarget.GameObject;

        /// <summary>BlendShape の場合: 対象ブレンドシェイプ名のリスト（複数選択可）</summary>
        [Tooltip("BlendShape の場合: 対象ブレンドシェイプ名（複数可）")]
        public List<string> blendShapeNames = new List<string>();

        // ─── 条件式 ───

        /// <summary>
        /// 条件式（再帰構造）。
        /// 空の場合（Param タイプでパラメータ名が空）は、
        /// メニュー生成が有効なときのみ自身のパラメータで ON/OFF。
        /// </summary>
        [Tooltip("空の場合は、メニュー生成が有効なときのみ自身のパラメータ名で ON/OFF")]
        public Condition condition = new Condition();

        // ─── 初期化 ───

        /// <summary>コンポーネント追加時にパラメータ名をオブジェクト名で初期化する</summary>
        private void Reset()
        {
            parameterName = gameObject.name;
        }

        // ─── 条件一致時 ───

        /// <summary>条件一致時: シェイプキーの値（0〜100）</summary>
        public float matchValue = 100f;

        /// <summary>条件一致時: オブジェクトの表示状態</summary>
        public bool matchActive = true;

        // ─── 条件不一致時 ───

        /// <summary>条件不一致時: シェイプキーの値（0〜100）</summary>
        public float unmatchValue = 0f;

        /// <summary>条件不一致時: オブジェクトの表示状態</summary>
        public bool unmatchActive = false;

        /// <summary>
        /// targetObjects 内の全 GameObject から SkinnedMeshRenderer を収集する。
        /// BlendShape モードで使用。
        /// </summary>
        public List<SkinnedMeshRenderer> GetTargetRenderers()
        {
            var renderers = new List<SkinnedMeshRenderer>();
            if (targetObjects == null) return renderers;

            foreach (var go in targetObjects)
            {
                if (go == null) continue;
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                    renderers.Add(smr);
            }
            return renderers;
        }
    }

    public enum RuleTarget
    {
        /// <summary>GameObject の ON/OFF</summary>
        GameObject = 0,

        /// <summary>BlendShape の値制御</summary>
        BlendShape = 1,
    }
}
