using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace TCPMessengerServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool interrupt = false;
        System.Net.Sockets.TcpClient clientSocket = new System.Net.Sockets.TcpClient();
        NetworkStream serverStream = default(NetworkStream);

        public MainWindow()
        {
            InitializeComponent();
            Connect.Click += new RoutedEventHandler(StartClient);
            Send.Click += new RoutedEventHandler(SendMessage);
            End.Click += new RoutedEventHandler(EndClient);
        }

        //Function that starts connection to server
        //Uses ip address in the ip text box
        public void StartClient(Object sender, EventArgs e)
        {
            //Connect to server
            Messages.Text += "Chat Client Started ....\n";
            clientSocket.Connect(Ip.Text, 8000);
            serverStream = clientSocket.GetStream();

            //Send identifying message to server
            byte[] outStream = System.Text.Encoding.ASCII.GetBytes(GetLocalIPAddress() + "$");
            serverStream.Write(outStream, 0, outStream.Length);
            serverStream.Flush();

            //Listen to messages from server on different thread
            ThreadStart listen = delegate ()
            {
                
                while (!interrupt)
                {    
                    byte[] inStream = new byte[10025];
                    serverStream.Read(inStream, 0, inStream.Length);
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                    {
                        Console.WriteLine(System.Text.Encoding.ASCII.GetString(inStream));
                        Messages.Text += Encoding.ASCII.GetString(inStream).Substring(0, Encoding.ASCII.GetString(inStream).IndexOf("$")) + "\n";
                    }));
                    Thread.Sleep(1000);
                }
                clientSocket.Close();
                Console.WriteLine("exit");
                Console.ReadLine();
            };
            new Thread(listen).Start();
        }

        public void EndClient(object sender, EventArgs e)
        {
            interrupt = true;
        }

        //Function called when send message button is clicked
        //Sends message from textblock to server
        private void SendMessage(object sender, EventArgs e)
        {
            byte[] message = System.Text.Encoding.ASCII.GetBytes(Message.Text + "$");
            serverStream.Write(message, 0, message.Length);
            Message.Text = "";
            serverStream.Flush();
        }

        //Function to find public ip
        //Will fail if unable to connect to dyndns.org
        public static string GetLocalIPAddress()
        {
            String address = "";
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                address = stream.ReadToEnd();
            }

            int first = address.IndexOf("Address: ") + 9;
            int last = address.LastIndexOf("</body>");
            address = address.Substring(first, last - first);

            return address;
        }
    }

}
