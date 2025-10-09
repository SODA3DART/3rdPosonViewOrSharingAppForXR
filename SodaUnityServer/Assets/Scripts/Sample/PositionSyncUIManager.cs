using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PositionSyncSampleのUI管理クラス
/// OnGUIメソッドとUI関連の処理を担当
/// </summary>
public class PositionSyncUIManager : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("UI表示エリアの位置とサイズ")]
    public Rect uiArea = new Rect(10, 10, 400, 600);
    
    [Tooltip("イベント履歴の表示数")]
    public int displayHistoryCount = 10;
    
    [Tooltip("UI表示を有効にする")]
    public bool showUI = true;
    
    // PositionSyncSampleへの参照
    private PositionSyncSample positionSyncSample;
    
    // PositionSyncManagerへの参照
    private PositionSyncManager syncManager;
    
    void Start()
    {
        // PositionSyncSampleを自動検索
        positionSyncSample = GetComponent<PositionSyncSample>();
        if (positionSyncSample == null)
        {
            positionSyncSample = FindObjectOfType<PositionSyncSample>();
        }
        
        // PositionSyncManagerを自動検索
        syncManager = GetComponent<PositionSyncManager>();
        if (syncManager == null)
        {
            syncManager = FindObjectOfType<PositionSyncManager>();
        }
        
        if (positionSyncSample == null)
        {
            Debug.LogError("[PositionSyncUIManager] PositionSyncSampleが見つかりません");
        }
        
        if (syncManager == null)
        {
            Debug.LogError("[PositionSyncUIManager] PositionSyncManagerが見つかりません");
        }
    }
    
    void OnGUI()
    {
        if (!showUI || positionSyncSample == null || syncManager == null) return;
        
        GUILayout.BeginArea(uiArea);
        
        // タイトル
        GUILayout.Label("=== PositionSyncSample UI ===", GUI.skin.box);
        GUILayout.Space(5);
        
        // 接続状態
        if (positionSyncSample.targetClient != null)
        {
            GUILayout.Label($"接続状態: {(positionSyncSample.targetClient.isConnected ? "接続中" : "未接続")}");
            GUILayout.Label($"セッションID: {positionSyncSample.targetClient.sessionId}");
        }
        else
        {
            GUILayout.Label("接続状態: XRSharingClient未設定");
        }
        
        GUILayout.Space(10);
        
        // 位置同期状態
        GUILayout.Label("=== 位置同期状態 ===", GUI.skin.box);
        if (syncManager != null)
        {
            GUILayout.Label($"同期完了: {syncManager.isPositionSynced}");
            GUILayout.Label($"基準デバイス: {syncManager.isReferenceDevice}");
            GUILayout.Label($"基準方向: {syncManager.referenceDirection}");
            
            GUILayout.Space(5);
            
            // 同期ポイント情報
            if (syncManager.syncPoints != null && syncManager.syncPoints.Length >= 2)
            {
                GUILayout.Label("=== 同期ポイント ===");
                for (int i = 0; i < syncManager.syncPoints.Length; i++)
                {
                    if (syncManager.syncPoints[i] != null)
                    {
                        GUILayout.Label($"Point{i + 1}: {syncManager.syncPoints[i].position}");
                    }
                    else
                    {
                        GUILayout.Label($"Point{i + 1}: 未設定");
                    }
                }
            }
            
            GUILayout.Space(5);
            
            // シェアリングルートオブジェクト状態
            if (syncManager.sharingRootObject != null)
            {
                GUILayout.Label($"シェアリングルート: {(syncManager.sharingRootObject.gameObject.activeInHierarchy ? "有効" : "無効")}");
            }
            else
            {
                GUILayout.Label("シェアリングルート: 未設定");
            }
        }
        else
        {
            GUILayout.Label("PositionSyncManagerが見つかりません");
        }
        
        GUILayout.Space(10);
        
        // テストボタン
        GUILayout.Label("=== テスト操作 ===", GUI.skin.box);
        
        if (positionSyncSample.targetClient != null && positionSyncSample.targetClient.isConnected)
        {
            if (GUILayout.Button("テスト位置同期送信"))
            {
                positionSyncSample.SendTestPositionSync();
            }
            
            if (GUILayout.Button("基準位置同期送信"))
            {
                positionSyncSample.SendReferencePositionSync();
            }
        }
        else
        {
            GUILayout.Label("XRSharingClientに接続されていません");
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("手動でシェアリングオブジェクト有効化"))
        {
            positionSyncSample.EnableSharingObjects();
        }
        
        GUILayout.Space(10);
        
        // イベント履歴
        GUILayout.Label("=== イベント履歴 ===", GUI.skin.box);
        
        if (positionSyncSample.eventHistory != null && positionSyncSample.eventHistory.Count > 0)
        {
            // 履歴表示（最新10件）
            int displayCount = Mathf.Min(displayHistoryCount, positionSyncSample.eventHistory.Count);
            for (int i = positionSyncSample.eventHistory.Count - displayCount; i < positionSyncSample.eventHistory.Count; i++)
            {
                if (i >= 0)
                {
                    var eventItem = positionSyncSample.eventHistory[i];
                    GUILayout.Label($"[{eventItem.timestamp}] {eventItem.eventType} (x{eventItem.count})");
                    GUILayout.Label($"  Data: {eventItem.eventData}");
                    GUILayout.Label($"  From: {eventItem.fromUserId}");
                    GUILayout.Space(5);
                }
            }
        }
        else
        {
            GUILayout.Label("イベント履歴なし");
        }
        
        GUILayout.EndArea();
    }
}
