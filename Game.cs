using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomCard_Server
{
    class Game : Connectioninterface
    {
        private List<List<byte>> PlayerCards = new List<List<byte>>(4);
        private List<int> PlayerHealths = new List<int>(4);
        private List<Client> Clients = new List<Client>(4);
        private int Turn = 0;
        private int currentdamage = 0;
        private int AddedDamage = 0;

        public int turns {
            get { return Turn; }
            set
            { Turn = value;
                if (Turn > 3)
                {
                    Turn = 0;
                }
            }
        }

        public Game(List<Client> clients)
        {
            this.Clients = clients;
        }

        public void StartGame()
        {

            for (int i = 0; i<4; i++)
            {
                List<byte> cards = new List<byte>(5);

                for (int j = 0; j < 5; j++)
                    cards.Add(CreateCard());

                byte[] cardarray = cards.ToArray();
                Clients[i].Send_And_Check(cardarray);

                byte[] turns = new byte[4];
                turns[0] = (byte)(i + 1);

                int cnt = 1;
                for (int j = 1; j < 5; j++)
                {
                    if (j != turns[0])
                    {
                        turns[cnt] = (byte)j; 
                        cnt++;
                    }
                }

                Clients[i].Send_And_Check(turns);

                PlayerCards.Add(cards);
                PlayerHealths.Add(200);
            }

            Clients[turns].Send_And_Check(new byte[] { 3, 0 });

            byte[] recvbuffer = new byte[1024];
            Clients[turns].Receive(recvbuffer, new AsyncCallback(ReceiveCallback));
        }

        public void SendInfo(Client client, byte cardcode)
        {
            int playerindex = Clients.IndexOf(client);
            List<byte> cards = PlayerCards[playerindex];
            byte boardclear = 0;

            if (AddedDamage != 0)
            {
                if (currentdamage >= AddedDamage)
                    AddedDamage += currentdamage;
                else
                {
                    boardclear = 1;
                    PlayerHealths[playerindex] -= AddedDamage;
                    if (PlayerHealths[playerindex] < 0)
                        PlayerHealths[playerindex] = 0;
                }
            }
            else
                AddedDamage += currentdamage;

            int count = cards.Count;

            int size = Clients.Count;
            byte[] buffer = { 1, cardcode, (byte)count, (byte)(playerindex + 1), (byte)currentdamage, (byte)AddedDamage, boardclear };
            for (int i = 0; i<size; i ++)
            {
                Clients[i].Send_And_Check(buffer);
            }

            int[] toarrays = PlayerHealths.ToArray();
            byte[] allplayerhealths = { 4 , (byte)toarrays[0], (byte)toarrays[1] , (byte)toarrays[2] , (byte)toarrays[3] };

            for (int i = 0; i < size; i++)
            {
                Clients[i].Send_And_Check(allplayerhealths);
            }
        }
        
        public void ReceiveCallback(IAsyncResult res)
        {
            Client client = (Client)res.AsyncState;

            int byteread = client.ConnectedSock.EndReceive(res);

            byte protocol = client.recvbuffer[0];
            byte[] newbuffer = new byte[byteread - 1];

            Array.Copy(client.recvbuffer, 1, newbuffer, 0 , newbuffer.Length);

            switch (protocol)
            {
                case 0:
                    byte cardno = newbuffer[0];

                    int index = Clients.IndexOf(client);

                    List<byte> cards = PlayerCards[index];
                    int arrayindex = newbuffer[1];
                    cards.RemoveAt(arrayindex);

                    if (cardno == 0)
                    {
                        AddedDamage = AddedDamage * 10;
                        if (AddedDamage > 255)
                            AddedDamage = 255;
                    } else if (cardno == 10)
                    {
                        cards.Clear();

                        for (int i = 0; i < 5; i++)
                            cards.Add(CreateCard());
                    } else if (cardno == 11)
                    {
                        AddedDamage = 0;
                    } else if (cardno == 12)
                    {
                        client.Send_And_Check(new byte[] { 2, 1 });
                        break;
                    } else if (cardno == 13)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            List<byte> playercards = PlayerCards[i];
                            playercards.Clear();

                            for (int j = 0; j < 5; j++)
                                playercards.Add(CreateCard());

                            client.Send_And_Check(ConcatBytes(0, playercards.ToArray()));
                        }
                        break;
                    } else
                    {
                        currentdamage += cardno;

                        if (currentdamage > 255)
                            currentdamage = 255;
                    }

                    client.Send_And_Check(ConcatBytes(0, cards.ToArray()));
                    SendInfo(client, cardno);

                    Clients[turns].ConnectedSock.Send(new byte[] { 3, 1 });
                    break;
                case 1:
                    int cnt = 0;

                    foreach(int health in PlayerHealths)
                    {
                        if (health == 0)
                            cnt++;
                    }

                    if(cnt == 3)
                    {
                        foreach(Client item in Clients)
                            Clients[turns].Send_And_Check(new byte[] { 5, 0 });

                        return;
                    } else
                    {
                        turns++;
                        Clients[turns].Send_And_Check(new byte[] { 3, 0 });
                    }
                    break;
                case 2:
                    int index1 = (newbuffer[0] - 1);
                    int index2 = (newbuffer[1] - 1);
                    int p1 = PlayerHealths[index1];
                    int p2 = PlayerHealths[index2];

                    PlayerHealths[index1] = p2;
                    PlayerHealths[index2] = p1;

                    client.Send_And_Check(ConcatBytes(0, PlayerCards[index1].ToArray()));
                    SendInfo(client, 12);
                    Clients[turns].ConnectedSock.Send(new byte[] { 3, 1 });
                    break;
            }
            
            Clients[turns].Receive(new byte[1024], new AsyncCallback(ReceiveCallback));
        }

        private byte CreateCard()
        {
            byte newbyte = 0;

            Random rand = new Random();

            int nextint = rand.Next(1, 100);
            byte[] cardlist = null;
    
            if (nextint <= 20)
                cardlist = new byte[] { 0, 10, 11, 12 };
            else if(nextint == 100)
                cardlist = new byte[] { 13 };
            else
                cardlist = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            newbyte = cardlist[rand.Next(0, cardlist.Length)];
            
            return newbyte;
        }
    }
}
