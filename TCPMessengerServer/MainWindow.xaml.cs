using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TCPMessengerServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Hashtable to store sockets and info about client
        public static Hashtable clientsList = new Hashtable();

        //Boolean value to stop looping to listen for messages from clients
        bool interrupt = false;

        //Function called on start of program to initialize xaml, click listeners, and start the server
        public MainWindow()
        {
            //Initialize main window
            InitializeComponent();

            //Set click listeners
            Send.Click += new RoutedEventHandler(SendMessage);
            End.Click += new RoutedEventHandler(EndServer);
            
            //Start the server on another thread
            ThreadStart start = delegate () {
                StartServer();
            };
            new Thread(start).Start();
        }

        //Function called from initializing function
        //Runs on seperate thread
        public void StartServer()
        {
            //Create socket connections to listen on for clients
            TcpListener serverSocket = new TcpListener((GetLocalIPAddress()), 8000);
            TcpClient clientSocket = default(TcpClient);

            //Counter to keep track of number of clients
            int counter = 0;

            serverSocket.Start();

            //Use a threadsafe call to update text display
            this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
            {
                Messages.Text += "Chat Server Started ....\n";
                Messages.Text += "Server IP: " + GetLocalIPAddress() + ", Port: 8000\n";

            }));

            //Loop to receive messages from clients while the program has not ended
            while (!interrupt)
            {
                //Increment number of clients
                counter += 1;

                //Accept connection from new client
                clientSocket = serverSocket.AcceptTcpClient();

                //Receive inital message from client that contains their public ip address
                byte[] bytesFrom = new byte[10025];
                string dataFromClient = null;

                NetworkStream networkStream = clientSocket.GetStream();
                networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));

                //Add new client to hashtable
                clientsList.Add(dataFromClient, clientSocket);

                //Announce that a new client has entered the chat
                Broadcast(dataFromClient + " Joined ", dataFromClient, false);

                //Use threadsafe call to update text display
                this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    Messages.Text += dataFromClient + " Joined chat room \n";

                }));

                //Create new instance of client
                HandleClient client = new HandleClient(Messages);
                client.StartClient(clientSocket, dataFromClient, clientsList);
            }

            //Close and dispose of remaining sockets
            clientSocket.Close();
            serverSocket.Stop();
        }

        //Function triggered when user clicks on the sendmessage button
        public void SendMessage(Object sender, EventArgs e)
        {
            //Gets the local ip to identify the server
            //Might change this to public ip later
            var ip = GetLocalIPAddress().ToString();
            Broadcast(Message.Text, ip, false);
            Messages.Text += ip + ": " + Message.Text + "\n";
            Message.Text = "";
        }

        //Function called when user elicks end button
        public void EndServer(Object sender, EventArgs e)
        {
            interrupt = true;
        }

        //Function to send message to all clients
        public static void Broadcast(string msg, string uName, bool flag)
        {
            //Loop through each item in the hastable of clients
            foreach (DictionaryEntry Item in clientsList)
            {
                //Use sockets contained in client hashtable to communicate with all clients
                TcpClient broadcastSocket;
                broadcastSocket = (TcpClient)Item.Value;
                NetworkStream broadcastStream = broadcastSocket.GetStream();
                Byte[] broadcastBytes = null;

                //Check to see if message is being broadcast by the server or is being repeated from a client
                if (flag == true)
                {
                    broadcastBytes = Encoding.ASCII.GetBytes(uName + " says : " + msg + "$");
                }
                else
                {
                    broadcastBytes = Encoding.ASCII.GetBytes("Server: " + msg + "$");
                }

                //Send the message
                broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                broadcastStream.Flush();
            }
        }

        //Function to find the local ip address
        //Might change to be the public ip address like client later
        public static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }

    //Class to handle clients
    public class HandleClient
    {
        private delegate void SafeCallDelegate(string text);
        TcpClient clientSocket;
        string clNo;
        Hashtable clientList = new Hashtable();
        TextBlock Messages;

        public HandleClient(TextBlock Message)
        {
            Messages = Message;
        }

        //Function to write text to text display safely
        private void WriteTextSafe(string text)
        {
            if (!Messages.CheckAccess())
            {
                Messages.Dispatcher.Invoke(new SafeCallDelegate(WriteTextSafe), text);
            }
            else
            {
                Messages.Text += text;
            }
        }

        //Function that initializes the client
        public void StartClient(TcpClient inClientSocket, string clineNo, Hashtable cList)
        {
            this.clientList = cList;
            this.clientSocket = inClientSocket;
            this.clNo = clineNo;
            Thread ctThread = new Thread(Chat);
            ctThread.Start();
        }

        //Function called upon initialization of the client
        //operates in different thread
        //Receives and repeats message from one client to all other clients
        private void Chat()
        {
            int requestCount = 0;
            byte[] bytesFrom = new byte[10025];
            string dataFromClient = null;
            string rCount = null;
            requestCount = 0;

            while (true)
            {
                try
                {
                    requestCount = requestCount + 1;
                    NetworkStream networkStream = clientSocket.GetStream();
                    networkStream.Read(bytesFrom, 0, bytesFrom.Length);
                    dataFromClient = System.Text.Encoding.ASCII.GetString(bytesFrom);
                    dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                    WriteTextSafe("From client - " + clNo + " : " + dataFromClient + "\n");
                    rCount = Convert.ToString(requestCount);

                    MainWindow.Broadcast(dataFromClient, clNo, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
