using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// UnityEventのデバッグ用クラス
/// XRSharingClientのOnCustomEventの動作を確認・テストするためのツール
/// </summary>
public class UnityEventDebug : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("デバッグログを有効にする")]
    public bool enableDebugLogs = true;
    
    [Tooltip("受信したイベントの履歴を保持する最大数")]
    public int maxEventHistory = 50;
    
    [Header("Event History")]
    [Tooltip("受信したイベントの履歴")]
    public List<EventHistory> eventHistory = new List<EventHistory>();
    
    [Header("XRSharingClient Reference")]
    [Tooltip("デバッグ対象のXRSharingClient")]
    public XRSharingClient targetClient;
    
    [Header("Test Events")]
    [Tooltip("テスト用のイベント送信")]
    public string testEventType = "TEST_EVENT";
    public string testEventData = "{\"message\":\"Test from UnityEventDebug\"}";
    
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
    
    void Start()
    {
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
            LogDebug("UnityEventDebug: イベントリスナーを設定しました");
        }
        else
        {
            LogError("XRSharingClientが見つかりません");
        }
    }
    
    void OnDestroy()
    {
        // イベントリスナーを解除
        if (targetClient != null)
        {
            targetClient.OnCustomEvent.RemoveListener(OnEventReceived);
            targetClient.OnEventReceived -= OnEventReceivedWithUserId;
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
    /// イベントタイプに応じた処理
    /// </summary>
    void HandleEvent(string eventType, string eventData)
    {
        switch (eventType)
        {
            case "TEST_EVENT":
                HandleTestEvent(eventData);
                break;
            case "BUTTON_CLICK":
                HandleButtonClick(eventData);
                break;
            case "OBJECT_SELECTED":
                HandleObjectSelected(eventData);
                break;
            case "ANIMATION_TRIGGER":
                HandleAnimationTrigger(eventData);
                break;
            default:
                LogDebug($"未知のイベントタイプ: {eventType}");
                break;
        }
    }
    
    /// <summary>
    /// テストイベントの処理
    /// </summary>
    void HandleTestEvent(string eventData)
    {
        LogDebug("テストイベントを受信しました");
        
        // JSONデータをパースして処理
        try
        {
            var data = JsonUtility.FromJson<TestEventData>(eventData);
            LogDebug($"テストメッセージ: {data.message}");
        }
        catch (System.Exception e)
        {
            LogError($"テストイベントのJSONパースエラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// ボタンクリックイベントの処理
    /// </summary>
    void HandleButtonClick(string eventData)
    {
        LogDebug("ボタンクリックイベントを受信しました");
        
        try
        {
            var data = JsonUtility.FromJson<ButtonClickData>(eventData);
            LogDebug($"ボタン名: {data.buttonName}, 位置: {data.position}");
            
            // 実際の処理（例：UIの更新）
            // UpdateButtonState(data.buttonName, data.position);
        }
        catch (System.Exception e)
        {
            LogError($"ボタンクリックイベントのJSONパースエラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// オブジェクト選択イベントの処理
    /// </summary>
    void HandleObjectSelected(string eventData)
    {
        LogDebug("オブジェクト選択イベントを受信しました");
        
        try
        {
            var data = JsonUtility.FromJson<ObjectSelectData>(eventData);
            LogDebug($"オブジェクト名: {data.objectName}, ID: {data.objectId}");
            
            // 実際の処理（例：オブジェクトのハイライト）
            // HighlightObject(data.objectName, data.objectId);
        }
        catch (System.Exception e)
        {
            LogError($"オブジェクト選択イベントのJSONパースエラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// アニメーショントリガーイベントの処理
    /// </summary>
    void HandleAnimationTrigger(string eventData)
    {
        LogDebug("アニメーショントリガーイベントを受信しました");
        
        try
        {
            var data = JsonUtility.FromJson<AnimationData>(eventData);
            LogDebug($"アニメーション名: {data.animationName}");
            
            // 実際の処理（例：アニメーション再生）
            // PlayAnimation(data.animationName);
        }
        catch (System.Exception e)
        {
            LogError($"アニメーショントリガーイベントのJSONパースエラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// テストイベントを送信
    /// </summary>
    public void SendTestEvent()
    {
        if (targetClient != null && targetClient.isConnected)
        {
            targetClient.SendEvent(testEventType, testEventData);
            LogDebug($"テストイベントを送信: {testEventType} - {testEventData}");
        }
        else
        {
            LogError("XRSharingClientに接続されていません");
        }
    }
    
    /// <summary>
    /// イベント履歴をクリア
    /// </summary>
    public void ClearHistory()
    {
        eventHistory.Clear();
        LogDebug("イベント履歴をクリアしました");
    }
    
    /// <summary>
    /// イベント統計を表示
    /// </summary>
    public void ShowEventStatistics()
    {
        var stats = new Dictionary<string, int>();
        
        foreach (var eventItem in eventHistory)
        {
            if (stats.ContainsKey(eventItem.eventType))
            {
                stats[eventItem.eventType] += eventItem.count;
            }
            else
            {
                stats[eventItem.eventType] = eventItem.count;
            }
        }
        
        LogDebug("=== イベント統計 ===");
        foreach (var stat in stats)
        {
            LogDebug($"{stat.Key}: {stat.Value}回");
        }
    }
    
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 400, 600));
        
        GUILayout.Label("=== UnityEvent Debug ===", GUI.skin.box);
        
        // 接続状態
        if (targetClient != null)
        {
            GUILayout.Label($"接続状態: {(targetClient.isConnected ? "接続中" : "未接続")}");
            GUILayout.Label($"セッションID: {targetClient.sessionId}");
            GUILayout.Label($"ユーザーID: {targetClient.userId}");
        }
        
        GUILayout.Space(10);
        
        // テストイベント送信
        GUILayout.Label("=== テストイベント送信 ===");
        if (GUILayout.Button("テストイベント送信"))
        {
            SendTestEvent();
        }
        
        GUILayout.Space(10);
        
        // イベント履歴
        GUILayout.Label($"=== イベント履歴 ({eventHistory.Count}/{maxEventHistory}) ===");
        if (GUILayout.Button("履歴クリア"))
        {
            ClearHistory();
        }
        if (GUILayout.Button("統計表示"))
        {
            ShowEventStatistics();
        }
        
        // 履歴表示（最新10件）
        int displayCount = Mathf.Min(10, eventHistory.Count);
        for (int i = eventHistory.Count - displayCount; i < eventHistory.Count; i++)
        {
            if (i >= 0)
            {
                var eventItem = eventHistory[i];
                GUILayout.Label($"[{eventItem.timestamp}] {eventItem.eventType} (x{eventItem.count})");
                GUILayout.Label($"  Data: {eventItem.eventData}");
                GUILayout.Label($"  From: {eventItem.fromUserId}");
                GUILayout.Space(5);
            }
        }
        
        GUILayout.EndArea();
    }
    
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[UnityEventDebug] {message}");
        }
    }
    
    void LogError(string message)
    {
        Debug.LogError($"[UnityEventDebug] {message}");
    }
}

/// <summary>
/// テストイベント用データクラス
/// </summary>
[System.Serializable]
public class TestEventData
{
    public string message;
}

/// <summary>
/// ボタンクリック用データクラス
/// </summary>
[System.Serializable]
public class ButtonClickData
{
    public string buttonName;
    public string position;
}

/// <summary>
/// オブジェクト選択用データクラス
/// </summary>
[System.Serializable]
public class ObjectSelectData
{
    public string objectName;
    public int objectId;
}

/// <summary>
/// アニメーション用データクラス
/// </summary>
[System.Serializable]
public class AnimationData
{
    public string animationName;
}
