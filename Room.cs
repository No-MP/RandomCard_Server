using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RandomCard_Server
{
    abstract class Connectioninterface
    {
        public byte[] ConcatBytes(byte protocol, byte[] array)
        {
            byte[] newarray = new byte[array.Length + 1];

            for (int i = 1; i < newarray.Length; i++)
                newarray[i] = array[i - 1];

            newarray[0] = protocol;

            return newarray;
        }

        public void SendData(Client client, byte[] sendbuffer, AsyncCallback SendCallback)
        {
            try
            {
                client.Send(sendbuffer, SendCallback);
                client.ConnectedSock.Receive(new byte[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }
    }

    class Room : Connectioninterface
    {
        private string name;
        private Client host;
        private bool flags = true;
        private List<Client> Clients = new List<Client>(4);
        private byte[] ReadyState; // 0 준비 안됨, 1 준비완료 ,2 플레이어 없음
        private Clientlist_TCP lobby;

        // 프로토콜 : 0 - 준비 데이터 전송, 1 - 접속 끊김 및 새 접속, 2 - 체팅 
        public Room(string names, Client client, Clientlist_TCP lobby)
        {
            this.Name = names;
            this.lobby = lobby;

            Clients.Add(client);
            this.host = client;

            host.readystate = 1;
        }

        public bool Join(Client client)
        {
            if (Clients.Count < 4)
            {
                Clients.Add(client);

                client.readystate = 0;
                return true;
            }
            return false;
        }

        public string Name { get => name; set => name = value; }
        public int ClientCounts()
        {
            return Clients.Count;
        }

        public void StartRoom()
        {
            SendState(host);
            
            Thread th = new Thread(Runable);
            th.Start();
        }
        
        public void SendData(Client client, byte[] buffer)
        {
            SendData(client, buffer, new AsyncCallback(SendCallback));
        }

        public void SendState(Client newclient)
        {
            updateReadystate();

            byte[] listbuffer = ConcatBytes(1, GetList());
            byte[] statebuffer = ConcatBytes(0, ReadyState);

            string connected = newclient.ConnectedEP.Address + " 님이 접속했습니다.";
            byte[] alarm = ConcatBytes(2, Encoding.UTF8.GetBytes(connected));

            foreach (Client client in Clients)
            {
                SendData(client, listbuffer);
                SendData(client, statebuffer);
                SendData(client, alarm);
            }
        }

        private void updateReadystate()
        {
            int size = Clients.Count;

            if (size != 0)
            {
                ReadyState = new byte[size];
                for (int i = 0; i < size; i++)
                    ReadyState[i] = Clients[i].readystate;

                this.host = Clients[0];
            }
            else
            {
                flags = false;
                lobby.CloseRoom(this);
            }
            
        }

        private byte[] GetList()
        {
            string str = "";
            int size = Clients.Count;
            for (int i = 0; i < size; i++)
            {
                string address = Clients[i].ConnectedEP.Address.ToString();
                str += i < (size - 1) ? address + "\n" : address;
            }

            byte[] buffer = Encoding.UTF8.GetBytes(str);
            return buffer;
        }

        public void SendCallback(IAsyncResult res)
        {
            Client client = (Client)res.AsyncState;
            client.ConnectedSock.EndSend(res);
        }

        public void ReceiveCallback(IAsyncResult res)
        {
            try
            {
                Client client = (Client)res.AsyncState;

                int byteread = client.ConnectedSock.EndReceive(res);

                byte[] newarray = new byte[byteread - 1];
                Array.Copy(client.recvbuffer, 1, newarray, 0, newarray.Length);

                byte protocol = client.recvbuffer[0];
                byte[] sendbuffer = null;

                switch (protocol)
                {
                    case 0:
                        if (newarray[0] == 0)
                            client.readystate = 0;
                        else if (newarray[0] == 1)
                            client.readystate = 1;

                        updateReadystate();

                        sendbuffer = ConcatBytes(0, ReadyState);

                        foreach (Client item in Clients)
                            SendData(item, sendbuffer);
                        
                        break;
                    case 1:
                        try
                        {
                            client.readystate = 2;
                            Clients.Remove(client);

                            client.ConnectedSock.Send(new byte[] { 3, 1 });
                            client.ConnectedSock.Receive(new byte[1]);

                            lobby.Add(client);
                            client.isCanSend = true;

                            updateReadystate();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.StackTrace);
                        }
                        
                        foreach (Client item in Clients)
                        {
                            sendbuffer = ConcatBytes(1, GetList());
                            SendData(item, sendbuffer);

                            sendbuffer = ConcatBytes(0, ReadyState);
                            SendData(item, sendbuffer);

                            string alarm = client.ConnectedEP.Address + "님이 방에서 나갔습니다.";
                            sendbuffer = ConcatBytes(2, Encoding.UTF8.GetBytes(alarm));
                            SendData(item, sendbuffer);
                        }
                        break;
                    case 2:
                        string received = Encoding.UTF8.GetString(newarray);

                        string chat = client.ConnectedEP.Address.ToString() + ": " + received;
                        byte[] sending = Encoding.UTF8.GetBytes(chat);

                        sendbuffer = ConcatBytes(2, sending);

                        foreach (Client item in Clients)
                            SendData(item, sendbuffer);
                                 
                        break;
                    case 3:
                        byte[] gamestart = {4, 0};

                        foreach (Client item in Clients)
                            SendData(item, gamestart);

                        flags = false;
                        lobby.CloseRoom(this);

                        Game newgame = new Game(Clients);

                         gamestart[0] = 5;

                        foreach (Client item in Clients)
                            SendData(item, gamestart);
                        newgame.StartGame();

                        break;
                }

                client.isCanReceive = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        public void Runable()
        {
            while (flags)
            {
                try
                {
                    int size = Clients.Count;

                    for (int i = 0; i < size; i++)
                    {
                        Client client = Clients[i];

                        if (client.isConnected())
                        {
                            if (client.isCanReceive)
                            {
                                client.isCanReceive = false;

                                byte[] buffer = new byte[1024];
                                client.Receive(buffer, new AsyncCallback(ReceiveCallback));
                            }
                        }
                        else
                        {
                            client.ConnectedSock.Close();
                            Clients.Remove(client);

                            updateReadystate();
                            i--;

                            byte[] buffer = ConcatBytes(0, ReadyState);
                            byte[] connected = ConcatBytes(1, GetList());

                            string alarm = client.ConnectedEP.Address + "의 연결이 끊겼습니다.";
                            byte[] stringbuffer = ConcatBytes(2, Encoding.UTF8.GetBytes(alarm));

                            foreach (Client item in Clients)
                            {
                                SendData(item, buffer);
                                SendData(item, connected);
                                SendData(item, stringbuffer);
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            }
        }
    }
 }
    