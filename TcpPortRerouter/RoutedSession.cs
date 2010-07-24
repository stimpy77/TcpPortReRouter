using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Diagnostics;

namespace TcpPortReRouter
{
    /// <summary>
    /// Defines the behavior of an active TCP Port ReRouter socket session.
    /// </summary>
    public class RoutedSession : IDisposable
    {
        /// <summary>
        /// Raised when this socket session has terminated.
        /// </summary>
        public event EventHandler Closed;

        /// <summary>
        /// Raised when a socket session has started. The sender is the new <see cref="RoutedSession"/> instance.
        /// </summary>
        public static event EventHandler SessionStarted;

        /// <summary>
        /// Raised when data is being transferred from the client to the routed target host.
        /// </summary>
        public event BytesTransferredEventHandler BytesForwardedFromClient;

        /// <summary>
        /// Raised when the routed target host has sent data that is being transferred to the client.
        /// </summary>
        public event BytesTransferredEventHandler BytesForwardedFromExtensionHost;

        /// <summary>
        /// Describes an event handler that has a strongly-typed sender of <see cref="RoutedSession"/>
        /// and contains a byte array as the event args.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="bytes"></param>
        public delegate void BytesTransferredEventHandler(RoutedSession sender, byte[] bytes);

        /// <summary>
        /// Sets up a new <see cref="RoutedSession"/> and immediately begins listening 
        /// for data to propagate to both sides of a session. This method should be 
        /// invoked after a client has connected to a route listener and the connection 
        /// socket has been accepted by the listener. A new socket will be created by this
        /// method to the routed target host.
        /// </summary>
        /// <param name="route"></param>
        /// <param name="clientSocket"></param>
        /// <returns></returns>
        public static RoutedSession BeginSession(Route route, Socket clientSocket)
        {
            var newSession = new RoutedSession { ClientSocket = clientSocket };
            newSession.Route = route;
            var extnSocket = new TcpClient();
            IPAddress ipaddr = null;
            if (!IPAddress.TryParse(route.TargetHost, out ipaddr))
            {
                ipaddr = Dns.GetHostAddresses(route.TargetHost).First();
            }
            extnSocket.Connect(new System.Net.IPEndPoint(ipaddr, route.TargetPort));
            newSession.ClientSocket = clientSocket;
            newSession.ExtensionSocket = extnSocket.Client;
            newSession.ClientEndPoint = (IPEndPoint)newSession.ClientSocket.RemoteEndPoint;
            newSession.Monitor();
            return newSession;
        }

        /// <summary>
        /// Gets the <see cref="IPEndPoint"/> of the client in this session.
        /// </summary>
        public IPEndPoint ClientEndPoint { get; private set; }

        /// <summary>
        /// Gets the <see cref="Socket"/> of the client in this session.
        /// </summary>
        public Socket ClientSocket { get; private set; }

        /// <summary>
        /// Gets the <see cref="Socket"/> of the routed target host in this session.
        /// </summary>
        public Socket ExtensionSocket { get; private set; }

        /// <summary>
        /// Gets the <see cref="Route"/> for this session.
        /// </summary>
        public Route Route { get; private set; }

        /// <summary>
        /// Gets whether this session has already been disposed. 
        /// If true, nothing in this session is usable.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Shuts down all sockets and marks this session as <see cref="Disposed"/>=true.
        /// </summary>
        public void Close()
        {
            try { ClientSocket.Close(); }
            catch { }

            try { ExtensionSocket.Close(); }
            catch { }

            if (Closed != null) Closed(this, new EventArgs());
            Disposed = true;
        }

        void IDisposable.Dispose()
        {
            Close();
            ClientSocket = null;
            ExtensionSocket = null;
        }

        private SocketAsyncEventArgs ClientSocketAsyncEventArgs;
        private SocketAsyncEventArgs ExtensionSocketAsyncEventArgs;
        private void Monitor()
        {
            if (SessionStarted != null)
            {
                SessionStarted(this, new EventArgs());
            }

            ClientSocketAsyncEventArgs = new SocketAsyncEventArgs();
            ClientSocketAsyncEventArgs.SetBuffer(new byte[1024], 0, 1024);
            ClientSocketAsyncEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ClientSocketAsyncReceive_Completed);
            ClientSocketAsyncEventArgs.UserToken = new AsyncUserToken(ClientSocket);
            ClientSocketAsyncEventArgs.RemoteEndPoint = ClientSocket.RemoteEndPoint;
            ClientSocket.ReceiveAsync(ClientSocketAsyncEventArgs);

            ExtensionSocketAsyncEventArgs = new SocketAsyncEventArgs();
            ExtensionSocketAsyncEventArgs.SetBuffer(new byte[1024], 0, 1024);
            ExtensionSocketAsyncEventArgs.Completed += new EventHandler<SocketAsyncEventArgs>(ExtensionSocketAsyncReceive_Completed);
            ExtensionSocketAsyncEventArgs.UserToken = new AsyncUserToken(ExtensionSocket);
            ExtensionSocketAsyncEventArgs.RemoteEndPoint = ClientSocket.RemoteEndPoint;
            ExtensionSocket.ReceiveAsync(ExtensionSocketAsyncEventArgs);
        }

        void ClientSocketAsyncReceive_Completed(object sender, SocketAsyncEventArgs e)
        {
            var sourceSocketRole = "Client";
            var targetSocketRole = "RoutedHost";
            try
            {
                sourceSocketRole = ClientSocket.RemoteEndPoint.ToString() + " (" + sourceSocketRole + ")";
                targetSocketRole = ExtensionSocket.RemoteEndPoint.ToString() + " (" + targetSocketRole + ")";
            }
            catch { }
            ForwardToSocket(ClientSocket, sourceSocketRole, ExtensionSocket, targetSocketRole, e);
        }

        void ExtensionSocketAsyncReceive_Completed(object sender, SocketAsyncEventArgs e)
        {
            var sourceSocketRole = "RoutedHost";
            var targetSocketRole = "Client";
            try
            {
                sourceSocketRole = ExtensionSocket.RemoteEndPoint.ToString() + " (" + sourceSocketRole + ")";
                targetSocketRole = ClientSocket.RemoteEndPoint.ToString() + " (" + targetSocketRole + ")";
            }
            catch { }
            ForwardToSocket(ExtensionSocket, sourceSocketRole, ClientSocket, targetSocketRole, e);
        }

        private void ForwardToSocket(Socket sourceSocket, string sourceSocketRole, 
            Socket targetSocket, string targetSocketRole, SocketAsyncEventArgs e)
        {
            if (!ValidateState(targetSocket, SelectMode.SelectWrite)) return;
            SocketError errorCode = SocketError.Success;
            targetSocket.Send(e.Buffer, e.Offset, e.BytesTransferred, SocketFlags.None, out errorCode);
            AnnotateTransferIfUnderstood(sourceSocketRole, targetSocketRole, e);
            if (errorCode != SocketError.Success) Close();
            //if (!ValidateState()) return;
            try
            {
                sourceSocket.ReceiveAsync(e);
            }
            catch {
                Close();
            }
        }

        private void AnnotateTransferIfUnderstood(string sourceRole, string targetRole, SocketAsyncEventArgs e)
        {
            var bytes = new byte[e.BytesTransferred];
            var debug = false;
#if DEBUG
            debug = true;
#endif
            var raiseEventClient = sourceRole.Contains("Client") && BytesForwardedFromClient != null;
            var raiseEventExtn = sourceRole.Contains("Host") && BytesForwardedFromExtensionHost != null;
            if (debug || raiseEventClient || raiseEventExtn)
            {
                Array.ConstrainedCopy(e.Buffer, e.Offset, bytes, 0, e.BytesTransferred);
                if (raiseEventClient) BytesForwardedFromClient(this, bytes);
                if (raiseEventExtn) BytesForwardedFromExtensionHost(this, bytes);
                try
                {
                    var s = new string(Encoding.Default.GetChars(bytes));

                    // HTTP
                    if (s.StartsWith("GET") || s.StartsWith("POST") || s.StartsWith("HTTP"))
                    {
                        Debug.WriteLine(Route.Name + ": " + sourceRole + "->" + targetRole + ": " + s.Split('\n')[0]);
                    }
                    else
                    {
                        Debug.WriteLine(Route.Name + ": " + sourceRole + "->" + targetRole + ": [data]");
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Validates that both the client and the routed target host are connected
        /// to this session, in which case this method returns true.
        /// If either side has disconnected, the session is closed, and this method 
        /// returns false.
        /// </summary>
        /// <returns></returns>
        public bool ValidateState()
        {
            var ret = ValidateState(ClientSocket);
            if (ret) ret = ValidateState(ExtensionSocket);
            return ret;
        }

        private bool ValidateState(Socket socket, SelectMode selectMode)
        {
            if (!socket.Connected)
            {
                Close();
                return false;
            }
            try
            {
                var ret = socket.Poll(1000 * 500, selectMode);
                if (!ret)
                {
                    Close();
                    return false;
                }
            }
            catch
            {
                Close();
                return false;
            }
            return true;
        }

        private bool ValidateState(Socket socket)
        {
            if (!socket.Connected)
            {
                Close();
                return false;
            }
            try
            {
                var ret = socket.Poll(1000*500, SelectMode.SelectWrite);
                if (ret) ret = socket.Poll(1000 * 500, SelectMode.SelectRead);
                if (!ret)
                {
                    Close();
                    return false;
                }
            }
            catch
            {
                Close();
                return false;
            }
            return true;
        }

    }
}
