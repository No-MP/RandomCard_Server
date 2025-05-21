using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RandomCard_Server
{
    class AcceptIn
    {
        private Clientlist_TCP Clients = new Clientlist_TCP();
        private Socket mainsocket = null;
        private IPEndPoint serverEP = null;
        private AsyncCallback callback = null;

        public AcceptIn()
        {
            try
            {
                mainsocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverEP = new IPEndPoint(IPAddress.Any, 15000);
                mainsocket.Bind(serverEP);
                mainsocket.Listen(50);

                callback = new AsyncCallback(AcceptCallback);
                mainsocket.BeginAccept(callback, null);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            
        }

        void AcceptCallback(IAsyncResult async)
        {
            Socket ConnectedSocket = null; 

            try
            {
                ConnectedSocket = mainsocket.EndAccept(async); 

                if (ConnectedSocket.Connected)
                {
                    Client newClient = new Client(ConnectedSocket);
                    IPEndPoint ClientEP = newClient.ConnectedEP;
                    Clients.Add(newClient);
                    Console.WriteLine("{0} is Connected at {1}", ClientEP.Address, ClientEP.Port);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            } finally
            {
                callback = new AsyncCallback(AcceptCallback);
                mainsocket.BeginAccept(callback, null);
            }
        }

        ~AcceptIn()
        {
            if(mainsocket!=null)
                mainsocket.Close();
            Clients.flag = false;
        }

        static void Main(string[] args)
        {
            new AcceptIn();
            Console.ReadLine();
        }
    }

}
