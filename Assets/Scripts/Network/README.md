# Kotatsu Network Implementation

Unity向けネットワーク通信実装です。シンプルなUDPプロトコルを使用したリアルタイム通信と、HTTP APIを使用したマッチメイキングを提供します。

## 概要

- **プロトコル**: カスタムUDP（1バイトヘッダー + JSON）
- **Reliable通信**: パラメータ変更、join/join_ok等の重要なメッセージ
- **Unreliable通信**: 位置同期などの高頻度メッセージ

## ファイル構成

```
Assets/Scripts/Network/
├── MatchmakingClient.cs       # HTTP API client for matchmaking
├── NetworkMessages.cs          # Network message protocol definitions
├── SimpleUdpClient.cs          # Simple UDP client with header protocol
├── GameNetworkClient.cs        # Game network client (uses SimpleUdpClient)
├── NetworkManager.cs           # Main network manager (MonoBehaviour)
└── README.md                   # This file
```

## プロトコル仕様

### パケット構造

すべてのUDPパケットは以下の形式：

```
[1 byte: Type] [N bytes: JSON Payload]
```

- `Type = 0x01`: Reliable メッセージ
- `Type = 0x02`: Unreliable メッセージ

### メッセージ形式

#### Client → Server (Reliable)

**Join**:
```json
{"t":"join","token":"..."}
```

**Parameter Change**:
```json
{"t":"param_change","seq":1,"param":"gravity","direction":"increase"}
```

#### Client → Server (Unreliable)

**Position Update**:
```json
{"t":"pos","seq":42,"x":12.3,"y":4.5,"vx":0.1,"vy":-0.2}
```

**Stage Progress**:
```json
{"t":"stage_progress","current_stage_index":2}
```

#### Server → Client (Reliable)

**Join OK**:
```json
{
  "t":"join_ok",
  "match_id":"m_...",
  "player_id":"p_...",
  "params":{"gravity":2,"friction":2,"speed":2},
  "server_time_ms":1761000000123
}
```

**Parameter Applied**:
```json
{
  "t":"param_applied",
  "from_player_id":"p_...",
  "seq":1,
  "params":{"gravity":3,"friction":2,"speed":2},
  "next_param_change_at_unix":1761000030,
  "server_time_ms":1761000000456
}
```

**Error**:
```json
{"t":"error","code":"param_update_failed","message":"cooldown_active:1761000030"}
```

#### Server → Client (Unreliable)

**Position Broadcast**:
```json
{
  "t":"pos",
  "player_id":"p_other",
  "seq":42,
  "x":12.3,
  "y":4.5,
  "vx":0.1,
  "vy":-0.2,
  "server_time_ms":1761000000789
}
```

**Stage Progress Broadcast**:
```json
{
  "t":"stage_progress",
  "player_id":"p_other",
  "current_stage_index":2,
  "server_time_ms":1761000000901
}
```

## 使い方

### 基本的なセットアップ

1. **NetworkManagerをシーンに追加**

   空のGameObjectを作成し、`NetworkManager`コンポーネントをアタッチ。

2. **設定**

   - **Matchmaking Url**: `http://127.0.0.1:8080` (ローカル) または本番URL
   - **Display Name**: プレイヤー名

### コード例

```csharp
using Kotatsu.Network;

public class GameController : MonoBehaviour
{
    private NetworkManager networkManager;

    void Start()
    {
        networkManager = FindObjectOfType<NetworkManager>();

        // イベントを登録
        networkManager.OnGameConnected += OnConnected;
        networkManager.OnPlayerParamsChanged += OnParamsChanged;
        networkManager.OnPlayerPositionUpdated += OnPosUpdated;

        // マッチを作成して参加
        networkManager.CreateAndJoinMatch();
    }

    void OnConnected()
    {
        Debug.Log("Game server connected!");
    }

    void OnParamsChanged(string playerId, int g, int f, int s)
    {
        Debug.Log($"Player {playerId}: G={g}, F={f}, S={s}");
    }

    void OnPosUpdated(string playerId, float x, float y, float vx, float vy)
    {
        // 他のプレイヤーの位置を更新
    }

    void Update()
    {
        // 自分の位置を送信
        if (networkManager.IsConnected)
        {
            Vector2 pos = transform.position;
            Vector2 vel = GetComponent<Rigidbody2D>().velocity;
            networkManager.UpdatePosition(pos.x, pos.y, vel.x, vel.y);
        }
    }
}
```

## バックエンド対応

バックエンドはRust製のUDPサーバーです：

- **場所**: `backend/realtime-split/realtime-server`
- **起動方法**:
  ```bash
  cd backend/realtime-split/realtime-server
  cargo run
  ```

### 環境変数

- `UDP_BIND_ADDR`: UDPサーバーのバインドアドレス（デフォルト: `0.0.0.0:4433`）
- `UDP_PUBLIC_URL`: クライアントに返すURL（デフォルト: `udp://127.0.0.1:4433`）
- `GRPC_ADDR`: gRPC制御プレーン（デフォルト: `0.0.0.0:50051`）

## テスト方法

1. **バックエンドを起動**:
   ```bash
   cd backend/realtime-split
   cargo run --bin kotatsu-realtime-server-split
   ```

2. **API serverを起動**:
   ```bash
   cd backend/realtime-split/api-server
   cargo run
   ```

3. **Unityでテスト**:
   - `NetworkManager` をシーンに配置
   - Play モードでロビー作成または参加の導線を実行

## 注意事項

### セキュリティ

- 現在の実装には暗号化がありません
- 本番環境では TLS/DTLS の使用を検討してください
- トークンの有効期限は1時間です

### パフォーマンス

- 位置更新は毎フレーム送信可能（Unreliable）
- パラメータ変更には30秒のクールダウンがあります
- 最大4プレイヤー/ルーム

## トラブルシューティング

### 接続できない

1. バックエンドが起動しているか確認
2. ファイアウォールでUDPポート4433が開いているか確認
3. `NetworkManager` の URL設定を確認

### メッセージが届かない

- UnityのConsoleでエラーログを確認
- バックエンドのログを確認（`RUST_LOG=debug cargo run`）

## 今後の改善案

- [ ] 再接続ロジックの実装
- [ ] レイテンシ計測機能
- [ ] 暗号化対応
- [ ] より堅牢なエラーハンドリング
- [ ] クライアント側の予測/補間
