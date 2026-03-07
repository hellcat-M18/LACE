# Changelog

## [0.3.1] - 2026-03-07

### Added
- VPM リポジトリ配布に必要なファイル一式を追加
  - `package.json` に `url` フィールドを追加（GitHub Releases の zip を参照）
  - リポジトリルートに `source.json` を追加（VPM リポジトリのメタ情報）
  - `.github/workflows/release.yml` を追加（リリース時に zip 生成・添付を自動化）
  - `Website/` を追加（VPM リポジトリ一覧ページ生成用テンプレート）

### Changed
- VPM 公開用ファイルの配置を、パッケージリポジトリルート基準に修正
- GitHub Actions の構成を、Release 作成と VPM 一覧生成・Pages 配信の 2 ワークフローに分離
- VPM 一覧生成処理を `package-list-action` の直接 checkout 実行に変更し、存在しないタグ参照を解消

## [0.3.0] - 2026-03-07

### Changed
- 設定UIを Inspector 中心から `Tools/LACE/Dashboard` 中心のワークフローに変更
- ダッシュボードを縦積み一覧から、左ペイン一覧 + 右ペイン詳細の2ペインUIに変更
- CostumeItem 一覧をタブ風の選択UIに変更し、同名パラメータにはインデックス表示を追加
- 条件エディタを自然言語ベースのビルダーに変更し、追加条件を `すべて満たす` / `いずれかを満たす` で編集できるように改善
- 条件セクション内で、自身の条件をグレーアウトした固定条件として表示するように変更
- 条件関連文言を `条件式` ベースから `切り替え条件` / `条件を満たした時` ベースの表現に変更
- GameObject モードの表示条件セクションを削除
- BlendShape モードの値設定を専用セクションから制御対象セクション内へ移動
- 新規作成フォームで、選択中オブジェクト数の代わりに選択オブジェクト名一覧を表示するよう変更

### Added
- ダッシュボードの新規作成フォームに選択中対象の一覧表示を追加
- 追加条件セクションの折りたたみ表示を追加

### Removed
- CostumeItem Inspector から詳細設定UIを削除し、ダッシュボード導線中心の最小構成に変更

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
