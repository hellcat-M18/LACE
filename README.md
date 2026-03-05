# LACE - Logic-driven Avatar Costume Engine

VRChat アバター向けの NDMF プラグイン。  
衣装パーツの ON/OFF トグルと、パラメータの論理演算に基づく条件付き制御（素体シェイプキー・オブジェクト表示）を1つのプラグインで完結させます。

## 機能

### LACE Costume Item
メインコンポーネント。空の GameObject にアタッチし、`ターゲットオブジェクト` で制御対象を指定します。

- **Expression Menu トグル自動生成**: パラメータ名・初期値・インストール先メニュー・サブメニューパスを設定可
- **制御対象**: GameObject の ON/OFF または BlendShape の値制御（複数シェイプキー選択可）
- **ターゲットオブジェクト**: 制御したい GameObject を複数指定可。BlendShape モードでは各 GO の SkinnedMeshRenderer からシェイプキーを和集合で表示
- **条件式**: パラメータの AND/OR/NOT の論理演算で制御。自身のパラメータは自動で含まれるため、追加条件のみ設定すればOK
- **同一 GameObject に複数配置可**: 条件別のシェイプキーグループ等を設定可能
- **AAO 互換**: CostumeItem は制御対象の GO にアタッチしないため、AAO Trace and Optimize のメッシュ自動マージを阻害しません

### その他
- **非破壊**: NDMF ビルドフェーズで処理。元のアバターデータは変更しません
- **Modular Avatar 統合**: メニュー・パラメータ・アニメータの統合に MA を使用
- **DNF 変換**: 条件式を加法標準形に変換し、最適な Animator Transition を生成
- **ビルド時バリデーション**: 設定ミスや不整合を自動検出（NDMF ErrorReport 対応）
- **ドラッグ＆ドロップ**: CostumeItem やターゲットオブジェクトを条件式にドロップしてパラメータ名を自動入力
- **パラメータコスト表示**: VRChat の 256 ビット上限に対する使用量をリアルタイム表示
- **条件式プレビュー**: 有効条件式を人間が読める形で Inspector に表示

## 使い方

### 基本（衣装パーツのトグル）
1. アバター配下に空の GameObject を作成（例: `LACE_Jacket`）
2. `LACE Costume Item` コンポーネントを追加
3. パラメータ名を設定（例: `Jacket`）
4. `ターゲットオブジェクト` に制御したい衣装 GameObject を追加
5. ビルドするだけで Expression Menu にトグルが追加されます

### 複数オブジェクトの同時トグル
1. 1つの CostumeItem の `ターゲットオブジェクト` に、同時にトグルしたい全 GameObject を追加
2. 全ターゲットが同一条件で ON/OFF されます

### 条件付きシェイプキー制御（シュリンク等）
1. 空 GameObject に CostumeItem を追加
2. `メニュー生成` = OFF、`制御対象` = `BlendShape`
3. `ターゲットオブジェクト` に素体メッシュ等の SkinnedMeshRenderer を持つ GO を追加
4. 「選択...」ボタンで全レンダラーのシェイプキーをまとめて選択
5. 条件式に上着等のパラメータを設定（ドラッグ＆ドロップ対応）

## 要件

- Unity 2022.3
- VRChat Avatars SDK 3.x
- NDMF 1.11.0+
- Modular Avatar 1.16.0+
