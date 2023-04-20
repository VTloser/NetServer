using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// 序列化工具类
/// </summary>
public static class ProtocalTool
{

    public const int DataSize = sizeof(Int32);  //发送数据长度预设 4字节
    public const int NameSize = sizeof(Int16);  //发送数据长度预设 2字节

    #region Json序列化

    /// <summary>
    /// Json序列化
    /// </summary>
    /// <param name="msgBase"></param>
    /// <returns></returns>
    private static byte[] Encode(MsgBase msgBase)
    {
        string s = JsonConvert.SerializeObject(msgBase);
        return Encoding.UTF8.GetBytes(s);
    }

    /// <summary>
    /// Json反序列化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static T Decode<T>(byte[] bytes, int offset, int count) where T : MsgBase
    {
        string s = Encoding.UTF8.GetString(bytes, offset, count);
        MsgBase msgBase = JsonConvert.DeserializeObject<T>(s);
        return (T)msgBase;
    }

    /// <summary>
    /// Json反序列化
    /// </summary>
    /// <param name="type"></param>
    /// <param name="bytes"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static MsgBase Decode(Type type, byte[] bytes, int offset, int count)
    {
        string s = Encoding.UTF8.GetString(bytes, offset, count);
        MsgBase msgBase = (MsgBase)JsonConvert.DeserializeObject(s, type);
        return msgBase;
    }


    /// <summary>
    /// Json数据封包
    /// </summary>
    /// <param name="msgBase"></param>
    public static byte[] PackageMessage(this MsgBase msgBase)
    {
        //数据编码
        byte[] nameBytes = EncodeName(msgBase);
        byte[] bodyBytes = Encode(msgBase);

        int len = nameBytes.Length + bodyBytes.Length;
        byte[] SendBytes = new byte[DataSize + len];

        //组装长度
        SendBytes[0] = (byte)(len % 256);
        SendBytes[1] = (byte)(len % 16777216 % 65536 / 256);
        SendBytes[2] = (byte)(len % 16777216 / 65536);
        SendBytes[3] = (byte)(len / 16777216);
        //SendBytes = BitConverter.GetBytes(len); //大小端

        //组装名字
        Array.Copy(nameBytes, 0, SendBytes, DataSize, nameBytes.Length);
        //组装消息体 
        Array.Copy(bodyBytes, 0, SendBytes, DataSize + nameBytes.Length, bodyBytes.Length);

        return SendBytes;
    }


    /// <summary>
    /// Json数据拆包
    /// </summary>
    /// <param name="dataBytes"></param>
    /// <returns></returns>
    public static MsgBase UnPackMessage(byte[] dataBytes)
    {
        Int16 bodyLength = BitConverter.ToInt16(dataBytes, 0);
        int nameCount = 0;
        string ProtoName = DecodeName(dataBytes, DataSize, out nameCount);

        if (ProtoName == "")
        {
            Console.WriteLine($"消息解析失败");
            return null;
        }

        //解析协议体   
        MsgBase msgBase = Decode(Type.GetType(ProtoName), dataBytes, nameCount + DataSize, bodyLength - nameCount);
        return msgBase;
    }


    #endregion


    #region 协议名序列化

    /// <summary>
    /// 协议名的序列化 使用Int16表示长度 (4 + 字符串)
    /// </summary>
    /// <param name="msgBase"></param>
    /// <returns></returns>
    private static byte[] EncodeName(MsgBase msgBase)
    {
        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(msgBase.protoName);
        //Int32 len = (Int32)nameBytes.Length;
        Int16 len = (Int16)nameBytes.Length;
        //申请bytes 数值
        byte[] bytes = new byte[NameSize + len];

        //组装2字节信息长度   2的8次方等于256 低位存放对应低位 高位存放对应高位 小段
        bytes[0] = (byte)(len % 256);
        bytes[1] = (byte)(len % 16777216 % 65536 / 256);
        bytes[2] = (byte)(len % 16777216 / 65536);
        bytes[3] = (byte)(len / 16777216);
        ////组装替代方法
        //byte[] length = BitConverter.GetBytes(len);

        //组装名字
        Array.Copy(nameBytes, 0, bytes, NameSize, len);
        return bytes;
    }

    /// <summary>
    /// 协议名的反序列化
    /// </summary>
    /// <param name="bs"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static string DecodeName(byte[] bs, int offset, out int count)
    {
        count = 0;
        //最小长度应该大于2
        if (offset + NameSize > bs.Length)
        {
            return "解析失败";
        }
        //读取长度
        //Int32 len = (Int16)(bs[offset + 1] << 8 | bs[offset]);
        ////读取长度替代方法
        Int32 len = BitConverter.ToInt16(bs, offset);

        if (len <= 0)
        {
            return "解析失败";
        }
        if (offset + NameSize + len > bs.Length)
        {
            return "解析失败";
        }
        //解析
        count = NameSize + len;
        string name = System.Text.Encoding.UTF8.GetString(bs, offset + NameSize, len);
        return name;
    }
    #endregion


    #region Protobuf 序列化

    /// <summary>
    /// Protobuf 序列化
    /// </summary>
    /// <param name="msgBase"></param>
    /// <returns></returns>
    private static byte[] ProEncode(MsgBase msgBase)
    {
        return ProSerialize(msgBase);
    }

    /// <summary>
    /// Protobuf反序列化
    /// </summary>
    /// <param name="name"></param>
    /// <param name="bytes"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static MsgBase ProDecode(string name, byte[] bytes, int offset, int count)
    {
        byte[] newbit = bytes.Skip(offset).Take(count).ToArray();
        return ProDeSerialize(name, newbit);
    }


    /// <summary>
    /// Protobuf数据封包
    /// </summary>
    /// <param name="msgBase"></param>
    public static byte[] ProPackageMessage(this MsgBase msgBase)
    {
        //数据编码
        byte[] nameBytes = EncodeName(msgBase);
        byte[] bodyBytes = ProEncode(msgBase);
        int len = nameBytes.Length + bodyBytes.Length;
        byte[] SendBytes = new byte[DataSize + len];

        //组装长度
        SendBytes[0] = (byte)(len % 256);
        SendBytes[1] = (byte)(len % 16777216 % 65536 / 256);
        SendBytes[2] = (byte)(len % 16777216 / 65536);
        SendBytes[3] = (byte)(len / 16777216);
        //SendBytes = BitConverter.GetBytes(len); //大小端

        //组装名字
        Array.Copy(nameBytes, 0, SendBytes, DataSize, nameBytes.Length);
        //组装消息体 
        Array.Copy(bodyBytes, 0, SendBytes, DataSize + nameBytes.Length, bodyBytes.Length);

        return SendBytes;
    }


    /// <summary>
    /// Protobuf 数据拆包
    /// </summary>
    /// <param name="dataBytes"></param>
    /// <returns></returns>
    public static MsgBase ProUnPackMessage(byte[] dataBytes)
    {
        Int16 bodyLength = BitConverter.ToInt16(dataBytes, 0);
        int nameCount = 0;
        string ProtoName = DecodeName(dataBytes, DataSize, out nameCount);
        if (ProtoName == "")
        {
            Console.WriteLine($"消息解析失败");
            return null;
        }
        //解析协议体   
        MsgBase msgBase = ProDecode(ProtoName, dataBytes, nameCount + DataSize, bodyLength - nameCount);
        return msgBase;
    }

    /// <summary>
    /// Protobuf 序列化方法
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="t"></param>
    /// <returns></returns>
    public static byte[] ProSerialize<T>(T t)
    {
        MemoryStream ms = new MemoryStream();
        Serializer.Serialize<T>(ms, t);
        return ms.ToArray();

    }

    /// <summary>
    /// Protobuf 泛型反序列化方法
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static T ProDeSerialize<T>(byte[] bytes)
    {
        MemoryStream ms = new MemoryStream(bytes);
        T t = Serializer.Deserialize<T>(ms);
        return t;
    }

    /// <summary>
    /// Protobuf 类型反序列化方法
    /// </summary>
    /// <param name="ProtoName"></param>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static MsgBase ProDeSerialize(string ProtoName, byte[] bytes)
    {
        MemoryStream ms = new MemoryStream(bytes);
        System.Type t = System.Type.GetType(ProtoName);
        return (MsgBase)ProtoBuf.Serializer.NonGeneric.Deserialize(t, ms);
    }


    #endregion



    public static bool ClientJudge(this Client client, IPEndPoint endPoint)
    {
        if (client.SenderIP.ToString() != endPoint.Address.ToString()) return false;
        if (client.SenderPort.GetHashCode() != endPoint.Port.GetHashCode()) return false;
        return true;
    }

    public static bool ClientJudge(this IPEndPoint endPoint, Client client)
    {
        if (client.SenderIP.GetHashCode() != endPoint.Address.GetHashCode()) return false;
        if (client.SenderPort.GetHashCode() != endPoint.Port.GetHashCode()) return false;
        return true;
    }

}

public class DemoClass
{
    public string protoName;
}


[ProtoContract]
[ProtoInclude(1000, typeof(MsgMove))]
[ProtoInclude(1001, typeof(MsgHeat))]
[ProtoInclude(1002, typeof(MsgCreate))]
[ProtoInclude(1003, typeof(MsgRoomCreate))]
[ProtoInclude(1004, typeof(MsgRoomJoin))]
[ProtoInclude(1005, typeof(MsgRoomGet))]
[ProtoInclude(1006, typeof(MsgRoomQuit))]
[ProtoInclude(1007, typeof(MsgRoomBeginGame))]
[ProtoInclude(1008, typeof(MsgRoomOwnerChange))]
[ProtoInclude(1009, typeof(MsgStateInfo))]
[ProtoInclude(1010, typeof(MsgRoomOverGame))]
[ProtoInclude(1011, typeof(MsgFire))]
[ProtoInclude(1012, typeof(MsgHit))]
[ProtoInclude(1013, typeof(MsgDestroy))]
[ProtoInclude(1014, typeof(MsgInstruct))]
[ProtoInclude(1015, typeof(MsgDemoFire))]
public class MsgBase
{
    /// <summary>   协议名     </summary>
    [ProtoMember(1)]
    public string protoName;

    /// <summary>   发送形式     </summary>
    [ProtoMember(2)]
    public SendType sendType = SendType.None;

    /// <summary>   发送客户端   </summary>
    [ProtoMember(3)]
    public Client SendClient;

    /// <summary>    房间名     /// </summary>
    [ProtoMember(4)]
    public float RoomID;
}

[ProtoContract]
public class MsgMove : MsgBase
{
    /// <summary>   同步基类   </summary>
    [ProtoMember(10)]
    public SyncBase translate;

    /// <summary>   同步ID   </summary>
    [ProtoMember(11)]
    public int ID;

    /// <summary>  炮塔旋转角    </summary>
    [ProtoMember(12)]
    public float TurretRotY;

    /// <summary>  炮管旋转角    </summary>
    [ProtoMember(13)]
    public float CannonRotx;


    [ProtoMember(14)]
    public float Drivertical;

    [ProtoMember(15)]
    public float Drihorizontal;



    public MsgMove(int id)
    {
        protoName = "MsgMove";
        sendType = SendType.Other;
        ID = id;
        //SendClient = GameManager.LocalClient;
        //RoomID = GameManager.RoomID;
    }

    public MsgMove() { }
}

[ProtoContract]
public class MsgHeat : MsgBase
{
    public MsgHeat()
    {
        protoName = "MsgHeat";
        sendType = SendType.Self;
    }
}

/// <summary>
/// 创建物体
/// </summary>
[ProtoContract]
public class MsgCreate : MsgBase
{
    public MsgCreate(string gameName, int id)
    {
        protoName = "MsgCreate";
        sendType = SendType.ALL;
        GameName = gameName;
        //SendClient = GameManager.LocalClient;
        ID = id;
        //RoomID = GameManager.RoomID;
    }

    /// <summary>   同步ID   </summary>
    [ProtoMember(10)]
    public int ID;

    /// <summary>   创建物体名   </summary>
    [ProtoMember(11)]
    public string GameName;

    /// <summary>   创建位置   </summary>
    [ProtoMember(12)]
    public SyncBase translate;


    public MsgCreate() { }
}

/// <summary>
/// 创建房间
/// </summary>
[ProtoContract]
public class MsgRoomCreate : MsgBase
{
    /// <summary>    最大人数   </summary>
    [ProtoMember(10)]
    public int MaxPeople;

    public MsgRoomCreate(float roomID)
    {
        protoName = "MsgRoomCreate";
        RoomID = roomID;
        MaxPeople = 5;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.Self; //创建成功后返回 信息提示创建成功
    }

    public MsgRoomCreate() { }
}

/// <summary>
/// 加入房间
/// </summary>
[ProtoContract]
public class MsgRoomJoin : MsgBase
{

    /// <summary>    当前房间信息     /// </summary>
    [ProtoMember(10)]
    public Room roomInfo;


    // 加入成功后 返回当前类显示加入成功 
    public MsgRoomJoin(float roomID)
    {
        protoName = "MsgRoomJoin";
        RoomID = roomID;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.None; //需要动态更改 如果加入失败改为Self 如果加入成功改为ALL
    }

    public MsgRoomJoin() { }
}

/// <summary>
/// 获取房间列表
/// </summary>
[ProtoContract]
public class MsgRoomGet : MsgBase
{
    /// <summary>    当前房间用户     /// </summary>
    [ProtoMember(10)]
    public List<Room> RoomItem;

    public MsgRoomGet()
    {
        protoName = "MsgRoomGet";
        sendType = SendType.Self;
    }
}

/// <summary>
/// 退出房间
/// </summary>
[ProtoContract]
public class MsgRoomQuit : MsgBase
{
    // 加入成功后 返回当前类显示加入成功 
    public MsgRoomQuit()
    {
        protoName = "MsgRoomQuit";
        //RoomID = GameManager.RoomID;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.ALL;
    }
}

/// <summary>
/// 房间开始游戏  只有房主可以下发
/// </summary>
[ProtoContract]
public class MsgRoomBeginGame : MsgBase
{
    public MsgRoomBeginGame()
    {
        protoName = "MsgRoomBeginGame";
        //RoomID = GameManager.RoomID;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.ALL;
    }
}


/// <summary>
/// 切换房主  只有房主可以下发
/// </summary>
[ProtoContract]
public class MsgRoomOwnerChange : MsgBase
{

    /// <summary>    新房主     /// </summary>
    [ProtoMember(11)]
    public Client NewRoomOwner;

    public MsgRoomOwnerChange(float roomID)
    {
        protoName = "MsgRoomOwnerChange";
        RoomID = roomID;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.ALL;
    }

    public MsgRoomOwnerChange() { }
}


/// <summary>
/// 发送准备信息
/// </summary>
[ProtoContract]
public class MsgStateInfo : MsgBase
{

    /// <summary>    准备状态     /// </summary>
    [ProtoMember(11)]
    public bool ReadState;

    public MsgStateInfo(bool readState)
    {
        protoName = "MsgStateInfo";
        // RoomID = GameManager.RoomID;
        ReadState = readState;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.ALL;
    }

    public MsgStateInfo() { }
}

/// <summary>
/// 游戏结束
/// </summary>
[ProtoContract]
public class MsgRoomOverGame : MsgBase
{
    public MsgRoomOverGame()
    {
        protoName = "MsgRoomOverGame";
        //RoomID = GameManager.RoomID;
        //SendClient = GameManager.LocalClient;
        sendType = SendType.ALL;
    }

}


/// <summary>
/// 开火
/// </summary>
[ProtoContract]
public class MsgFire : MsgBase
{
    /// <summary>   同步基类   </summary>
    [ProtoMember(10)]
    public SyncBase translate;

    /// <summary>   子弹种类   </summary>
    [ProtoMember(11)]
    public string Bullet;


    public MsgFire(string bullet)
    {
        protoName = "MsgFire";
        sendType = SendType.ALL;
        Bullet = bullet;
        //SendClient = GameManager.LocalClient;
        //RoomID = GameManager.RoomID;
    }

    public MsgFire() { }
}

/// <summary>
/// 被击中
/// </summary>
[ProtoContract]
public class MsgHit : MsgBase
{
    /// <summary>   同步ID   </summary>
    [ProtoMember(10)]
    public int ID;

    /// <summary>   受到伤害   </summary>
    [ProtoMember(11)]
    public float Damage;

    /// <summary>   受损部位   </summary>
    [ProtoMember(12)]
    public float translate;

    public MsgHit(int id)
    {
        ID = id;
        protoName = "MsgFire";
        sendType = SendType.ALL;
        //SendClient = GameManager.LocalClient;
        //RoomID = GameManager.RoomID;
    }

    public MsgHit() { }
}

/// <summary>
/// 销毁物体
/// </summary>
[ProtoContract]
public class MsgDestroy : MsgBase
{
    /// <summary>   同步ID   </summary>
    [ProtoMember(10)]
    public int ID;

    public MsgDestroy(int id)
    {
        id = ID;
        protoName = "MsgDestroy";
        sendType = SendType.ALL;
        //SendClient = GameManager.LocalClient;
        //RoomID = GameManager.RoomID;
    }

    public MsgDestroy() { }
}

/// <summary>
/// 传输指令
/// </summary>
[ProtoContract]
public class MsgInstruct : MsgBase
{
    /// <summary>   同步ID   </summary>
    [ProtoMember(10)]
    public int ID;

    [ProtoMember(11)]
    public float Drivertical;

    [ProtoMember(12)]
    public float Drihorizontal;

    [ProtoMember(13)]
    public float Aimvertical;

    [ProtoMember(14)]
    public float Aimhorizontal;

    public MsgInstruct(int id)
    {
        id = ID;
        protoName = "MsgInstruct";
        sendType = SendType.Other;
        //SendClient = GameManager.LocalClient;
       // RoomID = GameManager.RoomID;
    }
}

/// <summary>
/// 开火
/// </summary>
[ProtoContract]
public class MsgDemoFire : MsgBase
{
    /// <summary>   同步ID   </summary>
    [ProtoMember(10)]
    public int ID;

    public MsgDemoFire(int id)
    {
        protoName = "MsgDemoFire";
        ID = id;
        sendType = SendType.ALL;
        //SendClient = GameManager.LocalClient;
        //RoomID = GameManager.RoomID;
    }

    public MsgDemoFire() { }
}


/// <summary>
/// 同步基类
/// </summary>
[ProtoContract]
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]//设定对齐的粒度为一个字节
public struct SyncBase
{
    [ProtoMember(1)]
    public float Pos_X;
    [ProtoMember(2)]
    public float Pos_Y;
    [ProtoMember(3)]
    public float Pos_Z;

    [ProtoMember(4)]
    public float Rot_X;
    [ProtoMember(5)]
    public float Rot_Y;
    [ProtoMember(6)]
    public float Rot_Z;
}


[ProtoContract]
[System.Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]//设定对齐的粒度为一个字节
public struct Client
{
    [ProtoMember(1)]
    public string SenderIP;
    [ProtoMember(2)]
    public int SenderPort;

    public Client(string senderIP, int senderPort)
    {
        SenderIP = senderIP;
        SenderPort = senderPort;
    }

    public override string ToString()
    {
        return SenderIP + ":" + SenderPort;
    }

    public static bool operator ==(Client x, Client y)
    {
        if (x.SenderIP != y.SenderIP) return false;
        if (x.SenderPort != y.SenderPort) return false;
        return true;
    }

    public static bool operator !=(Client x, Client y)
    {
        if (x.SenderIP != y.SenderIP) return true;
        if (x.SenderPort != y.SenderPort) return true;
        return false;
    }

    public EndPoint GetEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(SenderIP), SenderPort);
    }
}


[ProtoContract]
public enum SendType
{
    [ProtoMember(1)]
    None,      //不发送
    [ProtoMember(2)]
    ALL,       //房间 广播 
    [ProtoMember(3)]
    Self,      //自己
    [ProtoMember(4)]
    Other,     //除了自己之外

    [ProtoMember(5)]
    Specific,  //特播 几乎用不上
}