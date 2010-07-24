using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TcpPortReRouter
{
    /// <summary>
    /// Defines a route for TCP Port ReRouter.
    /// </summary>
    public class RouteMapConfigurationItem : ConfigurationElement
    {
        /// <summary>
        /// Configures the name of this route.
        /// </summary>
        [ConfigurationProperty("name")]
        public string RouteName
        {
            get { return this["name"] as string; }
        }

        /// <summary>
        /// Configures which local IP address to listen on. 
        /// This field is optional; it defaults to the primary IP of the local machine.
        /// </summary>
        [ConfigurationProperty("listenIP")]
        public string ListenIP
        {
            get { return this["listenIP"] as string; }
        }

        /// <summary>
        /// Configures which local port to listen on.
        /// </summary>
        [ConfigurationProperty("listenPort", IsRequired = true)]
        public string ListenPort
        {
            get { return this["listenPort"] as string; }
        }

        /// <summary>
        /// Configures the host IP to route traffic to and from when a TCP socket is
        /// established on the listener IP/port.
        /// </summary>
        [ConfigurationProperty("targetHost", IsRequired = true)]
        public string TargetHost
        {
            get { return this["targetHost"] as string; }
        }

        /// <summary>
        /// Configures the host port to route traffic to and from when a TCP socket is
        /// established on the listener IP/port.
        /// </summary>
        [ConfigurationProperty("targetPort")]
        public string TargetPort
        {
            get { return this["targetPort"] as string; }
        }
    }
}
