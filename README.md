# XR Sharing Unity Server

Unityを使用したXRアプリケーション間でのTransform同期システムです。TCP/UDPハイブリッド通信により、リアルタイムでTransformデータを共有できます。

## 🎯 機能概要

- **TCP通信**: 接続管理、セッション管理、重要なデータの送信
- **UDP通信**: リアルタイムTransformデータの送信・受信
- **マルチクライアント対応**: 複数のクライアント間でのTransform同期
- **自動送信**: 継続的なTransform送信によるサーバー学習
- **スムージング**: 滑らかなTransform補間

## 🏗️ アーキテクチャ

```
PC1 (Server)          PC2 (Client)          PC3 (Client)
┌─────────────┐       ┌─────────────┐       ┌─────────────┐
│ SimpleServer│       │XRSharingClient│     │XRSharingClient│
│ - TCP:7777  │◄─────►│ - TCP接続    │     │ - TCP接続    │
│ - UDP:7778  │       │ - UDP送信    │     │ - UDP送信    │
│ - 転送処理  │       │ - 同期処理   │     │ - 同期処理   │
└─────────────┘       └─────────────┘       └─────────────┘
        ▲                       │                       │
        │                       ▼                       ▼
        └─────────── UDP転送 ────────────────┘
```

## 📁 ファイル構成

```
Assets/Scripts/
├── SimpleServer.cs          # サーバー（TCP/UDP）
├── XRSharingClient.cs       # クライアント（送信・受信・同期）
└── XRSharingDataTypes.cs    # 共通データ型定義
```

## 🚀 セットアップ

### 1. サーバー側（PC1）

1. **SimpleServer**コンポーネントをGameObjectに追加
2. **Inspector設定**:
   - `Server URL`: `https://localhost:7777`
   - `Auto Generate Session ID`: ✅
   - `Auto Start`: ✅

3. **実行**: Playボタンを押してサーバー開始

### 2. クライアント側（PC2, PC3）

1. **XRSharingClient**コンポーネントをGameObjectに追加
2. **Inspector設定**:
   - `Server URL`: `https://[サーバーIP]:7777`
   - `Auto Connect`: ✅
   - `Transform Targets`: 送信するTransform配列
   - `Sync Targets`: 同期するTransform配列
   - `Enable Auto Sync`: ✅
   - `Transform Send Interval`: `0.1` (10fps)

3. **実行**: Playボタンを押してクライアント接続

## ⚙️ 設定項目

### SimpleServer

| 項目 | 説明 | デフォルト |
|------|------|------------|
| Server URL | サーバーURL | `https://localhost:7777` |
| Auto Generate Session ID | セッションID自動生成 | `true` |
| Custom Session ID | 固定セッションID | `custom_session_123` |
| Auto Start | 自動開始 | `true` |

### XRSharingClient

| 項目 | 説明 | デフォルト |
|------|------|------------|
| Server URL | サーバーURL | `https://localhost:7777` |
| Auto Connect | 自動接続 | `true` |
| Transform Targets | 送信Transform配列 | `[]` |
| Sync Targets | 同期Transform配列 | `[]` |
| Enable Auto Sync | 自動同期 | `true` |
| Smoothing Factor | スムージング係数 | `0.1` |
| Allow Own Data | 自分のデータ受信 | `true` |
| Transform Send Interval | 送信間隔（秒） | `0.1` |

## 🔄 通信フロー

### 1. 接続確立
```
クライアント → TCP接続 → サーバー
クライアント → UDP接続 → サーバー
サーバー → セッションID送信 → クライアント
```

### 2. Transform送信
```
クライアント → UDP送信 → サーバー
サーバー → UDPエンドポイント学習
サーバー → UDP転送 → 他のクライアント
```

### 3. Transform同期
```
クライアント → Transform受信
クライアント → Sync Targetsに適用
```

## 📊 データ形式

### TransformData (UDP)
```csharp
{
    header: "TRNS",
    userId: "user_1",
    sessionId: "session_123",
    position: Vector3,
    rotation: Quaternion,
    timestamp: long
}
```

### ServerRequest (TCP)
```csharp
{
    message: "Hello World",
    userId: "user_1",
    sessionId: "session_123"
}
```

### ServerResponse (TCP)
```csharp
{
    message: "接続成功",
    timestamp: "2025-09-28 11:00:00",
    success: true,
    sessionId: "session_123",
    fromUserId: "user_1"
}
```

## 🛠️ 開発者向け情報

### カスタムフォーマッター
- `ServerRequestFormatter`
- `ServerResponseFormatter`
- `TransformDataFormatter`

### スレッドセーフ
- UDPエンドポイント管理: `lock`ステートメント使用
- メインスレッド: Unity API呼び出し
- バックグラウンドスレッド: ネットワーク処理

### エラーハンドリング
- 接続エラー: 自動再接続
- シリアライゼーションエラー: ログ出力
- UDP転送エラー: 個別クライアント除外

## 🔧 トラブルシューティング

### よくある問題

1. **Transform同期しない**
   - `Sync Targets`が設定されているか確認
   - `Enable Auto Sync`がONか確認
   - サーバーログでUDP転送成功を確認

2. **接続できない**
   - サーバーが起動しているか確認
   - ファイアウォール設定を確認
   - URL形式が正しいか確認

3. **UDPデータが届かない**
   - サーバーでUDPエンドポイント学習を確認
   - クライアントで継続送信を確認
   - ネットワーク設定を確認

### デバッグログ

```
[SimpleServer] UDP受信 [user_1]: TransformData from 127.0.0.1:XXXX
[SimpleServer] UDPエンドポイント保存: user_1 -> 127.0.0.1:XXXX
[SimpleServer] UDPデータ転送成功: user_1 -> user_2 (127.0.0.1:XXXX)
[XRSharingClient] UDP TransformData受信: user_1 from 127.0.0.1:XXXX
[XRSharingClient] Transform同期完了: Cube (X, Y, Z) -> (X, Y, Z)
```

## 📝 ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 🤝 貢献

バグ報告や機能要望は、GitHubのIssuesでお知らせください。

## 📞 サポート

技術的な質問やサポートが必要な場合は、GitHubのDiscussionsをご利用ください。