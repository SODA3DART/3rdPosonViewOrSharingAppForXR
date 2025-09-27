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
    /// UDP用Transformデータ（位置・回転のみ）
    /// </summary>
    [MessagePackObject]
    public class TransformData
    {
        [Key(0)]
        public string header = "TRNS";
        
        [Key(1)]
        public string userId = "";
        
        [Key(2)]
        public string sessionId = "";
        
        [Key(3)]
        public UnityEngine.Vector3 position;
        
        [Key(4)]
        public UnityEngine.Quaternion rotation;
        
        [Key(5)]
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

    /// <summary>
    /// TransformData用カスタムフォーマッター
    /// </summary>
    public class TransformDataFormatter : IMessagePackFormatter<TransformData>
    {
        public TransformData Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 11) throw new MessagePackSerializationException("Invalid array length for TransformData");

            var header = reader.ReadString() ?? "TRNS";
            var userId = reader.ReadString() ?? "";
            var sessionId = reader.ReadString() ?? "";
            
            // Vector3とQuaternionを個別のfloatとして読み取り
            var positionX = reader.ReadSingle();
            var positionY = reader.ReadSingle();
            var positionZ = reader.ReadSingle();
            var rotationX = reader.ReadSingle();
            var rotationY = reader.ReadSingle();
            var rotationZ = reader.ReadSingle();
            var rotationW = reader.ReadSingle();
            var timestamp = reader.ReadInt64();

            return new TransformData
            {
                header = header,
                userId = userId,
                sessionId = sessionId,
                position = new UnityEngine.Vector3(positionX, positionY, positionZ),
                rotation = new UnityEngine.Quaternion(rotationX, rotationY, rotationZ, rotationW),
                timestamp = timestamp
            };
        }

        public void Serialize(ref MessagePackWriter writer, TransformData value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(11);
            writer.Write(value.header ?? "TRNS");
            writer.Write(value.userId ?? "");
            writer.Write(value.sessionId ?? "");
            
            // Vector3とQuaternionを個別のfloatとして書き込み
            writer.Write(value.position.x);
            writer.Write(value.position.y);
            writer.Write(value.position.z);
            writer.Write(value.rotation.x);
            writer.Write(value.rotation.y);
            writer.Write(value.rotation.z);
            writer.Write(value.rotation.w);
            writer.Write(value.timestamp);
        }
    }

    /// <summary>
    /// TransformData用リゾルバ
    /// </summary>
    public class TransformDataResolver : IFormatterResolver
    {
        public static readonly TransformDataResolver Instance = new TransformDataResolver();

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(TransformData))
            {
                return (IMessagePackFormatter<T>)new TransformDataFormatter();
            }
            return null;
        }
    }

}
