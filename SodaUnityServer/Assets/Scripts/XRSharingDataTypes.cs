using MessagePack;
using MessagePack.Resolvers;
using MessagePack.Formatters;

/// <summary>
/// XRシェアリングで使用する共通データ型
/// </summary>
namespace XRSharing
{
    /// <summary>
    /// サーバーへのリクエスト
    /// </summary>
    [MessagePackObject]
    public class ServerRequest
    {
        [Key(0)]
        public string message = "";
        
        [Key(1)]
        public string userId = "";
        
        [Key(2)]
        public string sessionId = "";
    }

    /// <summary>
    /// サーバーからのレスポンス
    /// </summary>
    [MessagePackObject]
    public class ServerResponse
    {
        [Key(0)]
        public string message = "";
        
        [Key(1)]
        public string timestamp = "";
        
        [Key(2)]
        public bool success = false;
        
        [Key(3)]
        public string sessionId = "";
        
        [Key(4)]
        public string fromUserId = "";
    }
    
    /// <summary>
    /// XRデバイスタイプ
    /// </summary>
    public enum XRDeviceType
    {
        Auto,
        Quest,
        iPad,
        Desktop,
        Other
    }
    
    /// <summary>
    /// 位置・回転データ
    /// </summary>
    [MessagePackObject]
    public class TransformData
    {
        [Key(0)]
        public UnityEngine.Vector3 position;
        
        [Key(1)]
        public UnityEngine.Quaternion rotation;
        
        [Key(2)]
        public UnityEngine.Vector3 scale;
        
        [Key(3)]
        public string userId;
        
        [Key(4)]
        public long timestamp;
    }
    
    /// <summary>
    /// ハンドトラッキングデータ
    /// </summary>
    [MessagePackObject]
    public class HandTrackingData
    {
        [Key(0)]
        public UnityEngine.Vector3 leftHandPosition;
        
        [Key(1)]
        public UnityEngine.Quaternion leftHandRotation;
        
        [Key(2)]
        public UnityEngine.Vector3 rightHandPosition;
        
        [Key(3)]
        public UnityEngine.Quaternion rightHandRotation;
        
        [Key(4)]
        public bool isTracking;
        
        [Key(5)]
        public string userId;
    }
    
    /// <summary>
    /// アイトラッキングデータ
    /// </summary>
    [MessagePackObject]
    public class EyeTrackingData
    {
        [Key(0)]
        public UnityEngine.Vector3 leftEyePosition;
        
        [Key(1)]
        public UnityEngine.Quaternion leftEyeRotation;
        
        [Key(2)]
        public UnityEngine.Vector3 rightEyePosition;
        
        [Key(3)]
        public UnityEngine.Quaternion rightEyeRotation;
        
        [Key(4)]
        public bool isTracking;
        
        [Key(5)]
        public string userId;
    }

    /// <summary>
    /// ServerRequest用カスタムフォーマッター
    /// </summary>
    public class ServerRequestFormatter : IMessagePackFormatter<ServerRequest>
    {
        public ServerRequest Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 3) throw new MessagePackSerializationException("Invalid array length for ServerRequest");

            var message = reader.ReadString() ?? "";
            var userId = reader.ReadString() ?? "";
            var sessionId = reader.ReadString() ?? "";

            return new ServerRequest
            {
                message = message,
                userId = userId,
                sessionId = sessionId
            };
        }

        public void Serialize(ref MessagePackWriter writer, ServerRequest value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.message ?? "");
            writer.Write(value.userId ?? "");
            writer.Write(value.sessionId ?? "");
        }
    }

    /// <summary>
    /// ServerResponse用カスタムフォーマッター
    /// </summary>
    public class ServerResponseFormatter : IMessagePackFormatter<ServerResponse>
    {
        public ServerResponse Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 5) throw new MessagePackSerializationException("Invalid array length for ServerResponse");

            var message = reader.ReadString() ?? "";
            var timestamp = reader.ReadString() ?? "";
            var success = reader.ReadBoolean();
            var sessionId = reader.ReadString() ?? "";
            var fromUserId = reader.ReadString() ?? "";

            return new ServerResponse
            {
                message = message,
                timestamp = timestamp,
                success = success,
                sessionId = sessionId,
                fromUserId = fromUserId
            };
        }

        public void Serialize(ref MessagePackWriter writer, ServerResponse value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(5);
            writer.Write(value.message ?? "");
            writer.Write(value.timestamp ?? "");
            writer.Write(value.success);
            writer.Write(value.sessionId ?? "");
            writer.Write(value.fromUserId ?? "");
        }
    }

    /// <summary>
    /// ServerRequest用リゾルバ
    /// </summary>
    public class ServerRequestResolver : IFormatterResolver
    {
        public static readonly ServerRequestResolver Instance = new ServerRequestResolver();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(ServerRequest))
            {
                return (IMessagePackFormatter<T>)new ServerRequestFormatter();
            }
            return null;
        }
    }

    /// <summary>
    /// ServerResponse用リゾルバ
    /// </summary>
    public class ServerResponseResolver : IFormatterResolver
    {
        public static readonly ServerResponseResolver Instance = new ServerResponseResolver();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(ServerResponse))
            {
                return (IMessagePackFormatter<T>)new ServerResponseFormatter();
            }
            return null;
        }
    }

}
