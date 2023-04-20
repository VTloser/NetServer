using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;


namespace Client
{
    public partial class CCLient 
    {


        bool flag = true;//定义一个bool变量，标识是否接收数据
        Thread thread;//创建线程对象
        UdpClient udp;

        static void Main(string[] args)
        {
            CCLient cC = new CCLient();
            cC.button1_Click();
        }

        private void button1_Click()
        {
            udp = new UdpClient(7788);//使用端口号实例化UDP连接对象  7788是源端口
            udp.JoinMulticastGroup(IPAddress.Parse("224.0.0.0"));
            IPEndPoint ipendpoint = new IPEndPoint(IPAddress.Parse("224.0.0.0"), 5566);  //5566是目的端口
            flag = true;//标识接收数据

            //创建IPEndPoint对象，用来显示响应主机的标识
            //新开线程，执行接收数据操作
            thread = new Thread(() =>
            {
                while (flag)//如果标识为true
                {
                    try
                    {

                        if (udp.Available <= 0) continue;//判断是否有网络数据
                        if (udp.Client == null) return;//判断连接是否为空
                        //调用UdpClient对象的Receive方法获得从远程主机返回的UDP数据报
                        byte[] bytes = udp.Receive(ref ipendpoint);
                        //将获得的UDP数据报转换为字符串形式
                        string str = Encoding.Default.GetString(bytes);
                        Console.WriteLine("正在接收的信息：\n" + str);//显示正在接收的数据
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);//错误提示
                    }
                    Thread.Sleep(2000);//线程休眠2秒
                }
            }
            );
            thread.Start();//启动线程
        }



        private void button2_Click(object sender, EventArgs e)
        {

            flag = false;//不接收数据

            if (thread.ThreadState == ThreadState.Running)//判断线程是否运行
                thread.Abort();//终止线程

            //该错误是由于没有找到udp对象造成，请看第27行代码，第27行代码中创建了一个udp对象
            //但该对象是一个局部对象，因此，在button2的Click事件中无法访问到
            //要改正该程序，可以将udp对象声明为全局对象，即在Form1类的内部创建该对象
            udp.Close();//关闭连接

        }

    }

}