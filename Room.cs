using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

[ProtoContract]
public class Room
{
    /// <summary>     房间ID    /// </summary>
    [ProtoMember(10)]
    public float RoomID;

    /// <summary>     房间最大人数    /// </summary>
    [ProtoMember(11)]
    public int MaxPeople;

    /// <summary>     房间拥有者    /// </summary>
    [ProtoMember(12)]
    public Client RoomOwner;

    /// <summary>     房间玩家信息    /// </summary>
    [ProtoMember(13)]
    public List<Player> PlayerItem = new List<Player>();

    public Room(float roomID)
    {
        RoomID = roomID;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Room)) return false;
        if ((obj as Room).RoomID == RoomID) return true;
        return false;
    }
    public Room() { }

}


[ProtoContract]
public class Player
{
    [ProtoMember(10)]
    public Client client;

    [ProtoMember(11)]
    public bool ReadyState;

    public Player(Client client)
    {
        this.client = client;
        ReadyState = false;
    }

    public Player() { }

    public override bool Equals(object obj)
    {
        if (!(obj is Player)) return false;
        if (((Player)obj).client == client) return true;
        return false;
    }
}
