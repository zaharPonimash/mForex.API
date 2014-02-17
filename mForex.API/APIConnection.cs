﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using mForex.API.Packets;
using mForex.API.Exceptions;


namespace mForex.API
{
    public class APIConnection : IApiConnection
    {

        #region Private Fields
        private IPEndPoint serverEndPoint;
        private bool useSsl;

        private TcpClient tcpClient;
        private Stream stream;

        private bool canConnect;

        private ReceiveBuffer recvBuffer;
        private List<byte[]> sendQueue;
        private object sendingMutex;
        private bool isSending;

        private PacketSerializer serializer;       

        private bool disconnectedOccured;

        #endregion Private Fields

        #region Events
        public event Action<Exception> Disconnected;
        public event Action<APINetworkPacket> PacketReceived;
        #endregion Events

        #region Constructors
        /// <summary>
        /// Initialises a new instance the api connection class. 
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="useSsl"></param>
        public APIConnection(IPEndPoint endPoint, bool useSsl)
        {
            Initialise(endPoint, useSsl);
        }

        /// <summary>
        /// Initialises a new instance the api connection class. 
        /// </summary>
        /// <param name="connectToReal"></param>
        public APIConnection(ServerType server)
        {
            switch (server)
            { 
                case ServerType.Demo:
                    Initialise(new IPEndPoint(IPAddress.Loopback, 5615), false);
                    break;
                
                case ServerType.Real:
                    Initialise(new IPEndPoint(IPAddress.Loopback, 6615), true);
                    break;
            }
        }
        #endregion Constructors

        public async Task Connect()
        {
            VerifyCanConnect();
            canConnect = false;

            await SetupClientAndStream();//d

            Task.Run(new Action(ReadLoop)).Ignore();
        }

        public void Disconnect()
        {
            if (stream == null)
                throw new InvalidOperationException("Socket stream is not opened");
            if (tcpClient == null)
                throw new InvalidOperationException("Tcp client is not connected");

            try { stream.Close(); }
            catch { }

            try { tcpClient.Close(); }
            catch { }

            stream = null;
            tcpClient = null;
        }
       
        public void SendPacket(APINetworkPacket packet)
        {                        
            SendPacket(serializer.SerializeWithHeader(packet));         
        }

        #region Private Methods
        private void Initialise(IPEndPoint endPoint, bool useSsl)
        {
            this.serverEndPoint = endPoint;
            this.useSsl = useSsl;
            this.canConnect = true;
            this.recvBuffer = new ReceiveBuffer();
            this.sendQueue = new List<byte[]>();
            this.sendingMutex = new object();
            this.isSending = false;
            this.serializer = new PacketSerializer();
        }
        private void VerifyCanConnect()
        {
            if (!canConnect)
                throw new InvalidOperationException("APIClient can only be used to connect once");
        }

        private async Task SetupClientAndStream()
        {
            tcpClient = new TcpClient();
            Stream stream = null;

            try
            {
                await tcpClient.ConnectAsync(serverEndPoint.Address, serverEndPoint.Port);

                tcpClient.LingerState = new LingerOption(false, 0);
                tcpClient.NoDelay = true;

                stream = tcpClient.GetStream();
            }
            catch (Exception exc)
            {
                throw new ConnectionException("Could not connect to the server.", exc);
            }

            if (useSsl)
            {
                var secureStream = new SslStream(stream, false);
                try
                {
                    secureStream.AuthenticateAsClient("localhost");
                }
                catch (Exception exc)
                {
                    throw new AuthenticationException("Could not authenticate.", exc);
                }

                this.stream = secureStream;
            }
            else
            {
                this.stream = stream;
            }
        }

        private async void ReadLoop()
        {
            var buffer = new byte[8192];

            while (true)
            {
                try
                {
                    int rcvd = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (rcvd == 0)
                    {
                        OnClientDisconnected(new Exception("Connection has been closed by remote"));
                        return;
                    }

                    recvBuffer.PushBytes(new ArraySegment<byte>(buffer, 0, rcvd));

                    HandlePacketFromBuffer();
                }
                catch (Exception exc)
                {
                    OnClientDisconnected(exc);
                    break;
                }
            }
        }
      
        private async void SendPacket(byte[] packet)
        {
            try
            {
                lock (sendingMutex)
                {
                    sendQueue.Add(packet);
                    if (isSending) { return; }
                }

                while (true)
                {
                    byte[] toSend = null;

                    lock (sendingMutex)
                    {
                        if (isSending) { return; }
                        if (sendQueue.Count == 0) { return; }

                        toSend = sendQueue[0];
                        sendQueue.RemoveAt(0);

                        isSending = true;
                    }

                    await stream.WriteAsync(toSend, 0, toSend.Length);

                    lock (sendingMutex) { isSending = false; }
                }
            }
            catch (Exception exc)
            {
                lock (sendingMutex) { isSending = false; }
                OnClientDisconnected(exc);
            }
        }
        private void HandlePacketFromBuffer()
        {
            byte[] packet;
            int packetId;
            while (recvBuffer.TryAcquirePacket(out packetId, out packet))
            {
                var dPacket = serializer.DeserializePacket(packetId, packet);

                OnPacketReceived(dPacket);
            }
        }
          
        #region Event Risers        
        private void OnClientDisconnected(Exception exc)
        {
            lock (sendingMutex)
            {
                if (disconnectedOccured)
                    return;

                disconnectedOccured = true;

                OnDisconnected(exc);
            }
        }
        private void OnDisconnected(Exception exc)
        {
            var h = Disconnected;
            if (h != null)
                EventHandler.RiseSafely(() => h(exc));
        }
        private void OnPacketReceived(APINetworkPacket packet)
        {
            var h = PacketReceived;
            if (h != null)
                EventHandler.RiseSafely(() => h(packet));
        }                        
      
        #endregion Event Risers
        #endregion Private Methods
    }
}
