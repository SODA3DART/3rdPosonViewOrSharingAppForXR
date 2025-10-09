using UnityEngine;

/// <summary>
/// 位置同期データの構造体
/// TCPイベントで送信される位置同期情報を格納
/// </summary>
[System.Serializable]
public class PositionSyncData
{
    [Tooltip("同期ポイント1の位置")]
    public Vector3 point1;
    
    [Tooltip("同期ポイント2の位置")]
    public Vector3 point2;
    
    [Tooltip("デバイスタイプ（iPad, Quest, etc.）")]
    public string deviceType;
    
    [Tooltip("セッションID")]
    public string sessionId;
    
    [Tooltip("基準デバイス（iPad VPS）かどうか")]
    public bool isReferenceDevice;
    
    [Tooltip("基準方向ベクトル")]
    public Vector3 referenceDirection;
    
    /// <summary>
    /// デフォルトコンストラクタ
    /// </summary>
    public PositionSyncData()
    {
        point1 = Vector3.zero;
        point2 = Vector3.zero;
        deviceType = "";
        sessionId = "";
        isReferenceDevice = false;
        referenceDirection = Vector3.forward;
    }
    
    /// <summary>
    /// パラメータ付きコンストラクタ
    /// </summary>
    public PositionSyncData(Vector3 point1, Vector3 point2, string deviceType, string sessionId, bool isReferenceDevice, Vector3 referenceDirection)
    {
        this.point1 = point1;
        this.point2 = point2;
        this.deviceType = deviceType;
        this.sessionId = sessionId;
        this.isReferenceDevice = isReferenceDevice;
        this.referenceDirection = referenceDirection;
    }
}

