using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    class Program
    {
        //创建UdpClient对象
        static UdpClient udp = new UdpClient(5566);//要通过其进行通信的本地端口号。  5566是源端口

        static void Main(string[] args)
        {

            udp.JoinMulticastGroup(IPAddress.Parse("224.0.0.0"));//将 UdpClient 添加到多播组;IPAddress.Parse将IP地址字符串转换为IPAddress 实例
            IPEndPoint multicast = new IPEndPoint(IPAddress.Parse("224.0.0.0"), 7788); //将网络终结点表示为 IP 地址和端口号  7788是目的端口
            while (true)
            {
                Thread thread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            //定义一个字节数组，用来存放发送到远程主机的信息
                            Byte[] sendBytes = Encoding.Default.GetBytes("(" + DateTime.Now.ToLongTimeString() + ")节目预报：八点有大型晚会，请收听");
                            Console.WriteLine("(" + DateTime.Now.ToLongTimeString() + ")节目预报：八点有大型晚会，请收听");
                            //调用UdpClient对象的Send方法将UDP数据报发送到远程主机
                            udp.Send(sendBytes, sendBytes.Length, multicast);//将UDP数据报发送到位于指定远程终结点的主机
                            Thread.Sleep(2000);//线程休眠2秒
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                });
                thread.Start();//启动线程

            }

        }

    }

}