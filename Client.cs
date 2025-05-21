using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RandomCard_Server
{
    class Client
    {
        private Socket socket;
        private IPEndPoint EP;
        public byte readystate = 2;
        public byte[] sendbuffer;
        public byte[] recvbuffer;
        public bool isCanSend = true;
        public bool isCanReceive = true;

        public Socket ConnectedSock {
            get { return socket; }
        }

        public IPEndPoint ConnectedEP
        {
            get { return EP; }
        }

        public Client(Socket ConnectedSocket)
        {
            this.socket = ConnectedSocket;
            EP = ConnectedSocket.RemoteEndPoint as IPEndPoint;
        }

        public void Disconnect()
        {
            this.ConnectedSock.Disconnect(true);
        }

        public IAsyncResult Send(byte[] buffer, AsyncCallback callback)
        {
            this.sendbuffer = buffer;
            return socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, this);
        }

        public IAsyncResult Receive(byte[] buffer, AsyncCallback callback)
        {
            this.recvbuffer = buffer;
            return socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, this);
        }

        public void Send_And_Check(byte[] buffer)
        {
            ConnectedSock.Send(buffer);
            ConnectedSock.Receive(new byte[1]);
        }

        public bool isConnected()
        {
            bool check1 = socket.Poll(100, SelectMode.SelectRead);
            bool check2 = socket.Available == 0;

            if (check1 && check2)
                return false;

            return true;
        }

    }
}
