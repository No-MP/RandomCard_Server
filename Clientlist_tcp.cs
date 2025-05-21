using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RandomCard_Server
{
    class Clientlist_TCP
    {
        public Boolean flag = true;
        private List<Client> clientlist = new List<Client>();
        private List<Room> Rooms = new List<Room>();
        
        public Clientlist_TCP()
        {
            Thread th = new Thread(Runable);
            th.Start();
        }

        private void Runable()
        {
            while (flag)
            {
                Thread.Sleep(1400);
                RefreshList();
                ReciveRequest();
            }
        } 

        public void Add(Client client)
        {
            clientlist.Add(client);
            client.isCanReceive = true;
        }

        public void Remove(Client client)
        {
            clientlist.Remove(client);
        }
        
        public void CloseRoom(Room room)
        {
            Rooms.Remove(room);
        }

        private void RefreshList()
        {
            try
            {

                byte[] buffer = ListToByte();
                int size = clientlist.Count;
                for (int i = 0; i < size; i++)
                {
                    Client client = clientlist[i];
                    Socket ConnectedSocket = client.ConnectedSock;

                    if (client.isConnected())
                    {
                        if (client.isCanSend)
                        {
                            client.isCanSend = false;
                            client.Send(buffer, new AsyncCallback(SendCallback));
                        }
                    }
                    else
                    {
                        client.ConnectedSock.Close();
                        clientlist.Remove(client);
                        client.isCanSend = true;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private void SendCallback(IAsyncResult async)
        {
            Client client = (Client)async.AsyncState;

            try
            {
                int bytesread = client.ConnectedSock.EndSend(async);

                if (bytesread > 0)
                {
                    Console.WriteLine("Sent {0} bytes to {1}", bytesread, client.ConnectedEP.Address);
                }
                client.isCanSend = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private void ReciveRequest()
        {
            int size = clientlist.Count;

            for (int i = 0; i< size; i ++)
            {
                Client client = clientlist[i];

                if (client.isCanReceive)
                {
                    try
                    {
                        client.isCanReceive = false;

                        byte[] receivebuffer = new byte[256];
                        client.Receive(receivebuffer, new AsyncCallback(ReceiveCallback));
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine(e.StackTrace);
                    }

                }

            }
        }

        private void ReceiveCallback(IAsyncResult async)
        {
            Client client = (Client)async.AsyncState;
            try
            {
                int byteread = client.ConnectedSock.EndReceive(async);

                byte[] newarray = new byte[byteread];
                Array.Copy(client.recvbuffer,newarray,byteread);

                string str = Encoding.UTF8.GetString(newarray);
                string[] sp = str.Split('$');

                byte[] request = new byte[1];
                Room room = null;

                switch (sp[1])
                {
                    case "Create":
                        room = new Room(sp[0], client, this);
                        Rooms.Add(room);
                        clientlist.Remove(client);

                        request[0] = 0;
                        client.ConnectedSock.Send(request);

                        request[0] = 0;
                        client.ConnectedSock.Receive(request);

                        if (request[0] == 1)
                            room.StartRoom();
                        break;
                    case "Join":
                        int cnt = Int32.Parse(sp[0]);

                        if (Rooms[cnt].Join(client))
                        {
                            room = Rooms[cnt];
                            clientlist.Remove(client);
                            request[0] = 1;
                            client.ConnectedSock.Send(request);

                            request[0] = 0;
                            client.ConnectedSock.Receive(request);

                            if (request[0] == 1)
                                room.SendState(client);
                        }
                        else
                        {
                            request[0] = 0;
                            client.ConnectedSock.Send(request);
                        }
                        break;
                }

                client.isCanReceive = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private byte[] ListToByte()
        {
            byte[] Listarray = new byte[0];

            string str = "";

            int size = Rooms.Count;
            for (int i = 0; i<size; i++)
            {
                Room room = Rooms[i];
                string s = room.Name + "&" + room.ClientCounts();
                str += i < (size - 1) ? s + "\n" : s;
            }
            
            if (str.Length != 0)
            {
                string getstring = str.ToString();
                Listarray = Encoding.UTF8.GetBytes(getstring);
            }

            return Listarray;
        }

    }
}
