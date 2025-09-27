using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using System;
using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;
using XRSharing;

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
    
    // イベント
    public System.Action<string, string> OnMessageReceived; // (message, fromUserId)
    public System.Action OnConnected;
    public System.Action OnDisconnected;
    public System.Action<string> OnError;
    
    void Start()
    {
        if (autoConnect)
        {
            ConnectToServer();
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
            GUILayout.Label("=== UDP Transform送信テスト ===");
            if (GUILayout.Button("Transform送信"))
            {
                // メインカメラのTransformを送信
                if (Camera.main != null)
                {
                    SendTransformData(Camera.main.transform);
                }
            }
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
            
            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            LogError("接続エラー: " + e.Message);
            LogError("接続先: " + serverURL);
            OnError?.Invoke("接続エラー: " + e.Message);
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
            OnError?.Invoke("送信エラー: " + e.Message);
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
                    
                    // サンプルと同じ方式でデシリアライズ
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
                    
                    // サンプルと同じようにシンプルに
                    OnMessageReceived?.Invoke(response.message, response.fromUserId);
                }
            }
            catch (Exception e)
            {
                if (running)
                {
                    LogError("受信エラー: " + e.Message);
                    OnError?.Invoke("受信エラー: " + e.Message);
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
    /// UDPでTransformDataを送信
    /// </summary>
    public void SendTransformData(UnityEngine.Transform transform)
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
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            
            // サンプルと同じ方式でシリアライズ
            var options = MessagePackSerializerOptions.Standard.WithResolver(CompositeResolver.Create(
                new IMessagePackFormatter[] { new XRSharing.TransformDataFormatter() },
                new IFormatterResolver[] { XRSharing.TransformDataResolver.Instance }
            ));
            
            byte[] data = MessagePackSerializer.Serialize(transformData, options);
            udpClient.Send(data, data.Length);
            
            LogDebug("UDP TransformData送信完了: " + transformData.userId);
        }
        catch (Exception e)
        {
            LogError("UDP TransformData送信エラー: " + e.Message);
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
            Debug.Log("[XRSharingClient] " + message);
        }
    }
    
    void LogError(string message)
    {
        Debug.LogError("[XRSharingClient] " + message);
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
