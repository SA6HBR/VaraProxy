using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VaraProxy
{
    class Program
    {
        private static TcpListener[] listener = new TcpListener[100];
        private static TcpClient[] client = new TcpClient[100];
        private static Thread[] listenThreadClient = new Thread[100];
        private static Thread[] listenThreadServer = new Thread[100];

        static void Main()
        {


            int ports = 0;

            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string strWorkPath = System.IO.Path.GetDirectoryName(strExeFilePath);
            string strSettingsFile = System.IO.Path.Combine(strWorkPath, "VaraProxy.ini");

            using (var reader = new StreamReader(strSettingsFile))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');

                    int serverGroup = int.Parse(values[0]);
                    string serverGroupName = values[1];
                    int portServer = int.Parse(values[2]);
                    int portClient = int.Parse(values[3]);

                    if (portServer >= 1000 && portServer <= 65000 && portClient >= 1000 && portClient <= 65000)
                    {
                        ports += 1;

                        variables.serverGroup[ports] = serverGroup;
                        variables.serverGroupName[ports] = serverGroupName;
                        variables.serverPort[ports] = portServer;
                        variables.clientPort[ports] = portClient;
                    }

                }
            }


            for (int i = 1; i < ports + 1; i++)
            {
                variables.proxyNo = i;

                listener[variables.proxyNo] = new TcpListener(IPAddress.Any, variables.clientPort[variables.proxyNo]);
                listener[variables.proxyNo].Start();
                Console.WriteLine("{0} Server {1}   Started. Waiting for connections {2} <=> {3}", DateTime.Now.ToString("HH:mm:ss.ffffff"), variables.proxyNo, variables.serverPort[variables.proxyNo], variables.clientPort[variables.proxyNo]);

                listenThreadServer[variables.proxyNo] = new Thread(new ThreadStart(ListenForServer));
                listenThreadServer[variables.proxyNo].Start();

                listenThreadClient[variables.proxyNo] = new Thread(new ThreadStart(startClients));
                listenThreadClient[variables.proxyNo].Start();
                Thread.Sleep(1000);
            }
        }


        static class variables
        {
            private const int ports = 100;
            private const int bytes = 4096;
            public static int sleepTime = 10;
            public static int proxyNo = 0;

            public static int[] serverGroup = new int[ports];
            public static string[] serverGroupName = new string[ports];
            public static DateTime[] watchDogTimer = new DateTime[ports];

            public static int[] clientNo = new int[ports];
            public static int[] clientPort = new int[ports];
            public static int[] serverPort = new int[ports];
            public static string[] clientTime = new string[ports];
            public static string[] serverTime = new string[ports];
            public static int[] clientBytesRead = new int[ports];
            public static int[] serverBytesRead = new int[ports];

            public static byte[,] clientByte = new byte[ports, bytes];
            public static byte[,] serverByte = new byte[ports, bytes];

        }

        public static void setClientByte(byte[] srcArray, int row, int bytes)
        {

            int i = 0;

            for (i = 0; i < bytes; i++)
            {
                variables.clientByte[row, i] = srcArray[i];
            }

        }
        public static void setServerByte(byte[] srcArray, int row, int bytes)
        {

            int i = 0;

            for (i = 0; i < bytes; i++)
            {
                variables.serverByte[row, i] = srcArray[i];
            }

        }
        public static byte[] getClientByte(int row, int bytes)
        {

            byte[] dstArray = new byte[4049];
            int i = 0;

            for (i = 0; i < bytes; i++)
            {
                dstArray[i] = variables.clientByte[row, i];
            }

            return dstArray;
        }
        public static byte[] getServerByte(int row, int bytes)
        {

            byte[] dstArray = new byte[4049];
            int i = 0;

            for (i = 0; i < bytes; i++)
            {
                dstArray[i] = variables.serverByte[row, i];
            }

            return dstArray;
        }


        private static void startClients()
        {
            int clientNo = variables.proxyNo;
            while (true)
            {
                client[clientNo] = listener[clientNo].AcceptTcpClient();
                Console.WriteLine("{0} Client {1}   Connected {2} <=> {3}", DateTime.Now.ToString("HH:mm:ss.ffffff"), clientNo, client[clientNo].Client.LocalEndPoint, client[clientNo].Client.RemoteEndPoint);

                // clientThread = new Thread(new ParameterizedThreadStart(ListenForClients(client[clintNo], clintNo)));
                Thread clientThread = new Thread(unused => ListenForClients(client[clientNo], clientNo));
                clientThread.Start(client[clientNo]);
            }
        }
        private static void ListenForClients(object client, int clintNo)
        {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();

            variables.clientNo[clintNo] += 1;
            int thisClientNo = variables.clientNo[clintNo];
            Console.WriteLine("{0} Client {1}:{2} Listen", DateTime.Now.ToString("HH:mm:ss.ffffff"), clintNo, thisClientNo);

            string lastString = "";

            while (tcpClient.Connected)
            {
                int bytesRead;
                byte[] buffer = new byte[4096];

                if (lastString != variables.serverTime[clintNo] && variables.serverBytesRead[clintNo] != 0)
                {
                    byte[] serverBuffer = new byte[4096];
                    serverBuffer = getServerByte(clintNo, variables.serverBytesRead[clintNo]);

                    string replaceWith = "";
                    string getString = Encoding.ASCII.GetString(serverBuffer, 0, variables.serverBytesRead[clintNo]).Replace("\r\n", replaceWith).Replace("\n", replaceWith).Replace("\r", replaceWith);

                    try
                    {
                        clientStream.Write(serverBuffer, 0, variables.serverBytesRead[clintNo]);
                    }
                    catch (Exception)
                    {

                    }
                    clientStream.Flush();
                    lastString = variables.serverTime[clintNo];
                    Console.WriteLine("{0} Client {1}:{2} Write {3} : {4}", variables.serverTime[clintNo], clintNo, thisClientNo, variables.serverBytesRead[clintNo].ToString(), getString);
                }

                if (clientStream.DataAvailable)
                {
                    //Console.WriteLine("clientStream.DataAvailable");
                    try
                    {
                        bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                    }
                    catch
                    {
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (bytesRead > 0)
                    {
                        //Array.Copy(buffer, 0, variables.clientByte1, 0, bytesRead);

                        setClientByte(buffer, clintNo, bytesRead);

                        variables.clientTime[clintNo] = DateTime.Now.ToString("HH:mm:ss.ffffff");
                        variables.clientBytesRead[clintNo] = bytesRead;

                        string replaceWith = "";
                        string getString = Encoding.ASCII.GetString(buffer, 0, bytesRead).Replace("\r\n", replaceWith).Replace("\n", replaceWith).Replace("\r", replaceWith);
                        Console.WriteLine("{0} Client {1}:{2} Received {3} :{4}", variables.clientTime[clintNo], clintNo, thisClientNo, bytesRead.ToString(), getString);
                    }
                }

                Thread.Sleep(variables.sleepTime);

            }

            tcpClient.Close();

            Console.WriteLine("{0} Client {1}:{2} disconnected.", DateTime.Now.ToString("HH:mm:ss.ffffff"), clintNo, thisClientNo);
            variables.clientNo[clintNo] -= 1;

        }
        private static void ListenForServer()
        {
            int serverNo = variables.proxyNo;
            string lastString = "";


            while (true)
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), variables.serverPort[serverNo]);
                TcpClient server = new TcpClient();

                try
                {
                    server.Connect(ipEndPoint);

                }
                catch (Exception)
                {

                }


                if (server.Connected)
                {
                    Console.WriteLine("{0} Server {1}   Connected {2} <=> {3}", DateTime.Now.ToString("HH:mm:ss.ffffff"), serverNo, server.Client.RemoteEndPoint, server.Client.LocalEndPoint);
                    variables.watchDogTimer[variables.serverGroup[serverNo]] = DateTime.Now.AddMinutes(2);
                    while (server.Connected)
                    {
                        bytesRead = 0;

                        if (lastString != variables.clientTime[serverNo] && variables.clientBytesRead[serverNo] != 0)
                        {
                            string replaceWith = "";
                            byte[] clientBuffer = new byte[4096];
                            clientBuffer = getClientByte(serverNo, variables.clientBytesRead[serverNo]);
                            string getString = Encoding.ASCII.GetString(clientBuffer, 0, variables.clientBytesRead[serverNo]).Replace("\r\n", replaceWith).Replace("\n", replaceWith).Replace("\r", replaceWith);

                            //  if (getString != "LISTEN OFF")
                            //  {


                            try
                            {
                                server.GetStream().Write(clientBuffer, 0, variables.clientBytesRead[serverNo]);
                            }
                            catch (Exception)
                            {

                                //                                throw;
                            }

                            lastString = variables.clientTime[serverNo];
                            Console.WriteLine("{0} Server {1}   Write {2} :{3}", variables.clientTime[serverNo], serverNo, variables.clientBytesRead[serverNo], getString);
                            //}
                        }

                        if (server.Available != 0)
                        {
                            try
                            {
                                bytesRead = server.GetStream().Read(buffer, 0, buffer.Length);
                            }
                            catch (Exception)
                            {
                                bytesRead = 0;
                            }
                        }

                        if (bytesRead > 0)
                        {

                            variables.watchDogTimer[variables.serverGroup[serverNo]] = DateTime.Now.AddSeconds(65);

                            //Array.Copy(buffer, 0, variables.serverByte1, 0, bytesRead);

                            setServerByte(buffer, serverNo, bytesRead);
                            variables.serverTime[serverNo] = DateTime.Now.ToString("HH:mm:ss.ffffff");
                            variables.serverBytesRead[serverNo] = bytesRead;

                            string replaceWith = "";
                            string getString = Encoding.ASCII.GetString(buffer, 0, bytesRead).Replace("\r\n", replaceWith).Replace("\n", replaceWith).Replace("\r", replaceWith);
                            Console.WriteLine("{0} Server {1}   Received {2} :{3}", variables.serverTime[serverNo], serverNo, bytesRead, getString);
                            //    variables.serverData[serverNo] = getString;
                        }

                        if (DateTime.Now > variables.watchDogTimer[variables.serverGroup[serverNo]])
                        {
                            break;
                        }

                        Thread.Sleep(variables.sleepTime);

                    }
                    Console.WriteLine("{0} Server {1}   Disconnected", DateTime.Now.ToString("HH:mm:ss.ffffff"), serverNo);
                }

            }

        }
    }
}
