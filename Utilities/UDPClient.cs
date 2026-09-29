using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;
using Serilog;

namespace opentuner.Utilities
{
    public class UDPClient
    {
        private UdpClient udpClient;
        private int port;
        private bool isListening;
        private bool isStopped;
        private Thread listenThread;
        
        private int _id;

        public event EventHandler<byte[]> DataReceived;
        public event EventHandler<bool> ConnectionStatusChanged;

        public int getID() { return _id; }  

        // Stops the listener loop first, then closes the socket. Closing the socket under a running
        // loop made udpClient.Available throw a NullReferenceException on every exit (issue #16) -
        // caught and logged below, but noisy and it stops the debugger.
        public void Close()
        {
            bool wasListening = isListening;
            isListening = false;

            // The thread only exists once Connect() started it; wait (max. 0.5 s) until it has left its loop.
            for (int i = 0; wasListening && (i < 50) && !isStopped; i++)
            {
                Thread.Sleep(10);
            }

            udpClient?.Close();
        }

        public UDPClient(int port)
        {
            this.port = port;
            udpClient = new UdpClient(port);
            isListening = false;
            listenThread = new Thread(ListenForData);
            listenThread.IsBackground = true;
        }

        public UDPClient(int port, int ID)
        {
            _id = ID;
            this.port = port;
            udpClient = new UdpClient(port);
            isListening = false;
            listenThread = new Thread(ListenForData);
            listenThread.IsBackground = true;
        }


        public void Connect()
        {
            if (!isListening)
            {
                isListening = true;
                isStopped = false;
                listenThread.Start();

                OnConnectionStatusChanged(true);
            }
        }

        public void Disconnect()
        {
            if (isListening)
            {
                isListening = false;
                for (int i = 0; (i < 50) && !isStopped; i++) // wait till thread is stopped. max. 0.5 Seconds
                {
                    Thread.Sleep(100);
                }
//                listenThread.Join(); // Wait for the thread to finish
                udpClient.Close();

                OnConnectionStatusChanged(false);
            }
        }

        private void ListenForData()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                while (isListening)
                {
                    if (0 != udpClient.Available)
                    {
                        byte[] receivedBytes = udpClient.Receive(ref remoteEndPoint);

                        try
                        {
                            OnDataReceived(receivedBytes);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("OnDataReceived event failed: " + ex.Message);
                        }
                    }
                    else
                    {
                        Thread.Sleep(25);
                    }
                }
                isStopped = true;
            }
            catch (Exception ex)
            {
                Log.Error("Listen for UDP Data Exception: "  + this.port.ToString() + " : "+  ex.Message);
                OnConnectionStatusChanged(false);
                isStopped = true;
            }
        }

        protected virtual void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, data);
        }

        protected virtual void OnConnectionStatusChanged(bool status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }
    }
}
