using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TcpPortReRouter
{
    /// <summary>
    /// A <see cref="ConfigurationElementCollection"/> that contains
    /// a collection of <see cref="RouteMapConfigurationItem"/>s.
    /// </summary>
    public class RouteMapConfigurationItemCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// Returns the <see cref="RouteMapConfigurationItem"/> at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public RouteMapConfigurationItem this[int index]
        {
            get
            {
                return base.BaseGet(index) as RouteMapConfigurationItem;
            }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                base.BaseAdd(index, value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new RouteMapConfigurationItem();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((RouteMapConfigurationItem)element).RouteName
                ?? ((RouteMapConfigurationItem)element).ListenIP
                + ":" + ((RouteMapConfigurationItem)element).ListenPort;
        }
    }
}
