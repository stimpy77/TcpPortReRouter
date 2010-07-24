using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace TcpPortReRouter
{
    /// <summary>
    /// The TCP Port ReRouter service. This class loads the configured routes,
    /// sets up the TCP listeners, and invokes the session setup as connections
    /// are picked up by the listener.
    /// </summary>
    public class PortReRouter
    {
        const string ProductName = "TCP Port ReRouter";

        /// <summary>
        /// Initializes the service.
        /// </summary>
        public PortReRouter()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (Routes != null) UnloadRoutes();
            else RoutedSession.SessionStarted += new EventHandler(RoutedSession_SessionStarting);

            TcpListeners = new Dictionary<string, TcpListener>();
            Routes = new Dictionary<string, Route>();
            ActiveSessions = new List<RoutedSession>();

            LoadSettings();
        }

        void RoutedSession_SessionStarting(object sender, EventArgs e)
        {
            var clientEndPoint = ((RoutedSession)sender).ClientEndPoint;
            var route = ((RoutedSession)sender).Route;
            Debug.WriteLine("Session starting: " + clientEndPoint.ToString()
                + "<-->" + route.ToString());
        }

        private void UnloadRoutes()
        {
            foreach (var routekvp in Routes)
            {
                var route = routekvp.Value;
                StopListeningToRoute(route);
            }
        }

        /// <summary>
        /// Contains the configured routes on which the listeners and
        /// connected sessions are based.
        /// </summary>
        public Dictionary<string, Route> Routes { get; set; }

        /// <summary>
        /// Contains the list of currently running <see cref="RoutedSession"/>s.
        /// </summary>
        public List<RoutedSession> ActiveSessions { get; set; }

        /// <summary>
        /// Contains the listeners that have been started up and are 
        /// listening based on the configured <see cref="Routes"/>.
        /// </summary>
        public Dictionary<string, TcpListener> TcpListeners { get; set; }

        private void LoadSettings()
        {
            var settings = ConfigurationManager.GetSection("portRerouter") as RouteMapConfiguration;
            foreach (RouteMapConfigurationItem routeSetting in settings.RouteMaps)
            {
                var route = new Route
                {
                    Name = ResolveRouteName(routeSetting),
                    ListenIP = ResolveListenIP(routeSetting),
                    ListenPort = int.Parse(routeSetting.ListenPort),
                    TargetHost = ResolveTargetHost(routeSetting),
                    TargetPort = ResolveTargetPort(routeSetting)
                };
                if (route.ListenIP == Dns.GetHostAddresses(route.TargetHost).FirstOrDefault() &&
                    route.ListenPort == route.TargetPort)
                {
                    throw new ConfigurationErrorsException("Listen IP and port cannot match target IP and port.");
                }
                Routes.Add(route.Name, route);
            }
        }

        private string ResolveRouteName(RouteMapConfigurationItem routeSetting)
        {
            if (!string.IsNullOrEmpty(routeSetting.RouteName)) return routeSetting.RouteName;
            return ResolveListenIP(routeSetting).ToString() + ":" + routeSetting.ListenPort.ToString();
        }

        private string ResolveTargetHost(RouteMapConfigurationItem routeSetting)
        {
            if (!string.IsNullOrEmpty(routeSetting.TargetHost)) return routeSetting.TargetHost;
            return ResolveListenIP(routeSetting).ToString();
        }

        private int ResolveTargetPort(RouteMapConfigurationItem routeSetting)
        {
            if (!string.IsNullOrEmpty(routeSetting.TargetPort)) return int.Parse(routeSetting.TargetPort);
            return int.Parse(routeSetting.ListenPort);
        }

        private System.Net.IPAddress ResolveListenIP(RouteMapConfigurationItem routeSetting)
        {
            if (!string.IsNullOrEmpty(routeSetting.ListenIP))
            {
                IPAddress ret = null;
                if (IPAddress.TryParse(routeSetting.ListenIP, out ret))
                {
                    return ret;
                }
                ret = Dns.GetHostAddresses(routeSetting.ListenIP).FirstOrDefault();
                if (ret != null) return ret;
            }
            // don't use localhost/127.0.0.1 as that is short-circuited in loopback adapter
            var addrs = Dns.GetHostAddresses(Environment.MachineName);
            foreach (var addr in addrs)
            {
                if (!addr.IsIPv6LinkLocal && !addr.IsIPv6Multicast &&
                    !addr.IsIPv6SiteLocal && !addr.IsIPv6Teredo)
                {
                    return addr;
                }
            }
            return addrs.First();
        }

        ///////////////////////////////////////////////

        /// <summary>
        /// Sets up the <see cref="TcpListener"/>s based on the configured <see cref="Routes"/>.
        /// </summary>
        public void StartListeners()
        {
            foreach (var routekvp in Routes)
            {
                var route = routekvp.Value;
                StartListeningToRoute(route);
            }
            System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(MonitorListeners));
        }

        /// <summary>
        /// Closes all active sessions and shuts down all of the <see cref="TcpListener"/>s
        /// that were set up.
        /// </summary>
        public void StopListeners()
        {
            foreach (var routekvp in Routes)
            {
                var route = routekvp.Value;
                StopListeningToRoute(route);
            }
        }

        private void MonitorListeners(object obj)
        {
            while (TcpListeners.Count > 0)
            {
                System.Threading.Thread.Sleep(500);
                foreach (var session in new List<RoutedSession>(ActiveSessions))
                {
                    session.ValidateState();
                }
            }
        }

        private void StartListeningToRoute(Route route)
        {
            Debug.Write("Starting route listener: " + route.ToString());
            Debug.Write(" ... ");

            try
            {
                var listener = new TcpListener(new IPEndPoint(route.ListenIP, route.ListenPort));
                listener.Start();
                var kvp = new KeyValuePair<string, TcpListener>(route.Name, listener);
                try
                {
                    listener.BeginAcceptSocket(new AsyncCallback(TcpListener_AcceptSocket), kvp);
                    TcpListeners.Add(route.Name, listener);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Debug.WriteLine("Success!");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Debug.WriteLine("Failure!");
                    Debug.WriteLine(e.Message);
                    Console.ResetColor();
                    if (!System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("Console"))
                        EventLog.WriteEntry(ProductName, e.ToString(), EventLogEntryType.Error);
                    else
                    {
                        Debug.WriteLine("Press Enter to continue ...");
                        Console.ReadLine();
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Debug.WriteLine("Fail!");
                Debug.WriteLine(e.Message);
            }
            Console.ResetColor();
        }

        private void StopListeningToRoute(Route route)
        {
            Debug.Write("Stopping route listener: ");
            Debug.Write(route.ToString());
            Debug.Write(" ... ");
            foreach (var listenerkvp in TcpListeners)
            {
                var listener = listenerkvp.Value;
                try { listener.Stop(); }
                catch { }
            }
            if (ActiveSessions != null)
            {
                var sessionsToDispose = new List<RoutedSession>(ActiveSessions);
                foreach (IDisposable session in sessionsToDispose) session.Dispose();
                ActiveSessions.Clear();
            }
        }

        private void TcpListener_AcceptSocket(IAsyncResult ar)
        {
            // Get the listener that handles the client request.
            var listenerkvp = (KeyValuePair<string, TcpListener>)ar.AsyncState;
            var name = listenerkvp.Key;
            var listener = listenerkvp.Value;
            var route = Routes[listenerkvp.Key];

            try
            {
                // Start the session
                var session = RoutedSession.BeginSession(route, listener.EndAcceptSocket(ar));
                session.Closed += new EventHandler(Session_Closed);
                ActiveSessions.Add(session);
            }
            catch { } // skip the session, it failed
            
            // continue listening
            var stateObj2 = new KeyValuePair<string, TcpListener>(route.Name, listener);
            listener.BeginAcceptSocket(new AsyncCallback(TcpListener_AcceptSocket), stateObj2);
        }

        void Session_Closed(object sender, EventArgs e)
        {
            var routedSession = (RoutedSession)sender;
            Debug.WriteLine("Socket session ended: " + routedSession.ClientEndPoint.ToString() + "<--XX-->" + routedSession.Route.ToString());
            ActiveSessions.Remove(routedSession);
        }
    }
}
