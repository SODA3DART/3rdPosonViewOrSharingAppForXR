using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;
using XRSharing;
using System.Collections.Generic;

/// <summary>
/// XRシェアリング用クライアント（パッケージ版）
/// </summary>
public class XRSharingClient : MonoBehaviour
{
    [Header("Connection Settings")]
    [Tooltip("Server URL to connect to (supports HTTPS format like ngrok URLs)")]
    public string serverURL = "https://localhost:7777";
    
    [Tooltip("Automatically connect to server when the scene begins")]
    public bool autoConnect = false;
    
    [Header("Connection Status")]
    [Tooltip("Current connection state to server")]
    public bool isConnected = false;
    
    [Tooltip("Current session ID (received from server)")]
    public string sessionId = "";
    
    [Tooltip("Current user ID (auto-assigned by server)")]
    public string userId = "";
    
    [Header("Transform Settings")]
    [Tooltip("Transform objects to send via UDP")]
    public Transform[] transformTargets = new Transform[0];
    
    [Tooltip("Transform objects to sync with received data")]
    public Transform[] syncTargets = new Transform[0];
    
    [Tooltip("Enable automatic transform synchronization")]
    public bool enableAutoSync = true;
    
    [Tooltip("Smoothing factor for transform interpolation (0-1)")]
    [Range(0f, 1f)]
    public float smoothingFactor = 0.1f;
    
    [Tooltip("Allow receiving own transform data (for testing)")]
    public bool allowOwnData = true;
    
    [Tooltip("Transform send interval in seconds (0 = every frame)")]
    [Range(0f, 1f)]
    public float transformSendInterval = 0.1f; // 10fps
    
    [Header("Debug Settings")]
    [Tooltip("Enable debug log output")]
    public bool enableDebugLogs = true;
    
    // 接続関連
    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool running = false;
    
    // UDPクライアント
    private UdpClient udpClient;
    private bool udpConnected = false;
    
    // Transform同期用
    private Dictionary<string, TransformData> receivedTransforms = new Dictionary<string, TransformData>();
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> targetRotations = new Dictionary<string, Quaternion>();
    
    // Transform送信用
    private float lastTransformSendTime = 0f;
    
    // イベント
    public System.Action<string, string> OnMessageReceived; // (message, fromUserId)
    public System.Action<string, TransformData> OnTransformReceived; // (userId, transformData)
    public System.Action OnConnected;
    public System.Action OnDisconnected;
    public System.Action<string> OnError;
    
    // イベントシステム用
    public System.Action<string, string, string> OnEventReceived; // (eventType, eventData, fromUserId)
    public UnityEngine.Events.UnityEvent<string, string> OnCustomEvent; // (eventType, eventData) - Inspectorで設定可能
    
    // PositionSyncSample用のイベント
    public System.Action<string, string, string> OnPositionSyncEvent; // (eventType, eventData, fromUserId)
    
    void Start()
    {
        if (autoConnect)
        {
            ConnectToServer();
        }
    }
    
    /// <summary>
    /// ユーザーIDからユーザーインデックスを取得（簡易実装）
    /// </summary>
    private int GetUserIndex(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return 0;
        
        // user_1 -> 0, user_2 -> 1, user_3 -> 2 の形式を想定
        if (userId.StartsWith("user_"))
        {
            if (int.TryParse(userId.Substring(5), out int userNum))
            {
                return userNum - 1; // user_1 -> 0, user_2 -> 1
            }
        }
        
        // デフォルトは0
        return 0;
    }
    
    void Update()
    {
        if (isConnected)
        {
            // 自動同期処理
            if (enableAutoSync)
            {
                SyncTransforms();
            }
            
            // 継続的なTransform送信（サーバーにUDPエンドポイントを学習させるため）
            if (transformTargets != null && transformTargets.Length > 0)
            {
                float currentTime = Time.time;
                if (transformSendInterval <= 0f || currentTime - lastTransformSendTime >= transformSendInterval)
                {
                    SendAllTransformData();
                    lastTransformSendTime = currentTime;
                }
            }
        }
    }
    
    void OnGUI()
    {
        // 右画面に配置（画面幅の半分から開始）
        float screenWidth = Screen.width;
        float rightPanelX = screenWidth / 2 + 10;
        float rightPanelY = 10;
        float rightPanelWidth = screenWidth / 2 - 20;
        float rightPanelHeight = 400;
        
        GUILayout.BeginArea(new Rect(rightPanelX, rightPanelY, rightPanelWidth, rightPanelHeight));
        
        GUILayout.Label("=== XRSharingClient ===", GUI.skin.box);
        
        if (GUILayout.Button(isConnected ? "切断" : "接続"))
        {
            if (isConnected)
            {
                DisconnectFromServer();
            }
            else
            {
                ConnectToServer();
            }
        }
        
        GUILayout.Space(10);
        GUILayout.Label("TCP状態: " + (isConnected ? "接続中" : "未接続"));
        GUILayout.Label("UDP状態: " + (udpConnected ? "接続中" : "未接続"));
        GUILayout.Label("セッションID: " + sessionId);
        GUILayout.Label("UserID: " + userId);
        
        GUILayout.Space(10);
        if (isConnected)
        {
            GUILayout.Label("=== TCPメッセージ送信テスト ===");
            if (GUILayout.Button("Hello World"))
            {
                SendMessage("Hello World from " + userId);
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== イベント送信テスト ===");
            if (GUILayout.Button("テストイベント送信"))
            {
                SendEvent("TEST_EVENT", "{\"message\":\"Hello from client!\"}");
            }
            
            if (GUILayout.Button("ボタンクリックイベント送信"))
            {
                SendEvent("BUTTON_CLICK", "{\"buttonName\":\"ClientButton\",\"position\":\"right\"}");
            }
            
            if (GUILayout.Button("オブジェクト選択イベント送信"))
            {
                SendEvent("OBJECT_SELECTED", "{\"objectName\":\"ClientObject\",\"objectId\":456}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== UDP Transform送信テスト ===");
            if (GUILayout.Button("Transform送信"))
            {
                // 設定されたTransform配列から送信
                SendAllTransformData();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("=== Transform同期設定 ===");
            enableAutoSync = GUILayout.Toggle(enableAutoSync, "自動同期");
            allowOwnData = GUILayout.Toggle(allowOwnData, "自分のデータも受信（テスト用）");
            smoothingFactor = GUILayout.HorizontalSlider(smoothingFactor, 0f, 1f);
            GUILayout.Label("スムージング: " + smoothingFactor.ToString("F2"));
            GUILayout.Label("受信Transform数: " + receivedTransforms.Count);
        }
        
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// サーバーに接続
    /// </summary>
    public void ConnectToServer()
    {
        if (isConnected) return;
        
        try
        {
            // URLからホストとポートを解析
            var (host, port) = ParseServerURL(serverURL);
            LogDebug("接続試行: " + host + ":" + port);
            
            // TCP接続
            client = new TcpClient();
            client.Connect(host, port);
            stream = client.GetStream();
            
            // UDP接続（TCPポート+1を使用）
            udpClient = new UdpClient();
            udpClient.Connect(host, port + 1);
            
            isConnected = true;
            udpConnected = true;
            running = true;
            
            LogDebug("TCPサーバーに接続成功: " + host + ":" + port);
            LogDebug("UDPサーバーに接続成功: " + host + ":" + (port + 1));
            
            // 受信スレッド開始
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
            
            // UDP受信スレッド開始
            Thread udpReceiveThread = new Thread(ReceiveUdpMessages);
            udpReceiveThread.Start();
            
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            LogError("接続エラー: " + e.Message);
            LogError("接続先: " + serverURL);
            // メインスレッドでエラーイベントを発火
            if (UnityMainThreadDispatcher.Exists())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnError?.Invoke("接続エラー: " + e.Message);
                });
            }
            else
            {
                OnError?.Invoke("接続エラー: " + e.Message);
            }
        }
    }
    
    /// <summary>
    /// サーバーから切断
    /// </summary>
    public void DisconnectFromServer()
    {
        running = false;
        isConnected = false;
        udpConnected = false;
        
        if (stream != null)
        {
            stream.Close();
            stream = null;
        }
        
        if (client != null)
        {
            client.Close();
            client = null;
        }
        
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        
        sessionId = "";
        userId = "";
        receivedTransforms.Clear();
        targetPositions.Clear();
        targetRotations.Clear();
        
        LogDebug("TCP/UDPサーバーから切断");
        OnDisconnected?.Invoke();
    }
    
    /// <summary>
    /// メッセージを送信
    /// </summary>
    public void SendMessage(string message)
    {
        if (!isConnected || stream == null)
        {
            LogError("サーバーに接続されていません");
            return;
        }
        
        try
        {
            // userIdとsessionIdが空の場合はデフォルト値を設定
            string currentUserId = string.IsNullOrEmpty(userId) ? "unknown_user" : userId;
            string currentSessionId = string.IsNullOrEmpty(sessionId) ? "unknown_session" : sessionId;
            
            var request = new XRSharing.ServerRequest
            {
                message = message,
                userId = currentUserId,
                sessionId = currentSessionId
            };
            
            LogDebug($"送信データ: userId={currentUserId}, sessionId={currentSessionId}, message={message}");
            
            // サンプルと同じ方式でシリアライズ
            var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new ServerRequestFormatter() },
                new IFormatterResolver[] { ServerRequestResolver.Instance }
            ));
            byte[] data = MessagePackSerializer.Serialize(request, options);
            stream.Write(data, 0, data.Length);
            
            LogDebug("メッセージ送信完了: " + message);
        }
        catch (Exception e)
        {
            LogError("送信エラー: " + e.Message);
            LogError("Exception details: " + e.ToString());
            // メインスレッドでエラーイベントを発火
            if (UnityMainThreadDispatcher.Exists())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnError?.Invoke("送信エラー: " + e.Message);
                });
            }
            else
            {
                OnError?.Invoke("送信エラー: " + e.Message);
            }
        }
    }
    
    /// <summary>
    /// イベントを送信
    /// </summary>
    public void SendEvent(string eventType, string eventData, string targetSessionId = "", string targetUserId = "")
    {
        if (!isConnected || stream == null)
        {
            LogError("サーバーに接続されていません");
            return;
        }
        
        try
        {
            string currentUserId = string.IsNullOrEmpty(userId) ? "unknown_user" : userId;
            string currentSessionId = string.IsNullOrEmpty(sessionId) ? "unknown_session" : sessionId;
            
            var eventMessage = new XRSharing.EventData
            {
                header = "EVNT",
                eventType = eventType,
                eventData = eventData,
                fromUserId = currentUserId,
                targetSessionId = targetSessionId,
                targetUserId = targetUserId,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                sessionId = currentSessionId
            };
            
            LogDebug($"イベント送信: {eventType} -> セッション:{targetSessionId}, ユーザー:{targetUserId}");
            
            // サンプルと同じ方式でシリアライズ
            var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new EventDataFormatter() },
                new IFormatterResolver[] { EventDataResolver.Instance }
            ));
            byte[] data = MessagePackSerializer.Serialize(eventMessage, options);
            stream.Write(data, 0, data.Length);
            
            LogDebug("イベント送信完了: " + eventType);
        }
        catch (Exception e)
        {
            LogError("イベント送信エラー: " + e.Message);
            // メインスレッドでエラーイベントを発火
            if (UnityMainThreadDispatcher.Exists())
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    OnError?.Invoke("イベント送信エラー: " + e.Message);
                });
            }
            else
            {
                OnError?.Invoke("イベント送信エラー: " + e.Message);
            }
        }
    }
    
    void ReceiveMessages()
    {
        byte[] buffer = new byte[4096];
        
        while (running && isConnected)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    byte[] actualData = new byte[bytesRead];
                    Array.Copy(buffer, 0, actualData, 0, bytesRead);
                    
                    // データタイプを判定して適切にデシリアライズ
                    try
                    {
                        // まずServerResponseとして試行
                        var reader = new MessagePackReader(actualData);
                        var deserializeOptions = MessagePackSerializerOptions.Standard;
                        var formatter = new ServerResponseFormatter();
                        var response = formatter.Deserialize(ref reader, deserializeOptions);
                        
                        // セッションIDとUserIDを設定（初回受信時）
                        if (string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(response.sessionId))
                        {
                            sessionId = response.sessionId;
                            LogDebug("セッションID設定: " + sessionId);
                        }
                        if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(response.fromUserId))
                        {
                            userId = response.fromUserId;
                            LogDebug("UserID設定: " + userId);
                        }
                        
                        LogDebug("受信: " + response.message + " (from: " + response.fromUserId + ")");
                        
                        // メインスレッドでメッセージ受信イベントを発火
                        if (UnityMainThreadDispatcher.Exists())
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                                OnMessageReceived?.Invoke(response.message, response.fromUserId);
                            });
                        }
                        else
                        {
                            // フォールバック: 直接実行
                            OnMessageReceived?.Invoke(response.message, response.fromUserId);
                        }
                    }
                    catch (Exception responseError)
                    {
                        // ServerResponseで失敗した場合、EventDataとして試行
                        try
                        {
                            var reader = new MessagePackReader(actualData);
                            var deserializeOptions = MessagePackSerializerOptions.Standard;
                            var eventFormatter = new EventDataFormatter();
                            var eventData = eventFormatter.Deserialize(ref reader, deserializeOptions);
                            
                            LogDebug($"イベント受信: {eventData.eventType} (from: {eventData.fromUserId})");
                            
                            // メインスレッドでイベントを発火
                            if (UnityMainThreadDispatcher.Exists())
                            {
                                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                                    // イベント受信イベント
                                    OnEventReceived?.Invoke(eventData.eventType, eventData.eventData, eventData.fromUserId);
                                    
                                    // UnityEventも発火
                                    OnCustomEvent?.Invoke(eventData.eventType, eventData.eventData);
                                    
                                    // PositionSyncSample用のイベント
                                    if (eventData.eventType == "POSITION_SYNC" || 
                                        eventData.eventType == "MARKER_PLACED" || 
                                        eventData.eventType == "VPS_ANCHOR" || 
                                        eventData.eventType == "SYNC_COMPLETE")
                                    {
                                        OnPositionSyncEvent?.Invoke(eventData.eventType, eventData.eventData, eventData.fromUserId);
                                    }
                                });
                            }
                            else
                            {
                                // フォールバック: 直接実行
                                OnEventReceived?.Invoke(eventData.eventType, eventData.eventData, eventData.fromUserId);
                                OnCustomEvent?.Invoke(eventData.eventType, eventData.eventData);
                                
                                // PositionSyncSample用のイベント
                                if (eventData.eventType == "POSITION_SYNC" || 
                                    eventData.eventType == "MARKER_PLACED" || 
                                    eventData.eventType == "VPS_ANCHOR" || 
                                    eventData.eventType == "SYNC_COMPLETE")
                                {
                                    OnPositionSyncEvent?.Invoke(eventData.eventType, eventData.eventData, eventData.fromUserId);
                                }
                            }
                        }
                        catch (Exception eventError)
                        {
                            LogError("データデシリアライズエラー: " + eventError.Message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (running)
                {
                    LogError("受信エラー: " + e.Message);
                    // メインスレッドでエラーイベントを発火
                    if (UnityMainThreadDispatcher.Exists())
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() => {
                            OnError?.Invoke("受信エラー: " + e.Message);
                        });
                    }
                    else
                    {
                        OnError?.Invoke("受信エラー: " + e.Message);
                    }
                }
                break;
            }
        }
        
        // 接続が切れた場合
        if (running)
        {
            DisconnectFromServer();
        }
    }
    
    (string host, int port) ParseServerURL(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            string host = uri.Host;
            int port = uri.Port;
            
            LogDebug("URL解析結果: Host=" + host + ", Port=" + port);
            
            if (port == -1)
            {
                port = 7777; // デフォルトポート
                LogDebug("デフォルトポート使用: " + port);
            }
            
            return (host, port);
        }
        catch (Exception e)
        {
            LogError("URL解析エラー: " + e.Message);
            LogError("URL: " + url);
            return ("localhost", 7777);
        }
    }
    
    /// <summary>
    /// 設定されたTransform配列を全て送信
    /// </summary>
    public void SendAllTransformData()
    {
        LogDebug("SendAllTransformData開始");
        LogDebug($"UDP接続状態: {udpConnected}, udpClient: {udpClient != null}");
        
        if (!udpConnected || udpClient == null)
        {
            LogError("UDP接続されていません");
            return;
        }
        
        if (transformTargets == null || transformTargets.Length == 0)
        {
            LogError("Transform配列が設定されていません");
            return;
        }
        
        LogDebug($"Transform配列数: {transformTargets.Length}");
        
        int sentCount = 0;
        for (int i = 0; i < transformTargets.Length; i++)
        {
            if (transformTargets[i] != null)
            {
                LogDebug($"Transform[{i}]送信: {transformTargets[i].name}");
                SendTransformData(transformTargets[i], i);
                sentCount++;
            }
            else
            {
                LogDebug($"Transform[{i}]はnull");
            }
        }
        
        LogDebug($"UDP TransformData送信完了: {sentCount}個のTransformを送信");
    }
    
    /// <summary>
    /// UDPでTransformDataを送信
    /// </summary>
    public void SendTransformData(UnityEngine.Transform transform, int index = 0)
    {
        if (!udpConnected || udpClient == null)
        {
            LogError("UDP接続されていません");
            return;
        }
        
        try
        {
            // TransformDataを作成
            var transformData = new XRSharing.TransformData
            {
                header = "TRNS",
                userId = userId ?? "unknown_user",
                sessionId = sessionId ?? "unknown_session",
                position = transform.position,
                rotation = transform.rotation,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userIndex = GetUserIndex(userId), // 新フィールド
                objectIndex = index // 新フィールド
            };
            
            // サンプルと同じ方式でシリアライズ
            var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new XRSharing.TransformDataFormatter() },
                new IFormatterResolver[] { XRSharing.TransformDataResolver.Instance }
            ));
            
            byte[] transformDataBytes = MessagePackSerializer.Serialize(transformData, options);
            
            // ヘッダーを追加
            byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes("TRNS");
            byte[] data = new byte[headerBytes.Length + transformDataBytes.Length];
            Array.Copy(headerBytes, 0, data, 0, headerBytes.Length);
            Array.Copy(transformDataBytes, 0, data, headerBytes.Length, transformDataBytes.Length);
            
            udpClient.Send(data, data.Length);
            
            LogDebug($"UDP TransformData[{index}]送信完了: {transformData.userId} - {transform.name}");
        }
        catch (Exception e)
        {
            LogError($"UDP TransformData[{index}]送信エラー: " + e.Message);
        }
    }
    
    /// <summary>
    /// UDP受信処理
    /// </summary>
    void ReceiveUdpMessages()
    {
        LogDebug("UDP受信スレッド開始");
        while (running && udpConnected)
        {
            try
            {
                LogDebug("UDP受信待機中...");
                // UDPデータを受信
                var remoteEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
                byte[] receivedBytes = udpClient.Receive(ref remoteEndPoint);
                LogDebug($"UDPデータ受信: {receivedBytes.Length} bytes from {remoteEndPoint}");
                
                // 受信データの詳細ログ
                LogDebug($"受信データ詳細:");
                LogDebug($"  バイト数: {receivedBytes.Length}");
                LogDebug($"  送信元: {remoteEndPoint}");
                LogDebug($"  データ内容: {BitConverter.ToString(receivedBytes)}");
                
                // ヘッダーを解析（最初の4バイト）
                if (receivedBytes.Length >= 4)
                {
                    string header = System.Text.Encoding.ASCII.GetString(receivedBytes, 0, 4);
                    LogDebug($"UDPヘッダー: '{header}'");
                    
                    if (header == "TRNS")
                    {
                        LogDebug($"TRNSヘッダー検出！データ処理開始");
                        
                        // ヘッダーを除いた部分を取得
                        byte[] transformDataBytes = new byte[receivedBytes.Length - 4];
                        Array.Copy(receivedBytes, 4, transformDataBytes, 0, transformDataBytes.Length);
                        
                        LogDebug($"TransformDataバイト数: {transformDataBytes.Length}");
                        LogDebug($"TransformData内容: {BitConverter.ToString(transformDataBytes)}");
                        
                        // TransformDataをデシリアライズ
                        var reader = new MessagePackReader(transformDataBytes);
                        var options = MessagePackSerializerOptions.Standard;
                        var formatter = new TransformDataFormatter();
                        var transformData = formatter.Deserialize(ref reader, options);
                        
                        LogDebug($"デシリアライズ成功: userId={transformData.userId}, sessionId={transformData.sessionId}");
                        
                        // 自分のデータは無視（テスト用オプションで制御）
                        if (transformData.userId == userId && !allowOwnData)
                        {
                            LogDebug($"自分のデータのため無視: {transformData.userId} == {userId}");
                            continue;
                        }
                        
                        LogDebug($"UDP TransformData受信: {transformData.userId} from {remoteEndPoint}");
                        LogDebug($"  Position: {transformData.position}");
                        LogDebug($"  Rotation: {transformData.rotation}");
                        LogDebug($"  Timestamp: {transformData.timestamp}");
                        
                        // TransformDataを保存
                        receivedTransforms[transformData.userId] = transformData;
                        LogDebug($"TransformData保存完了: {receivedTransforms.Count}個のTransformを保持");
                        
                        // スムージング用の目標値を設定
                        targetPositions[transformData.userId] = transformData.position;
                        targetRotations[transformData.userId] = transformData.rotation;
                        LogDebug($"目標値設定完了: Position={targetPositions[transformData.userId]}, Rotation={targetRotations[transformData.userId]}");
                        
                        // メインスレッドでTransform受信イベントを発火
                        if (UnityMainThreadDispatcher.Exists())
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                                OnTransformReceived?.Invoke(transformData.userId, transformData);
                                LogDebug($"OnTransformReceivedイベント発火完了");
                            });
                        }
                        else
                        {
                            // フォールバック: 直接実行
                            OnTransformReceived?.Invoke(transformData.userId, transformData);
                            LogDebug($"OnTransformReceivedイベント発火完了");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (running)
                {
                    LogError("UDP受信エラー: " + e.Message);
                    LogError("UDP受信エラー詳細: " + e.StackTrace);
                    // メインスレッドでエラーイベントを発火
                    if (UnityMainThreadDispatcher.Exists())
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() => {
                            OnError?.Invoke("UDP受信エラー: " + e.Message);
                        });
                    }
                    else
                    {
                        OnError?.Invoke("UDP受信エラー: " + e.Message);
                    }
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// Transform同期処理（既存ロジック + objectIndexベースの新ロジック）
    /// </summary>
    void SyncTransforms()
    {
        if (syncTargets == null || syncTargets.Length == 0)
        {
            LogDebug("SyncTargetsが設定されていません");
            return;
        }
        
        if (targetPositions.Count == 0)
        {
            LogDebug("同期対象のTransformがありません");
            return;
        }
        
        LogDebug($"同期処理開始: {targetPositions.Count}個のTransformを同期");
        
        // 新ロジック: objectIndexベースの同期（優先）
        bool usedNewLogic = false;
        foreach (var kvp in receivedTransforms)
        {
            string userId = kvp.Key;
            var transformData = kvp.Value;
            
            // objectIndexが有効な場合、新ロジックを使用
            if (transformData.objectIndex >= 0 && transformData.objectIndex < syncTargets.Length)
            {
                if (syncTargets[transformData.objectIndex] != null)
                {
                    Transform targetTransform = syncTargets[transformData.objectIndex];
                    Vector3 oldPos = targetTransform.position;
                    Quaternion oldRot = targetTransform.rotation;
                    
                    // スムージング適用
                    if (smoothingFactor > 0)
                    {
                        targetTransform.position = Vector3.Lerp(targetTransform.position, transformData.position, smoothingFactor);
                        targetTransform.rotation = Quaternion.Lerp(targetTransform.rotation, transformData.rotation, smoothingFactor);
                    }
                    else
                    {
                        targetTransform.position = transformData.position;
                        targetTransform.rotation = transformData.rotation;
                    }
                    
                    LogDebug($"新ロジック同期完了: {targetTransform.name} (objectIndex={transformData.objectIndex}) {oldPos} -> {targetTransform.position}");
                    usedNewLogic = true;
                }
            }
        }
        
        // 既存ロジック: userIdベースの同期（フォールバック）
        if (!usedNewLogic)
        {
            LogDebug("新ロジックが使用できないため、既存ロジックを使用");
            int targetIndex = 0;
            foreach (var kvp in targetPositions)
            {
                string userId = kvp.Key;
                Vector3 targetPos = kvp.Value;
                Quaternion targetRot = targetRotations.ContainsKey(userId) ? targetRotations[userId] : Quaternion.identity;
                
                LogDebug($"既存ロジック同期: userId={userId}, targetIndex={targetIndex}, targetPos={targetPos}");
                
                if (targetIndex < syncTargets.Length && syncTargets[targetIndex] != null)
                {
                    Transform targetTransform = syncTargets[targetIndex];
                    Vector3 oldPos = targetTransform.position;
                    Quaternion oldRot = targetTransform.rotation;
                    
                    // スムージング適用
                    if (smoothingFactor > 0)
                    {
                        targetTransform.position = Vector3.Lerp(targetTransform.position, targetPos, smoothingFactor);
                        targetTransform.rotation = Quaternion.Lerp(targetTransform.rotation, targetRot, smoothingFactor);
                    }
                    else
                    {
                        targetTransform.position = targetPos;
                        targetTransform.rotation = targetRot;
                    }
                    
                    LogDebug($"既存ロジック同期完了: {targetTransform.name} {oldPos} -> {targetTransform.position}");
                    targetIndex++;
                }
                else
                {
                    LogDebug($"同期対象がありません: targetIndex={targetIndex}, syncTargets.Length={syncTargets.Length}");
                }
            }
        }
    }
    
    void OnDestroy()
    {
        DisconnectFromServer();
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
//            Debug.Log("[XRSharingClient] " + message);
        }
    }
    
    void LogError(string message)
    {
        // メインスレッドで実行するようにキューに追加
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.LogError("[XRSharingClient] " + message);
            });
        }
        else
        {
            // フォールバック: コンソールに直接出力
            System.Console.WriteLine("[XRSharingClient] " + message);
        }
    }
}

/// <summary>
/// メインスレッドで処理を実行するためのディスパッチャー
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance = null;
    private static readonly System.Collections.Generic.Queue<System.Action> _executionQueue = new System.Collections.Generic.Queue<System.Action>();
    
    public static UnityMainThreadDispatcher Instance()
    {
        if (!Exists())
        {
            GameObject go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }
    
    public static bool Exists()
    {
        return _instance != null;
    }
    
    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
    
    public void Enqueue(System.Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
