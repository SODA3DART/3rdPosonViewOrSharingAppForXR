using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 位置同期のコアロジックを管理するクラス
/// 座標同期の計算やオブジェクト操作を担当
/// </summary>
public class PositionSyncManager : MonoBehaviour
{
    [Header("Position Sync Settings")]
    [Tooltip("同期対象の2点の空間座標")]
    public Transform[] syncPoints = new Transform[2];
    
    [Tooltip("シェアリング用のルートオブジェクト")]
    public Transform sharingRootObject;
    
    [Tooltip("シェアリング用オブジェクトの配列")]
    public Transform[] sharingObjects;
    
    [Tooltip("位置同期が完了したかどうか")]
    public bool isPositionSynced = false;
    
    [Header("Device Settings")]
    [Tooltip("このデバイスが基準デバイス（iPad VPS）かどうか")]
    public bool isReferenceDevice = false;
    
    [Tooltip("基準方向ベクトル（基準デバイスから送信される）")]
    public Vector3 referenceDirection = Vector3.forward;
    
    [Header("Debug Settings")]
    [Tooltip("デバッグログを有効にする")]
    public bool enableDebugLogs = true;
    
    // PositionSyncSampleへの参照
    private PositionSyncSample positionSyncSample;
    
    void Start()
    {
        // PositionSyncSampleを自動検索
        positionSyncSample = GetComponent<PositionSyncSample>();
        if (positionSyncSample == null)
        {
            positionSyncSample = FindObjectOfType<PositionSyncSample>();
        }
        
        if (positionSyncSample == null)
        {
            Debug.LogError("[PositionSyncManager] PositionSyncSampleが見つかりません");
        }
        
        // シェアリングルートオブジェクトの初期化
        InitializeSharingRoot();
    }
    
    /// <summary>
    /// シェアリングルートオブジェクトの初期化
    /// </summary>
    void InitializeSharingRoot()
    {
        if (sharingRootObject != null)
        {
            sharingRootObject.gameObject.SetActive(false);
            LogDebug("シェアリングルートオブジェクトを初期化しました（無効状態）");
        }
        else
        {
            LogError("シェアリングルートオブジェクトが設定されていません");
        }
    }
    
    /// <summary>
    /// 位置同期イベントの処理
    /// </summary>
    public void HandlePositionSync(string eventData)
    {
        PositionSyncData data;
        try
        {
            data = JsonUtility.FromJson<PositionSyncData>(eventData);
        }
        catch (System.Exception e)
        {
            LogError($"位置同期イベントのJSONパースエラー: {e.Message}");
            return;
        }

        // Unityのオブジェクトを操作する処理をメインスレッドに渡す
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                LogDebug("メインスレッドで位置同期処理を実行します");
                LogDebug($"位置同期データ: Point1={data.point1}, Point2={data.point2}, Device={data.deviceType}");
                ExecutePositionSync(data);
            });
        }
        else
        {
            LogDebug("UnityMainThreadDispatcherが見つかりません - 直接実行します");
            LogDebug("メインスレッドで位置同期処理を実行します");
            LogDebug($"位置同期データ: Point1={data.point1}, Point2={data.point2}, Device={data.deviceType}");
            ExecutePositionSync(data);
        }
    }
    
    /// <summary>
    /// マーカー配置イベントの処理（Quest用）
    /// </summary>
    public void HandleMarkerPlaced(string eventData)
    {
        PositionSyncData data;
        try
        {
            data = JsonUtility.FromJson<PositionSyncData>(eventData);
        }
        catch (System.Exception e)
        {
            LogError($"マーカー配置イベントのJSONパースエラー: {e.Message}");
            return;
        }

        // Unityのオブジェクトを操作する処理をメインスレッドに渡す
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                LogDebug("メインスレッドでマーカー配置処理を実行します");
                LogDebug($"マーカー配置: Point1={data.point1}, Device={data.deviceType}");
                RecordMarkerPosition(data);
            });
        }
        else
        {
            LogDebug("UnityMainThreadDispatcherが見つかりません - 直接実行します");
            LogDebug("メインスレッドでマーカー配置処理を実行します");
            LogDebug($"マーカー配置: Point1={data.point1}, Device={data.deviceType}");
            RecordMarkerPosition(data);
        }
    }
    
    /// <summary>
    /// VPSアンカーイベントの処理（iPad用）
    /// </summary>
    public void HandleVPSAnchor(string eventData)
    {
        PositionSyncData data;
        try
        {
            data = JsonUtility.FromJson<PositionSyncData>(eventData);
        }
        catch (System.Exception e)
        {
            LogError($"VPSアンカーイベントのJSONパースエラー: {e.Message}");
            return;
        }

        // Unityのオブジェクトを操作する処理をメインスレッドに渡す
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                LogDebug("メインスレッドでVPSアンカー処理を実行します");
                LogDebug($"VPSアンカー: Point1={data.point1}, Device={data.deviceType}");
                RecordVPSAnchor(data);
            });
        }
        else
        {
            LogDebug("UnityMainThreadDispatcherが見つかりません - 直接実行します");
            LogDebug("メインスレッドでVPSアンカー処理を実行します");
            LogDebug($"VPSアンカー: Point1={data.point1}, Device={data.deviceType}");
            RecordVPSAnchor(data);
        }
    }
    
    /// <summary>
    /// 同期完了イベントの処理
    /// </summary>
    public void HandleSyncComplete(string eventData)
    {
        PositionSyncData data;
        try
        {
            data = JsonUtility.FromJson<PositionSyncData>(eventData);
        }
        catch (System.Exception e)
        {
            LogError($"同期完了イベントのJSONパースエラー: {e.Message}");
            return;
        }

        // Unityのオブジェクトを操作する処理をメインスレッドに渡す
        if (UnityMainThreadDispatcher.Exists())
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                LogDebug("メインスレッドで同期完了処理を実行します");
                LogDebug($"同期完了: Device={data.deviceType}");
                OnSyncComplete(data);
            });
        }
        else
        {
            LogDebug("UnityMainThreadDispatcherが見つかりません - 直接実行します");
            LogDebug("メインスレッドで同期完了処理を実行します");
            LogDebug($"同期完了: Device={data.deviceType}");
            OnSyncComplete(data);
        }
    }
    
    /// <summary>
    /// 位置同期の実行
    /// </summary>
    void ExecutePositionSync(PositionSyncData data)
    {
        if (syncPoints.Length < 2)
        {
            LogError("同期ポイントが2つ設定されていません");
            return;
        }

        if (data.isReferenceDevice)
        {
            referenceDirection = (data.point2 - data.point1).normalized;
            LogDebug($"基準方向を更新: {referenceDirection}");
        }

        syncPoints[0].position = data.point1;
        syncPoints[1].position = data.point2;

        LogDebug($"位置同期実行: Point1={data.point1}, Point2={data.point2}");
        LogDebug($"基準デバイス: {data.isReferenceDevice}, デバイスタイプ: {data.deviceType}");

        UpdateSharingObjectsWithRotation();
        EnableSharingObjects();
        isPositionSynced = true;
        NotifySyncComplete();
        LogDebug("位置同期が完了し、シェアリングオブジェクトを有効化しました");
    }
    
    /// <summary>
    /// 回転補正を考慮したシェアリングオブジェクトの更新
    /// </summary>
    void UpdateSharingObjectsWithRotation()
    {
        if (sharingObjects == null || sharingObjects.Length == 0)
        {
            LogDebug("シェアリングオブジェクトが設定されていません");
            return;
        }

        Vector3 localDirection = (syncPoints[1].position - syncPoints[0].position).normalized;
        Quaternion rotationCorrection = CalculateYAxisRotation(localDirection);

        LogDebug($"ローカル方向: {localDirection}");
        LogDebug($"基準方向: {referenceDirection}");
        LogDebug($"回転補正角度: {rotationCorrection.eulerAngles.y}度");

        for (int i = 0; i < sharingObjects.Length; i++)
        {
            if (sharingObjects[i] != null)
            {
                float distance = Vector3.Distance(syncPoints[0].position, syncPoints[1].position);
                Vector3 basePosition = referenceDirection * (distance * i / (sharingObjects.Length - 1));

                Vector3 correctedPosition = rotationCorrection * basePosition;
                sharingObjects[i].position = syncPoints[0].position + correctedPosition;
                sharingObjects[i].rotation = rotationCorrection * sharingObjects[i].rotation;

                LogDebug($"シェアリングオブジェクト[{i}]位置更新: {sharingObjects[i].position}");
            }
        }
    }
    
    /// <summary>
    /// Y軸回転の計算（鉛直軸のみ考慮）
    /// </summary>
    Quaternion CalculateYAxisRotation(Vector3 localDirection)
    {
        Vector3 localHorizontal = new Vector3(localDirection.x, 0, localDirection.z).normalized;
        Vector3 referenceHorizontal = new Vector3(referenceDirection.x, 0, referenceDirection.z).normalized;
        float angle = Vector3.SignedAngle(referenceHorizontal, localHorizontal, Vector3.up);
        LogDebug($"水平方向の回転角度: {angle}度");
        return Quaternion.AngleAxis(angle, Vector3.up);
    }
    
    /// <summary>
    /// シェアリングオブジェクトの有効化
    /// </summary>
    public void EnableSharingObjects()
    {
        if (sharingRootObject != null)
        {
            sharingRootObject.gameObject.SetActive(true);
            LogDebug("シェアリングルートオブジェクトを有効化しました");
        }
        else
        {
            LogError("シェアリングルートオブジェクトが設定されていません");
        }
    }
    
    /// <summary>
    /// マーカー位置の記録（Quest用）
    /// </summary>
    void RecordMarkerPosition(PositionSyncData data)
    {
        LogDebug($"マーカー位置を記録: {data.point1}");
        // マーカー位置の記録ロジックを実装
    }
    
    /// <summary>
    /// VPSアンカー位置の記録（iPad用）
    /// </summary>
    void RecordVPSAnchor(PositionSyncData data)
    {
        LogDebug($"VPSアンカー位置を記録: {data.point1}");
        // VPSアンカー位置の記録ロジックを実装
    }
    
    /// <summary>
    /// 同期完了の処理
    /// </summary>
    void OnSyncComplete(PositionSyncData data)
    {
        LogDebug($"同期完了: {data.deviceType}デバイスとの同期が完了しました");
        // 同期完了時の追加処理を実装
    }
    
    /// <summary>
    /// 同期完了の通知
    /// </summary>
    void NotifySyncComplete()
    {
        if (positionSyncSample != null && positionSyncSample.targetClient != null && positionSyncSample.targetClient.isConnected)
        {
            var completeData = new PositionSyncData
            {
                point1 = syncPoints[0].position,
                point2 = syncPoints[1].position,
                deviceType = "CurrentDevice",
                sessionId = positionSyncSample.targetClient.sessionId,
                isReferenceDevice = isReferenceDevice,
                referenceDirection = referenceDirection
            };
            string eventData = JsonUtility.ToJson(completeData);
            positionSyncSample.targetClient.SendEvent("SYNC_COMPLETE", eventData);
            LogDebug($"同期完了通知を送信: {eventData}");
        }
    }
    
    /// <summary>
    /// デバッグログ出力
    /// </summary>
    void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PositionSyncManager] {message}");
        }
    }
    
    /// <summary>
    /// エラーログ出力
    /// </summary>
    void LogError(string message)
    {
        Debug.LogError($"[PositionSyncManager] {message}");
    }
}
