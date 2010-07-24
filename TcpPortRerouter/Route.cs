using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace TcpPortReRouter
{
    /// <summary>
    /// Describes a route for proxying any TCP data.
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Gets or sets the name of this route.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets or sets the IP address that TCP Port ReRouter should listen on.
        /// </summary>
        public IPAddress ListenIP { get; set; }

        /// <summary>
        /// Gets or sets the port that TCP Port ReRouter should listen on.
        /// </summary>
        public int ListenPort { get; set; }

        /// <summary>
        /// Gets or sets the host IP to route traffic to and from when a TCP socket is
        /// established on the listener IP/port.
        /// </summary>
        public string TargetHost { get; set; }

        /// <summary>
        /// Gets or sets the host port to route traffic to and from when a TCP socket is
        /// established on the listener IP/port.
        /// </summary>
        public int TargetPort { get; set; }

        /// <summary>
        /// Describes this route in human-readable detail.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var route = this;
            return this.Name + " (" + route.ListenIP.ToString() + ":" + route.ListenPort.ToString()
                + "<-->" + route.TargetHost.ToString() + ":" + route.TargetPort.ToString() + ")";
        }
    }
}
