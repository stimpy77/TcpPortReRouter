using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using TcpPortReRouter;

namespace TcpPortReRouter.Service
{
    public partial class TcpPortRerouterService : ServiceBase
    {
        public TcpPortRerouterService()
        {
            InitializeComponent();
        }

        private PortReRouter ReRouterService;

        protected override void OnStart(string[] args)
        {
            ReRouterService = new PortReRouter();
            ReRouterService.StartListeners();
        }

        protected override void OnStop()
        {
            ReRouterService.StopListeners();
        }
    }
}
