using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TcpPortReRouter
{
    /// <summary>
    /// Used with the asynchronous data received monitor.
    /// </summary>
    public class AsyncUserToken
    {
        private System.Net.Sockets.Socket Socket;

        /// <summary>
        /// Creates the AsyncUserToken with the specified socket as context.
        /// </summary>
        /// <param name="socket"></param>
        public AsyncUserToken(System.Net.Sockets.Socket socket)
        {
            this.Socket = socket;
        }
    }
}
