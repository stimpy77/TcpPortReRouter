using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TcpPortReRouter
{
    /// <summary>
    /// Specifies the routes configuration for TCP Port ReRouter.
    /// </summary>
    public class RouteMapConfiguration : ConfigurationSection
    {
        /// <summary>
        /// Specifies the routes collection for the TCP Port ReRouter routes.
        /// </summary>
        [ConfigurationProperty("routes")]
        public RouteMapConfigurationItemCollection RouteMaps
        {
            get
            {
                return this["routes"] as RouteMapConfigurationItemCollection;
            }
        }
    }
}
