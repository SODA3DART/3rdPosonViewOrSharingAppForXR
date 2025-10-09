using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;
using XRSharing;

/// <summary>
/// MessagePackを使用したシンプルなUnityサーバー
/// </summary>
public class SimpleServer : MonoBehaviour
{
    [Header("Server Configuration")]
    [Tooltip("Server URL for listening (supports HTTPS format like ngrok URLs)")]
    public string serverURL = "https://localhost:7777";
    
    [Tooltip("Enable ngrok compatibility mode")]
    public bool useNgrok = false;
    
    [Tooltip("Automatically start server when the scene begins")]
    public bool autoStart = false;
    
    [Tooltip("Auto-generate session ID (if false, use custom session ID below)")]
    public bool autoGenerateSessionId = true;
    
    [Tooltip("Custom session ID (only used when auto-generate is disabled)")]
    public string customSessionId = "custom_session_123";
    
    [Header("Server Status")]
    [Tooltip("Current server running state")]
    public bool isRunning = false;
    
    [Tooltip("Number of connected clients")]
    public int connectedClients = 0;
    
    [Tooltip("Current session ID (auto-generated)")]
    public string currentSessionId = "";
    
    private TcpListener server;
    private Thread serverThread;
    private bool running = false;
    
    // UDPサーバー
    private UdpClient udpServer;
    private Thread udpServerThread;
    private bool udpRunning = false;
    
    // クライアント管理
    private Dictionary<string, TcpClient> connectedClientsDict = new Dictionary<string, TcpClient>();
    private Dictionary<string, NetworkStream> clientStreams = new Dictionary<string, NetworkStream>();
    private Dictionary<string, IPEndPoint> clientUdpEndpoints = new Dictionary<string, IPEndPoint>();
    private int nextUserId = 1;
    private readonly object udpEndpointsLock = new object();

    //受信ログを出すか出さないか
    [Header("Debug")] private bool _ShowLog = false;
    void Start()
    {
        if (autoStart)
        {
            StartServer();
        }
    }
    
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        
        GUILayout.Label("Simple Server", GUI.skin.box);
        
        if (GUILayout.Button(isRunning ? "サーバー停止" : "サーバー開始"))
        {
            if (isRunning)
            {
                StopServer();
            }
            else
            {
                StartServer();
            }
        }
        
        GUILayout.Label("状態: " + (isRunning ? "実行中" : "停止中"));
        GUILayout.Label("URL: " + serverURL);
        GUILayout.Label("セッションID: " + currentSessionId);
        GUILayout.Label("セッション生成: " + (autoGenerateSessionId ? "自動" : "固定"));
        GUILayout.Label("ngrok使用: " + (useNgrok ? "はい" : "いいえ"));
        GUILayout.Label("接続クライアント数: " + connectedClients);
        
        GUILayout.Space(10);
        GUILayout.Label("=== イベント送信 ===", GUI.skin.box);
        
        if (isRunning)
        {
            if (GUILayout.Button("テストイベント送信（全クライアント）"))
            {
                SendEventToClients("TEST_EVENT", "{\"message\":\"Hello from server!\"}");
            }
            
            if (GUILayout.Button("ボタンクリックイベント送信"))
            {
                SendEventToClients("BUTTON_CLICK", "{\"buttonName\":\"TestButton\",\"position\":\"center\"}");
            }
            
            if (GUILayout.Button("オブジェクト選択イベント送信"))
            {
                SendEventToClients("OBJECT_SELECTED", "{\"objectName\":\"Cube\",\"objectId\":123}");
            }
        }
        
        GUILayout.EndArea();
    }
    
    void StartServer()
    {
        if (isRunning) return;
        
        try
        {
            // URLからIPアドレスとポートを解析
            var (ipAddress, port) = ParseServerURL(serverURL);
            
            // TCPサーバー開始
            server = new TcpListener(ipAddress, port);
            server.Start();
            
            // UDPサーバー開始（TCPポート+1を使用）
            udpServer = new UdpClient(port + 1);
            
            isRunning = true;
            running = true;
            udpRunning = true;
            
            // セッションIDを設定（自動生成または固定）
            currentSessionId = autoGenerateSessionId ? GenerateSessionId() : customSessionId;
            
            Debug.Log("TCPサーバー開始: " + serverURL + " (" + ipAddress + ":" + port + ")");
            Debug.Log("UDPサーバー開始: " + ipAddress + ":" + (port + 1));
            Debug.Log("セッションID: " + currentSessionId);
            
            // TCPサーバースレッド開始
            serverThread = new Thread(ServerLoop);
            serverThread.Start();
            
            // UDPサーバースレッド開始
            udpServerThread = new Thread(UdpServerLoop);
            udpServerThread.Start();
            Debug.Log("UDPサーバースレッド開始完了");
        }
        catch (Exception e)
        {
            Debug.LogError("サーバー開始エラー: " + e.Message);
        }
    }
    
    void StopServer()
    {
        running = false;
        udpRunning = false;
        isRunning = false;
        connectedClients = 0;
        currentSessionId = "";
        
        // 全クライアントを切断
        foreach (var client in connectedClientsDict.Values)
        {
            client.Close();
        }
        connectedClientsDict.Clear();
        clientStreams.Clear();
        
        // UDPエンドポイントもクリア
        lock (udpEndpointsLock)
        {
            clientUdpEndpoints.Clear();
        }
        
        // TCPサーバー停止
        if (server != null)
        {
            server.Stop();
            server = null;
        }
        
        // UDPサーバー停止
        if (udpServer != null)
        {
            udpServer.Close();
            udpServer = null;
        }
        
        Debug.Log("TCP/UDPサーバー停止");
    }
    
    string GenerateSessionId()
    {
        return "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(1000, 9999);
    }
    
    void ForwardMessageToOtherClients(XRSharing.ServerRequest request, string fromUserId)
    {
        // 他のクライアントにメッセージを転送
        foreach (var kvp in clientStreams)
        {
            string targetUserId = kvp.Key;
            NetworkStream targetStream = kvp.Value;
            
            if (targetUserId != fromUserId) // 送信者以外に転送
            {
                try
                {
                    var forwardMessage = new XRSharing.ServerResponse
                    {
                        message = "[" + fromUserId + "]: " + request.message,
                        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        success = true,
                        sessionId = currentSessionId ?? "default_session",
                        fromUserId = fromUserId ?? "default_user"
                    };
                    
                    try
                    {
                        // サンプルと同じ方式でシリアライズ
                        var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                            new IMessagePackFormatter[] { new ServerResponseFormatter() },
                            new IFormatterResolver[] { ServerResponseResolver.Instance }
                        ));
                        byte[] forwardData = MessagePackSerializer.Serialize(forwardMessage, options);
                        targetStream.Write(forwardData, 0, forwardData.Length);
                        Debug.Log("転送成功 [" + fromUserId + " → " + targetUserId + "]: " + request.message);
                    }
                    catch (Exception serializeError)
                    {
                        Debug.LogError("転送シリアライゼーションエラー [" + targetUserId + "]: " + serializeError.Message);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("メッセージ転送エラー [" + targetUserId + "]: " + e.Message);
                }
            }
        }
    }
    
    /// <summary>
    /// イベントを他のクライアントに転送
    /// </summary>
    void ForwardEventToOtherClients(XRSharing.EventData eventData, string fromUserId)
    {
        // 他のクライアントにイベントを転送
        foreach (var kvp in clientStreams)
        {
            string targetUserId = kvp.Key;
            NetworkStream targetStream = kvp.Value;
            
            if (targetUserId != fromUserId) // 送信者以外に転送
            {
                try
                {
                    // セッションIDフィルタリング
                    if (!string.IsNullOrEmpty(eventData.targetSessionId) && currentSessionId != eventData.targetSessionId)
                    {
                        continue;
                    }
                    
                    // ユーザーIDフィルタリング
                    if (!string.IsNullOrEmpty(eventData.targetUserId) && targetUserId != eventData.targetUserId)
                    {
                        continue;
                    }
                    
                    var forwardEvent = new XRSharing.EventData
                    {
                        header = eventData.header,
                        eventType = eventData.eventType,
                        eventData = eventData.eventData,
                        fromUserId = fromUserId,
                        targetSessionId = eventData.targetSessionId,
                        targetUserId = eventData.targetUserId,
                        timestamp = eventData.timestamp,
                        sessionId = currentSessionId
                    };
                    
                    try
                    {
                        var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                            new IMessagePackFormatter[] { new EventDataFormatter() },
                            new IFormatterResolver[] { EventDataResolver.Instance }
                        ));
                        byte[] forwardData = MessagePackSerializer.Serialize(forwardEvent, options);
                        targetStream.Write(forwardData, 0, forwardData.Length);
                        Debug.Log($"イベント転送成功 [{fromUserId} → {targetUserId}]: {eventData.eventType}");
                    }
                    catch (Exception serializeError)
                    {
                        Debug.LogError($"イベント転送シリアライゼーションエラー [{targetUserId}]: {serializeError.Message}");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"イベント転送エラー [{targetUserId}]: {e.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// イベントを特定のクライアントに送信
    /// </summary>
    public void SendEventToClients(string eventType, string eventData, string targetSessionId = "", string targetUserId = "")
    {
        if (!isRunning)
        {
            Debug.LogError("サーバーが起動していません");
            return;
        }
        
        var eventMessage = new XRSharing.EventData
        {
            header = "EVNT",
            eventType = eventType,
            eventData = eventData,
            fromUserId = "server",
            targetSessionId = targetSessionId,
            targetUserId = targetUserId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            sessionId = currentSessionId
        };
        
        Debug.Log($"イベント送信: {eventType} -> セッション:{targetSessionId}, ユーザー:{targetUserId}");
        
        // 対象クライアントに送信
        foreach (var kvp in clientStreams)
        {
            string clientUserId = kvp.Key;
            NetworkStream clientStream = kvp.Value;
            
            // セッションIDフィルタリング
            if (!string.IsNullOrEmpty(targetSessionId) && currentSessionId != targetSessionId)
            {
                continue;
            }
            
            // ユーザーIDフィルタリング
            if (!string.IsNullOrEmpty(targetUserId) && clientUserId != targetUserId)
            {
                continue;
            }
            
            try
            {
                var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                    new IMessagePackFormatter[] { new EventDataFormatter() },
                    new IFormatterResolver[] { EventDataResolver.Instance }
                ));
                byte[] eventDataBytes = MessagePackSerializer.Serialize(eventMessage, options);
                clientStream.Write(eventDataBytes, 0, eventDataBytes.Length);
                Debug.Log($"イベント送信成功 [{clientUserId}]: {eventType}");
            }
            catch (Exception e)
            {
                Debug.LogError($"イベント送信エラー [{clientUserId}]: {e.Message}");
            }
        }
    }
    
    void ServerLoop()
    {
        while (running)
        {
            try
            {
                // クライアント接続を待機
                TcpClient client = server.AcceptTcpClient();
                string userId = "user_" + nextUserId++;
                Debug.Log("クライアント接続: " + client.Client.RemoteEndPoint + " (UserID: " + userId + ")");
                
                connectedClients++;
                
                // クライアント情報を保存
                connectedClientsDict[userId] = client;
                clientStreams[userId] = client.GetStream();
                
                // クライアントに接続情報を送信
                var connectionResponse = new XRSharing.ServerResponse
                {
                    message = "接続成功",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    success = true,
                    sessionId = currentSessionId ?? "default_session",
                    fromUserId = userId ?? "default_user"
                };
                
                Debug.Log("送信準備: message=" + connectionResponse.message + 
                         ", timestamp=" + connectionResponse.timestamp + 
                         ", success=" + connectionResponse.success + 
                         ", sessionId=" + connectionResponse.sessionId + 
                         ", fromUserId=" + connectionResponse.fromUserId);
                
                try
                {
                    // サンプルと同じ方式でシリアライズ
                    var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                        new IMessagePackFormatter[] { new ServerResponseFormatter() },
                        new IFormatterResolver[] { ServerResponseResolver.Instance }
                    ));
                    byte[] connectionData = MessagePackSerializer.Serialize(connectionResponse, options);
                    client.GetStream().Write(connectionData, 0, connectionData.Length);
                    Debug.Log("クライアントに接続情報送信成功: userId=" + userId + ", sessionId=" + currentSessionId);
                }
                catch (Exception serializeError)
                {
                    Debug.LogError("シリアライゼーションエラー: " + serializeError.Message);
                    Debug.LogError("接続レスポンス詳細: " + 
                                 "message=" + connectionResponse.message + 
                                 ", timestamp=" + connectionResponse.timestamp + 
                                 ", success=" + connectionResponse.success + 
                                 ", sessionId=" + connectionResponse.sessionId + 
                                 ", fromUserId=" + connectionResponse.fromUserId);
                }
                
                // クライアント処理スレッド開始
                Thread clientThread = new Thread(() => HandleClient(client, userId));
                clientThread.Start();
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogError("サーバーループエラー: " + e.Message);
                }
                break;
            }
        }
    }
    
    void HandleClient(TcpClient client, string userId)
    {
        NetworkStream stream = client.GetStream();
        
        try
        {
            while (running && client.Connected)
            {
                // データを受信
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead > 0)
                {
                    // データタイプを判定して適切にデシリアライズ
                    byte[] actualData = new byte[bytesRead];
                    Array.Copy(buffer, 0, actualData, 0, bytesRead);
                    
                    try
                    {
                        // まずServerRequestとして試行
                        var reader = new MessagePackReader(actualData);
                        var deserializeOptions = MessagePackSerializerOptions.Standard;
                        var formatter = new ServerRequestFormatter();
                        var request = formatter.Deserialize(ref reader, deserializeOptions);
                        
                        Debug.Log("受信 [" + userId + "]: " + request.message);
                        
                        // メッセージ転送処理
                        ForwardMessageToOtherClients(request, userId);
                        
                        // 送信者への確認レスポンス
                        var response = new XRSharing.ServerResponse
                        {
                            message = "メッセージ受信確認",
                            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            success = true,
                            sessionId = currentSessionId ?? "default_session",
                            fromUserId = userId ?? "default_user"
                        };
                        
                        try
                        {
                            // サンプルと同じ方式でシリアライズ
                            var responseOptions = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                                new IMessagePackFormatter[] { new ServerResponseFormatter() },
                                new IFormatterResolver[] { ServerResponseResolver.Instance }
                            ));
                            byte[] responseData = MessagePackSerializer.Serialize(response, responseOptions);
                            stream.Write(responseData, 0, responseData.Length);
                            Debug.Log("確認レスポンス送信成功: " + userId);
                        }
                        catch (Exception responseError)
                        {
                            Debug.LogError("確認レスポンス送信エラー: " + responseError.Message);
                        }
                    }
                    catch (Exception requestError)
                    {
                        // ServerRequestで失敗した場合、EventDataとして試行
                        try
                        {
                            var reader = new MessagePackReader(actualData);
                            var deserializeOptions = MessagePackSerializerOptions.Standard;
                            var eventFormatter = new EventDataFormatter();
                            var eventData = eventFormatter.Deserialize(ref reader, deserializeOptions);
                            
                            Debug.Log($"イベント受信 [{userId}]: {eventData.eventType} - {eventData.eventData}");
                            
                            // イベント転送処理
                            ForwardEventToOtherClients(eventData, userId);
                            
                            // 送信者への確認レスポンス
                            var response = new XRSharing.ServerResponse
                            {
                                message = "イベント受信確認",
                                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                success = true,
                                sessionId = currentSessionId ?? "default_session",
                                fromUserId = userId ?? "default_user"
                            };
                            
                            try
                            {
                                // サンプルと同じ方式でシリアライズ
                                var responseOptions = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                                    new IMessagePackFormatter[] { new ServerResponseFormatter() },
                                    new IFormatterResolver[] { ServerResponseResolver.Instance }
                                ));
                                byte[] responseData = MessagePackSerializer.Serialize(response, responseOptions);
                                stream.Write(responseData, 0, responseData.Length);
                                Debug.Log("イベント確認レスポンス送信成功: " + userId);
                            }
                            catch (Exception responseError)
                            {
                                Debug.LogError("イベント確認レスポンス送信エラー: " + responseError.Message);
                            }
                        }
                        catch (Exception eventError)
                        {
                            Debug.LogError("データデシリアライズエラー: " + eventError.Message);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("クライアント処理エラー: " + e.Message);
        }
        finally
        {
            connectedClients--;
            connectedClientsDict.Remove(userId);
            clientStreams.Remove(userId);
            client.Close();
            
            // UDPエンドポイントも削除
            lock (udpEndpointsLock)
            {
                clientUdpEndpoints.Remove(userId);
                Debug.Log($"UDPエンドポイント削除: {userId}");
            }
            
            Debug.Log("クライアント切断 [" + userId + "]");
        }
    }
    
    (IPAddress ipAddress, int port) ParseServerURL(string url)
    {
        try
        {
            // URLを解析
            Uri uri = new Uri(url);
            
            // ホスト名をIPアドレスに変換
            IPAddress ipAddress;
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
            {
                ipAddress = IPAddress.Any; // ローカル接続の場合はすべてのインターフェースでリッスン
            }
            else
            {
                // ホスト名をIPアドレスに解決
                IPHostEntry hostEntry = Dns.GetHostEntry(uri.Host);
                ipAddress = hostEntry.AddressList[0];
            }
            
            int port = uri.Port;
            if (port == -1)
            {
                // ポートが指定されていない場合はデフォルトポート
                port = useNgrok ? 443 : 7777;
            }
            
            return (ipAddress, port);
        }
        catch (Exception e)
        {
            Debug.LogError("URL解析エラー: " + e.Message);
            return (IPAddress.Any, 7777); // デフォルト値
        }
    }
    
    /// <summary>
    /// UDPサーバーループ
    /// </summary>
    void UdpServerLoop()
    {
        Debug.Log("UDPサーバーループ開始");
        Debug.Log($"UDPサーバー状態: udpRunning={udpRunning}, udpServer={udpServer != null}");
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        while (udpRunning)
        {
            try
            {
                if (_ShowLog)
                 Debug.Log("UDP受信待機中...");
                // UDPデータを受信
                byte[] receivedBytes = udpServer.Receive(ref remoteEndPoint);
                if (_ShowLog)
                 Debug.Log($"UDPデータ受信: {receivedBytes.Length} bytes from {remoteEndPoint}");
                
                // ヘッダーを解析（最初の4バイト）
                if (receivedBytes.Length >= 4)
                {
                    string header = System.Text.Encoding.ASCII.GetString(receivedBytes, 0, 4);
                    if (_ShowLog)
                     Debug.Log($"UDPヘッダー: '{header}'");
                    
                    if (header == "TRNS")
                    {
                        // ヘッダーを除いた部分を取得
                        byte[] transformDataBytes = new byte[receivedBytes.Length - 4];
                        Array.Copy(receivedBytes, 4, transformDataBytes, 0, transformDataBytes.Length);
                        
                        // TransformDataをデシリアライズ
                        var reader = new MessagePackReader(transformDataBytes);
                        var options = MessagePackSerializerOptions.Standard;
                        var formatter = new TransformDataFormatter();
                        var transformData = formatter.Deserialize(ref reader, options);

                        if (_ShowLog)
                        {
                         
                            Debug.Log("UDP受信 [" + transformData.userId + "]: TransformData from " + remoteEndPoint);
                            Debug.Log("  Position: " + transformData.position);
                            Debug.Log("  Rotation: " + transformData.rotation);
                            Debug.Log("  Timestamp: " + transformData.timestamp);

                        }
                        // クライアントのUDPエンドポイントを学習・保存
                        lock (udpEndpointsLock)
                        {
                            clientUdpEndpoints[transformData.userId] = remoteEndPoint;
                            if (_ShowLog)
                                Debug.Log($"UDPエンドポイント保存: {transformData.userId} -> {remoteEndPoint}");
                        }
                        
                        // 他のクライアントにUDPデータを転送
                        ForwardUdpDataToOtherClients(receivedBytes, transformData.userId);
                    }
                }
            }
            catch (Exception e)
            {
                if (udpRunning)
                {
                    Debug.LogError("UDPサーバーループエラー: " + e.Message);
                    Debug.LogError("UDPサーバーループエラー詳細: " + e.StackTrace);
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// UDPデータを他のクライアントに転送
    /// </summary>
    void ForwardUdpDataToOtherClients(byte[] data, string fromUserId)
    {
        lock (udpEndpointsLock)
        {
            if (_ShowLog)
             Debug.Log("UDPデータ転送開始: " + fromUserId + " -> " + clientUdpEndpoints.Count + "クライアント");
            
            // 保存されているUDPエンドポイントに転送
            foreach (var kvp in clientUdpEndpoints)
            {
                string clientUserId = kvp.Key;
                IPEndPoint clientUdpEndpoint = kvp.Value;
                
                // 送信者自身には転送しない
                if (clientUserId == fromUserId)
                {
                    continue;
                }
                
                try
                {
                    if(_ShowLog)
                     Debug.Log($"UDP転送試行: {fromUserId} -> {clientUserId} ({clientUdpEndpoint})");
                    
                    // サーバーのUDPソケットを使用して転送
                    udpServer.Send(data, data.Length, clientUdpEndpoint);
                    if(_ShowLog)
                     Debug.Log("UDPデータ転送成功: " + fromUserId + " -> " + clientUserId + " (" + clientUdpEndpoint + ")");
                }
                catch (Exception e)
                {
                    Debug.LogError("UDPデータ転送エラー: " + clientUserId + " - " + e.Message);
                }
            }
        }
    }
    
    void OnDestroy()
    {
        StopServer();
    }
}

/// <summary>
/// サーバーへのリクエスト
/// </summary>
[MessagePackObject]
public class ServerRequest
{
    [Key(0)]
    public string message;
    
    [Key(1)]
    public string userId;
    
    [Key(2)]
    public string sessionId;
}

/// <summary>
/// サーバーからのレスポンス
/// </summary>
[MessagePackObject]
public class ServerResponse
{
    [Key(0)]
    public string message;
    
    [Key(1)]
    public string timestamp;
    
    [Key(2)]
    public bool success;
    
    [Key(3)]
    public string sessionId;
    
    [Key(4)]
    public string fromUserId;
}
