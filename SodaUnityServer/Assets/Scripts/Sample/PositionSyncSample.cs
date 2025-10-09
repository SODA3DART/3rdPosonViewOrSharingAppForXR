using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TCPイベントで異なるデバイス間の空間座標同期を行うメインクラス
/// 各マネージャークラスを協調させる「司令塔」としての役割
/// </summary>
public class PositionSyncSample : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("位置同期のコアロジックを管理するマネージャー")]
    public PositionSyncManager syncManager;
    
    [Tooltip("UI管理を担当するマネージャー")]
    public PositionSyncUIManager uiManager;
    
    [Header("XRSharingClient Reference")]
    [Tooltip("通信対象のXRSharingClient")]
    public XRSharingClient targetClient;
    
    [Header("Event History")]
    [Tooltip("受信したイベントの履歴を保持する最大数")]
    public int maxEventHistory = 50;
    
    [Tooltip("受信したイベントの履歴")]
    public List<EventHistory> eventHistory = new List<EventHistory>();
    
    [Header("Test Settings")]
    [Tooltip("テスト用の位置同期イベント")]
    public string testPositionSyncEvent = "POSITION_SYNC";
    public string testPositionSyncData = "{\"point1\":{\"x\":0,\"y\":0,\"z\":0},\"point2\":{\"x\":1,\"y\":0,\"z\":0}}";
    
    [System.Serializable]
    public class EventHistory
    {
        public string eventType;
        public string eventData;
        public string fromUserId;
        public string timestamp;
        public int count;
        
        public EventHistory(string type, string data, string userId)
        {
            eventType = type;
            eventData = data;
            fromUserId = userId;
            timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
            count = 1;
        }
    }
    
    // PositionSyncDataクラスは独立したファイルに移動済み
    
    void Start()
    {
        // マネージャーコンポーネントの自動検索
        if (syncManager == null)
        {
            syncManager = GetComponent<PositionSyncManager>();
            if (syncManager == null)
            {
                syncManager = FindObjectOfType<PositionSyncManager>();
            }
        }
        
        if (uiManager == null)
        {
            uiManager = GetComponent<PositionSyncUIManager>();
            if (uiManager == null)
            {
                uiManager = FindObjectOfType<PositionSyncUIManager>();
            }
        }
        
        // XRSharingClientが設定されていない場合は自動検索
        if (targetClient == null)
        {
            targetClient = FindObjectOfType<XRSharingClient>();
        }
        
        // イベントリスナーを設定
        if (targetClient != null)
        {
            targetClient.OnCustomEvent.AddListener(OnEventReceived);
            targetClient.OnEventReceived += OnEventReceivedWithUserId;
            targetClient.OnPositionSyncEvent += OnEventReceivedWithUserId;
            LogDebug("PositionSyncSample: イベントリスナーを設定しました");
        }
        else
        {
            LogError("XRSharingClientが見つかりません");
        }
        
        // マネージャーコンポーネントの初期化（警告のみ）
        if (syncManager == null)
        {
            LogDebug("PositionSyncManagerが見つかりません - 一部機能が制限されます");
        }
        
        if (uiManager == null)
        {
            LogDebug("PositionSyncUIManagerが見つかりません - UI機能が制限されます");
        }
    }
    
    void OnEnable()
    {
        // コンポーネントが有効化された時にイベントリスナーを再設定
        if (targetClient != null)
        {
            targetClient.OnCustomEvent.AddListener(OnEventReceived);
            targetClient.OnEventReceived += OnEventReceivedWithUserId;
            targetClient.OnPositionSyncEvent += OnEventReceivedWithUserId;
            LogDebug("PositionSyncSample: イベントリスナーを再設定しました");
        }
    }
    
    void OnDisable()
    {
        // コンポーネントが無効化された時にイベントリスナーを解除
        if (targetClient != null)
        {
            targetClient.OnCustomEvent.RemoveListener(OnEventReceived);
            targetClient.OnEventReceived -= OnEventReceivedWithUserId;
            targetClient.OnPositionSyncEvent -= OnEventReceivedWithUserId;
            LogDebug("PositionSyncSample: イベントリスナーを解除しました");
        }
    }
    
    void OnDestroy()
    {
        // イベントリスナーを解除
        if (targetClient != null)
        {
            targetClient.OnCustomEvent.RemoveListener(OnEventReceived);
            targetClient.OnEventReceived -= OnEventReceivedWithUserId;
            targetClient.OnPositionSyncEvent -= OnEventReceivedWithUserId;
        }
    }
    
    /// <summary>
    /// UnityEvent経由でイベントを受信
    /// </summary>
    public void OnEventReceived(string eventType, string eventData)
    {
        LogDebug($"UnityEvent受信: {eventType} - {eventData}");
        AddToHistory(eventType, eventData, "UnityEvent");
        
        // イベントタイプに応じた処理
        HandleEvent(eventType, eventData);
    }
    
    /// <summary>
    /// Action経由でイベントを受信（送信者情報付き）
    /// </summary>
    public void OnEventReceivedWithUserId(string eventType, string eventData, string fromUserId)
    {
        LogDebug($"Action受信: {eventType} - {eventData} (from: {fromUserId})");
        AddToHistory(eventType, eventData, fromUserId);
        
        // イベントタイプに応じた処理
        HandleEvent(eventType, eventData);
    }
    
    /// <summary>
    /// イベント履歴に追加
    /// </summary>
    void AddToHistory(string eventType, string eventData, string fromUserId)
    {
        // 同じイベントが連続で来た場合はカウントを増やす
        if (eventHistory.Count > 0)
        {
            var lastEvent = eventHistory[eventHistory.Count - 1];
            if (lastEvent.eventType == eventType && 
                lastEvent.eventData == eventData && 
                lastEvent.fromUserId == fromUserId)
            {
                lastEvent.count++;
                return;
            }
        }
        
        // 新しいイベントを追加
        eventHistory.Add(new EventHistory(eventType, eventData, fromUserId));
        
        // 履歴が上限を超えた場合は古いものを削除
        if (eventHistory.Count > maxEventHistory)
        {
            eventHistory.RemoveAt(0);
        }
    }
    
    /// <summary>
    /// イベントタイプに応じた処理（各マネージャーに委譲）
    /// </summary>
    void HandleEvent(string eventType, string eventData)
    {
        if (syncManager == null)
        {
            LogDebug($"PositionSyncManagerが設定されていません - イベント {eventType} をスキップします");
            return;
        }
        
        switch (eventType)
        {
            case "POSITION_SYNC":
                syncManager.HandlePositionSync(eventData);
                break;
            case "MARKER_PLACED":
                syncManager.HandleMarkerPlaced(eventData);
                break;
            case "VPS_ANCHOR":
                syncManager.HandleVPSAnchor(eventData);
                break;
            case "SYNC_COMPLETE":
                syncManager.HandleSyncComplete(eventData);
                break;
            default:
                LogDebug($"未知のイベントタイプ: {eventType}");
                break;
        }
    }
    
    // コアロジックはPositionSyncManagerに移動済み
    
    /// <summary>
    /// テスト用の位置同期イベント送信
    /// </summary>
    public void SendTestPositionSync()
    {
        if (targetClient != null && targetClient.isConnected)
        {
            // テスト用の位置同期データを作成
            var testData = new PositionSyncData
            {
                point1 = new Vector3(0, 0, 0),
                point2 = new Vector3(1, 0, 0),
                deviceType = "TestDevice",
                sessionId = targetClient.sessionId,
                isReferenceDevice = syncManager != null ? syncManager.isReferenceDevice : false,
                referenceDirection = syncManager != null ? syncManager.referenceDirection : Vector3.forward
            };
            
            string eventData = JsonUtility.ToJson(testData);
            targetClient.SendEvent(testPositionSyncEvent, eventData);
            LogDebug($"テスト位置同期イベントを送信: {testPositionSyncEvent} - {eventData}");
            
            // ローカルでも位置同期を実行（テスト用）
            if (syncManager != null)
            {
                syncManager.HandlePositionSync(eventData);
            }
            else
            {
                LogDebug("PositionSyncManagerが設定されていないため、ローカル同期をスキップします");
            }
        }
        else
        {
            LogError("XRSharingClientに接続されていません");
        }
    }
    
    /// <summary>
    /// 基準デバイス（iPad VPS）からの位置同期イベント送信
    /// </summary>
    public void SendReferencePositionSync()
    {
        if (targetClient != null && targetClient.isConnected)
        {
            if (syncManager == null)
            {
                LogDebug("PositionSyncManagerが設定されていないため、基準位置同期をスキップします");
                return;
            }
            
            if (syncManager.syncPoints.Length < 2 || 
                syncManager.syncPoints[0] == null || syncManager.syncPoints[1] == null)
            {
                LogError("同期ポイントが設定されていません");
                return;
            }
            
            // 基準デバイスからの位置同期データを作成
            var referenceData = new PositionSyncData
            {
                point1 = syncManager.syncPoints[0].position,
                point2 = syncManager.syncPoints[1].position,
                deviceType = "iPad_VPS",
                sessionId = targetClient.sessionId,
                isReferenceDevice = true,
                referenceDirection = (syncManager.syncPoints[1].position - syncManager.syncPoints[0].position).normalized
            };
            
            string eventData = JsonUtility.ToJson(referenceData);
            targetClient.SendEvent("POSITION_SYNC", eventData);
            LogDebug($"基準位置同期イベントを送信: {eventData}");
        }
        else
        {
            LogError("XRSharingClientに接続されていません");
        }
    }
    
    /// <summary>
    /// シェアリングオブジェクトの手動有効化（UI用）
    /// </summary>
    public void EnableSharingObjects()
    {
        if (syncManager != null)
        {
            syncManager.EnableSharingObjects();
        }
        else
        {
            LogDebug("PositionSyncManagerが設定されていないため、シェアリングオブジェクトの有効化をスキップします");
        }
    }
    
    /// <summary>
    /// イベント履歴をクリア
    /// </summary>
    public void ClearHistory()
    {
        eventHistory.Clear();
        Debug.Log("[PositionSyncSample] イベント履歴をクリアしました");
    }
    
    /// <summary>
    /// デバッグログ出力
    /// </summary>
    void LogDebug(string message)
    {
        Debug.Log($"[PositionSyncSample] {message}");
    }
    
    /// <summary>
    /// エラーログ出力
    /// </summary>
    void LogError(string message)
    {
        Debug.LogError($"[PositionSyncSample] {message}");
    }
}
