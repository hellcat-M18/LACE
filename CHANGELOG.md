# Changelog

## [0.2.0] - 2026-03-05

### Changed
- **破壊的変更**: CostumeItem を制御対象の GO にアタッチする方式から、空の GO にアタッチして `targetObjects` で制御対象を指定する方式に変更
- `linkedObjects`（連動オブジェクト）と暗黙の自己ターゲットを `targetObjects`（ターゲットオブジェクト）リスト1本に統合
- `targetRenderer` フィールドを廃止。BlendShape モードでは `targetObjects` 内の全 GO から SkinnedMeshRenderer を自動収集
- BlendShapePickerWindow を複数レンダラー対応に変更（シェイプキーの和集合を表示、所属レンダラー名を併記）
- AnimatorGenerator が `targetObjects` 内の全 GO に対してアニメーションカーブを生成するよう変更
- バリデーションを `targetObjects` ベースに更新（空リスト・重複制御の検出）
- サブメニューパスをインストール先メニュー基準に変更（ルート基準から）
- 条件式エディタをコンパクトなUIに刷新（パラメータ行を1行表示、セパレータ、グループインデント）
- 制御対象タイプを2択ボタン（GameObject / BlendShape）に変更
- GameObject モード時のセクション名を「表示条件」に変更

### Added
- パラメータ設定（Synced / Saved）オプションを追加（折りたたみフォルダウト内）
- 条件式エディタに自身パラメータの読み取り専用表示を追加
- 同一パラメータ名で Synced/Saved が不一致の場合のバリデーション警告
- コンポーネント追加時にパラメータ名をオブジェクト名で自動設定

### Removed
- CostumeItemLink マーカーコンポーネントを廃止
- CostumeItemLinkEditor を削除
- CostumeItem / CostumeItemLink の同一 GO 排他制御を削除

## [0.1.0] - 2026-02-24

### Added
- 初回リリース
- LACE Costume Item 統合コンポーネント（メニュー生成 + 条件制御）
- LACE Costume Item Link マーカーコンポーネント
- 条件式の DNF 変換（AND/OR/NOT の論理演算）
- Animator Controller 自動生成（デフォルトステート対応）
- Expression Menu & Parameters 自動生成（Modular Avatar 統合）
- メニューインストール先階層ドロップダウンピッカー
- BlendShape 複数選択ポップアップ（検索・一括選択対応）
- レンダラー自動設定（同一 GameObject 上の SkinnedMeshRenderer）
- 連動オブジェクト（linkedObjects）による複数オブジェクト同時トグル
- 条件式へのドラッグ＆ドロップパラメータ自動入力
- 自身のパラメータの暗黙的 AND 結合（追加条件方式）
- ビルド時バリデーション（設定ミス・不整合検出）
- 同一 GameObject への複数 CostumeItem 配置対応
- NDMF ErrorReport UI 統合（ビルドエラーを NDMF エラーレポート画面に表示）
- パラメータコスト表示（VRChat Synced Parameter 256 ビット上限に対する使用量）
- 有効条件式のサマリープレビュー（Inspector 内で人間が読める形式で表示）
- 重複制御の警告（連動オブジェクトが複数 CostumeItem に登録されている場合）
- MIT ライセンス
