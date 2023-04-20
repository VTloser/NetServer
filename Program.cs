using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading;
using Newtonsoft.Json;

class MainClass
{
    public static string IP = "192.168.10.103";
    public static int Port = 10011;

    public bool EnableHeatDetec = true;

    /// <summary>    服务器房间信息     /// </summary>
    public List<Room> Rooms = new List<Room>();

    public static void Main(string[] args) 
    {
        MainClass mainClass = new MainClass();

        //mainClass.StratTcpServ(IP, Port);

        mainClass.StartUdpServ();

        //IPAddress ip = IPAddress.Parse(IP);
        //IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
        MsgMove temp = new MsgMove(10);
        temp.translate.Pos_X = 175;
        temp.translate.Pos_Y = 185;
        temp.translate.Pos_Z = 195;
        temp.protoName = "MsgMove";

        string s = JsonConvert.SerializeObject(temp);

        //byte[] SendBytes = new byte[4 + 20];

        var t = temp.ProPackageMessage();
        var tt = ProtocalTool.ProUnPackMessage(t);

        Console.WriteLine((tt as MsgMove).protoName);
        Console.ReadLine();
    }

    #region Tcp


    Dictionary<Socket, ClientState> Clients = new Dictionary<Socket, ClientState>();

    /// <summary>
    /// 开启服务器
    /// </summary>
    /// <param name="IP"></param>
    /// <param name="Port"></param>
    public void StratTcpServ(string IP, int Port)
    {
        Socket listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint Ipend = new IPEndPoint(IPAddress.Parse(IP), Port);
        listenfd.Bind(Ipend);
        listenfd.Listen(0);
        listenfd.BeginAccept(AcceptCB, listenfd);
        Log($"服务器开启于【{DateTime.Now}】");
    }

    /// <summary>
    /// 服务器接收回调
    /// </summary>
    /// <param name="ar"></param>
    private void AcceptCB(IAsyncResult ar)
    {
        try
        {
            //因为前面传进来的参数是listenfd ，所以接收的还是listenfd
            Socket listenfd = ar.AsyncState as Socket;
            Socket clintlistenfd = listenfd.EndAccept(ar);

            ClientState clientState = new ClientState(clintlistenfd);
            Clients.Add(clintlistenfd, clientState);

            Log($"【客户端{clientState.socket.RemoteEndPoint}】加入链接");

            //接收消息，传递的类包含ReadBuff
            clintlistenfd.BeginReceive(clientState.ReadBuffer, 0, clientState.ReadBuffer.Length, SocketFlags.None, ReceiveCB, clientState);

            //继续接收 否则只能接收一个
            listenfd.BeginAccept(AcceptCB, listenfd);
        }
        catch (Exception e)
        {
            Log(e.ToString());
        }
    }

    /// <summary>
    /// 异步接收来自套接字的数据
    /// </summary>
    /// <param name="ar"></param>
    private void ReceiveCB(IAsyncResult ar)
    {
        try
        {
            //因为前面传进来的参数是clientstate ，所以接收的还是clientstate
            ClientState clientstate = ar.AsyncState as ClientState;
            int count = clientstate.socket.EndReceive(ar);
            if (count < 0)
            {
                Log($"【客户端{clientstate.socket.RemoteEndPoint}】断开连接");
                clientstate.socket.Shutdown(SocketShutdown.Both);
                clientstate.socket.Close();
                Clients.Remove(clientstate.socket);
                return;
            }
            string str = Encoding.UTF8.GetString(clientstate.ReadBuffer, 0, count);
            Log($"接收到来自【{clientstate.socket.RemoteEndPoint}】的消息【{str}】");

            BroadCaste(clientstate.ReadBuffer);

            clientstate.socket.BeginReceive(clientstate.ReadBuffer, 0, clientstate.ReadBuffer.Length, SocketFlags.None, ReceiveCB, clientstate);

        }
        catch (Exception e)
        {
            Log(e.ToString());
        }
    }


    private void BroadCaste(string str)
    {
        //str = conn.GetAdress() + str;
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        //消息广播
        foreach (var item in Clients.Keys)
        {
            item.Send(bytes, bytes.Length, SocketFlags.None);
            Log($"服务器向[{item.RemoteEndPoint}]广播消息");
        }
    }

    private void BroadCaste(byte[] bytes)
    {
        //消息广播
        foreach (var item in Clients.Keys)
        {
            item.Send(bytes, bytes.Length, SocketFlags.None);
            Log($"服务器向[{item.RemoteEndPoint}]广播消息");
        }
    }



    #region Function
    public void Log<T>(T str)
    {
        Console.WriteLine(str);
    }
    class ClientState
    {
        public Socket socket;
        public byte[] ReadBuffer;
        public ClientState(Socket socket)
        {
            this.socket = socket;
            ReadBuffer = new byte[1024];
        }
    }

    #endregion
    #endregion

    #region UDP
    Socket UdpServer;
    private void StartUdpServ()
    {
        UdpServer = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPAddress ip = IPAddress.Parse(IP);
        IPEndPoint endPoint = new IPEndPoint(ip, Port);
        UdpServer.Bind(endPoint);


        Thread t = new Thread(ReciveMsg);
        t.IsBackground = true;
        t.Start();
        Log($"UDP Server，服务器启动端口：[{Port}]，服务器启动时间：[{DateTime.Now}]");

        Thread s = new Thread(SendMsg);
        s.IsBackground = true;
        s.Start();


        if (EnableHeatDetec)
        {
            Thread c = new Thread(checkedHear);
            c.IsBackground = true;
            c.Start();
        }

        Thread Handle = new Thread(HandleEvent);
        Handle.IsBackground = true;
        Handle.Start();

    }

    /// <summary>
    /// 接收处理事件
    /// </summary>
    private void HandleEvent()
    {
        while (true)
        {
            if (ReceiveQueue.Count > 0)
            {
                EndPoint endPoint;
                MsgBase msgBase = Judge(out endPoint);
                Console.WriteLine(msgBase.protoName);
                if (msgBase != null)
                {
                    if (msgBase is MsgHeat)
                    {
                        if (Clints.Contains(new ClientInfo(endPoint)))
                        {
                            var endpoint = Clints.Find((X) => X.point.GetHashCode() == endPoint.GetHashCode());
                            endpoint.Heartbeat = DateTime.Now.Ticks;
                        }
                        else
                        {
                            ClientInfo sendInfo = new ClientInfo(endPoint);
                            sendInfo.Heartbeat = DateTime.Now.Ticks;
                            Clints.Add(sendInfo);

                            Log(sendInfo.point + "上线了");
                        }
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint)); //心跳返回
                    }
                    else if (msgBase is MsgRoomCreate) //创建房间
                    {
                        MsgRoomCreate msg = msgBase as MsgRoomCreate;

                        if (msg.RoomID == -1 || Rooms.Contains(new Room(msg.RoomID))) //如果已经有了创建失败
                        {
                            msg.RoomID = -1;
                        }
                        else //创建成功
                        {
                            Room room = new Room(msg.RoomID);
                            room.PlayerItem.Add(new Player(msg.SendClient));
                            room.MaxPeople = msg.MaxPeople;
                            room.RoomOwner = msg.SendClient;
                            Rooms.Add(room);
                        }

                        SendQueue.Enqueue(new SendInfo(msg, endPoint));
                    }
                    else if (msgBase is MsgRoomJoin) //加入房间
                    {
                        MsgRoomJoin msg = msgBase as MsgRoomJoin;
                        if (Rooms.Contains(new Room(msg.RoomID)))
                        {
                            Room room = Rooms.Find((x) => x.RoomID == msg.RoomID);
                            if (room.PlayerItem.Count < room.MaxPeople) //房间未满员
                            {
                                room.PlayerItem.Add(new Player(msg.SendClient));
                                msg.roomInfo = room;
                                msg.sendType = SendType.ALL;
                            }
                            else
                            {
                                msg.RoomID = -1;
                                msg.sendType = SendType.Self;
                            }
                        }
                        else
                        {
                            msg.RoomID = -1;
                            msg.sendType = SendType.Self;
                        }
                        SendQueue.Enqueue(new SendInfo(msg, endPoint));
                    }
                    else if (msgBase is MsgRoomGet) //获取房间列表
                    {
                        MsgRoomGet msg = msgBase as MsgRoomGet;
                        msg.RoomItem = Rooms;
                        SendQueue.Enqueue(new SendInfo(msg, endPoint));
                    }
                    else if (msgBase is MsgRoomQuit) //退出房间
                    {
                        MsgRoomQuit msg = msgBase as MsgRoomQuit;
                        Room room = Rooms.Find((x) => x.RoomID == msg.RoomID);
                        var client = room.PlayerItem.Find((X) => X.client == msg.SendClient);
                        room.PlayerItem.Remove(client);
                        if (room.PlayerItem.Count <= 0) //房间没人了
                        {
                            Rooms.Remove(room);
                            msg.sendType = SendType.Self;
                        }
                        SendQueue.Enqueue(new SendInfo(msg, endPoint));
                    }
                    else if (msgBase is MsgRoomBeginGame) //开始游戏
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else if (msgBase is MsgRoomOwnerChange)//切换房主
                    {
                        //暂时不考虑
                    }
                    else if (msgBase is MsgStateInfo)//准备状态改变
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else if (msgBase is MsgRoomOverGame)
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else if (msgBase is MsgFire)
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else if (msgBase is MsgHit)
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else if (msgBase is MsgDestroy)
                    {
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                    else
                    {
                        Console.WriteLine("接受到协议：" + msgBase.protoName);
                        SendQueue.Enqueue(new SendInfo(msgBase, endPoint));
                    }
                }
            }
        }
    }

    public MsgBase Judge(out EndPoint endPoint)
    {
        endPoint = null;
        if (ReceiveQueue.Count > 0)
        {
            ReceEvent temp = ReceiveQueue.Dequeue();
            endPoint = temp.SendClient;
            MsgBase msgBase = null;
            try
            {
                //拆包体
                //msgBase = ProtocalTool.UnPackMessage(temp.buff);
                msgBase = ProtocalTool.ProUnPackMessage(temp.buff);
            }
            catch { };
            if (msgBase != null)
            {
                if (msgBase is MsgHeat) return msgBase as MsgHeat;
                if (msgBase is MsgRoomCreate) return msgBase as MsgRoomCreate;
                return msgBase as MsgBase;
            }
            return null;
        }
        return null;
    }


    private void checkedHear()
    {
        while (true)
        {
            for (int i = 0; i < Clints.Count; i++)
            {
                //Console.WriteLine(DateTime.Now.Ticks - Clints[i].Heartbeat);
                //Console.WriteLine(DateTime.Now.Ticks - Clints[i].Heartbeat > 10 * 10000000);
                if (DateTime.Now.Ticks - Clints[i].Heartbeat > 10 * 10000000)
                {
                    Log(Clints[i].point + "心跳失败 断开连接");
                    Clints.Remove(Clints[i]);
                }
            }
            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// 接收到数据
    /// </summary>
    /// <param name="obj"></param>
    private void ReciveMsg()
    {
        while (true)
        {
            //用来保存发送发IP
            EndPoint point = new IPEndPoint(IPAddress.Any, 0);
            byte[] buff = new byte[1024 * 1024];
            int length = UdpServer.ReceiveFrom(buff, ref point);

            byte[] newbyte = new byte[length];
            Array.Copy(buff, 0, newbyte, 0, length);

            //byte[] pointLength = System.Text.Encoding.ASCII.GetBytes(point.ToString()+":");
            //newbyte = pointLength.Concat(newbyte).ToArray();

            //string msg = Encoding.UTF8.GetString(buff, 0, length);

            Log($"接收到来自:{point.ToString()} 的消息 第i个 {Receive_i++} ");

            ReceiveQueue.Enqueue(new ReceEvent(newbyte, point));
        }
    }


    int Receive_i = 0;

    Queue<ReceEvent> ReceiveQueue = new Queue<ReceEvent>();
    Queue<SendInfo> SendQueue = new Queue<SendInfo>();
    List<ClientInfo> Clints = new List<ClientInfo>();


    int Sendcount = 0;
    /// <summary>
    /// 发送数据
    /// </summary>
    private void SendMsg()
    {
        while (true)
        {
            for (int j = 0; j < 10; j++)
            {
                if (SendQueue.Count > 0)
                {
                    SendInfo temp = SendQueue.Dequeue();
                    if (temp != null)
                    {
                        Room room = Rooms.Find(x => (x.RoomID) == temp.SendMsg.RoomID);
                        switch (temp.SendMsg.sendType)
                        {
                            case SendType.None:
                                break;
                            case SendType.ALL:
                                for (int i = 0; i < room.PlayerItem.Count; i++)
                                {
                                    Console.WriteLine($"ALL：{room.PlayerItem[i].client.GetEndPoint()}");
                                    SendMsg(temp.SendMsg.ProPackageMessage(), room.PlayerItem[i].client.GetEndPoint());
                                }
                                break;
                            case SendType.Self:
                                Console.WriteLine($"Self [{temp.SendClient}]");
                                SendMsg(temp.SendMsg.ProPackageMessage(), temp.SendClient);
                                break;
                            case SendType.Other:
                                for (int i = 0; i < room.PlayerItem.Count; i++)
                                {
                                    if (temp.SendClient.GetHashCode() == room.PlayerItem[i].client.GetEndPoint().GetHashCode())
                                    {
                                        Console.WriteLine(temp.SendClient);
                                        Console.WriteLine(Clints[i].GetHashCode());
                                        continue;
                                    }
                                    SendMsg(temp.SendMsg.ProPackageMessage(), room.PlayerItem[i].client.GetEndPoint());
                                    Console.WriteLine($"Other事件");
                                    Console.WriteLine(temp.SendClient);
                                    Console.WriteLine(Clints[i].point);
                                }
                                break;
                            case SendType.Specific:
                                break;
                            default:
                                break;
                        }
                        Sendcount++;
                        //Console.WriteLine($"处理事件{Sendcount}");
                    }
                }

            }
        }
    }

    private void SendMsg(byte[] bytes, EndPoint point)
    {
        if (bytes.Length <= 0)
            return;
        UdpServer.SendTo(bytes, point);
    }

    #endregion
}

class ClientState
{
    public Socket socket;
    public byte[] ReadBuffer;
    public ClientState(Socket socket)
    {
        this.socket = socket;
        ReadBuffer = new byte[1024];
    }
}

public class ClientInfo
{
    public EndPoint point;

    public long Heartbeat;
    public ClientInfo(EndPoint point)
    {
        this.point = point;
    }

    public override bool Equals(object? obj)
    {
        if (!(obj is ClientInfo)) return false;

        if (((ClientInfo)obj).point.GetHashCode() == point.GetHashCode())
            return true;
        else
            return false;
    }
}

public class SendInfo
{
    public MsgBase SendMsg;
    public EndPoint SendClient;

    public SendInfo(MsgBase sendMsg, EndPoint sendClient)
    {
        SendMsg = sendMsg;
        SendClient = sendClient;
    }
}

public class ReceEvent
{
    public byte[] buff;
    public EndPoint SendClient;

    public ReceEvent(byte[] buff, EndPoint sendClient)
    {
        this.buff = buff;
        SendClient = sendClient;
    }
}


