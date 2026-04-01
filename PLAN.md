## WindowResizer SimHub プラグイン実装プラン（スナップショット）

> 元プランファイル: `.cursor/plans/windowresizer-simhub-plugin_9bac58f6.plan.md`  
> 内容を WindowResizer プロジェクト直下に保存したものです。

### 1. プロジェクト基盤の用意

- **SimHub プラグインプロジェクト作成**
  - SimHub の公式テンプレート or 既存サンプルから C# クラスライブラリを作成し、プロジェクト名を `WindowResizer` に設定。
  - プラグインのメタ情報（表示名、説明、バージョン）を `WindowResizer` として定義。
  - ビルドした DLL が SimHub に読み込まれ、最低限のログが出るところまで確認。

### 2. ウインドウ操作ロジックの移植

- **WinAPI ラッパ `WindowManager` の実装**
  - 既存 Resize Raccoon の Rust 実装を参考に、C# で以下を実装:
    - プロセス名 / ウインドウタイトル / クラス名でターゲットウインドウを列挙・選択する関数。
    - 位置・サイズ変更（`SetWindowPos` / `MoveWindow` 相当）。
    - 枠削除・スタイル変更（`GetWindowLongPtr` / `SetWindowLongPtr`）。
    - 前面化＆フォーカス制御（`SetForegroundWindow` / `SetWindowPos`）。
  - 最小テストとして、「現在アクティブなウインドウを固定座標に移動＆リサイズする」メソッドを用意し、SimHub からのテストアクションで動作確認。

### 3. プロファイルモデルと永続化

- **C# `Profile` クラス定義**
  - フィールド例:
    - `Id` (GUID)
    - `Name`
    - `ProcessName`
    - `WindowTitleContains` / `WindowClassContains`
    - `X, Y, Width, Height`
    - `RemoveBorders`, `BringToFront`, `BringToFrontTakeFocus`
    - `IncludeInCycle`
  - 既存の `Profile`（Rust 版）と概ね互換になるよう設計。
- **JSON 保存/読み込み**
  - `profiles.json` を `AppData/SimHub/WindowResizer` 等に保存。
  - 起動時に JSON を読み込み `List<Profile>` に展開、変更時は即時保存 or 明示セーブボタンで保存。

### 4. プロファイル適用ロジック

- **`ProfileManager` 実装**
  - プロファイル一覧のロード／セーブ、追加・編集・削除を管理。
  - `ApplyProfile(Profile profile)` メソッドで:
    - `WindowManager` を使って対象ウインドウを検索。
    - 見つかったウインドウに対して位置・サイズ・枠・前面化などを適用。
  - ウインドウが見つからない場合のログ・エラーメッセージ方針を決める（SimHub ログ出力）。

### 5. NEXT / PREV サイクルの実装

- **サイクル状態 `CycleState` の実装（WindowResizerPlugin 内部で実現）**
  - `IncludeInCycle == true` なプロファイルだけを順番リストとして扱う。
  - 現在インデックスをメモリ上に保持し、
    - `NextProfile()` で +1（末尾なら先頭）、
    - `PrevProfile()` で -1（先頭なら末尾）。
  - 実際には `ProfileManager` と連携し、`ApplyProfile` を呼び出すだけにする。

### 6. SimHub アクションとの結合

- **公開アクションの定義**
  - SimHub に以下のアクション（メソッド）を公開する想定:
    - `WindowResizer.NextProfile`
    - `WindowResizer.PrevProfile`
    - `WindowResizer.ApplyProfileByName(name)`（任意）
  - 各アクションが呼ばれたときに `WindowResizerPlugin` 内のサイクル処理／`ProfileManager` を通じてウインドウ操作を行う。
- **入力デバイスは SimHub 側に委譲**
  - ユーザーは SimHub の Input / Mapping から任意ボタンに上記アクションをバインドするだけでよい。

### 7. 設定 UI の実装

- **SimHub 設定パネル**
  - プロファイル一覧を表示し、追加・編集・削除ができる簡易 UI を作成。
  - 個々のプロファイルについて、
    - 対象プロセス名とフィルタ
    - 位置・サイズ
    - フラグ（枠削除／前面化／IncludeInCycle）
    を編集できるようにする。
  - 変更内容は `profiles.json` に反映。

### 8. 既存動作とのギャップ調整と仕上げ

- **Resize Raccoon Extended との比較テスト**
  - 同じプロファイル条件で、Tauri 版と SimHub 版で挙動を比較。
  - 特に注視する点:
    - 前面化とフォーカスのタイミング。
    - ゲームがフルスクリーン / ボーダーレス時の挙動。
  - 必要に応じて遅延やフォールバックロジックを追加。
- **ログとエラーハンドリング**
  - ターゲットウインドウが見つからない、権限で失敗するなどのケースに対して、SimHub ログに分かりやすいメッセージを出す。
- **ドキュメント更新**
  - ビルド手順、SimHub への導入方法、アクションの設定例（ボタン割当サンプル）を README などに追記。

