using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace TcpPortReRouter
{
    class Program
    {
        private static PortReRouter ReRouterService;

        private static string HelpText = @"
Available commands:
~=

exit            Exits the program.
";

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Debug.Listeners.Add(new ConsoleTraceListener(true));
            ReRouterService = new PortReRouter();
            ReRouterService.StartListeners();
            Console.WriteLine("Type \"exit\" and hit ENTER to exit.");
            string line = string.Empty;
            Console.Write("> ");
            while (((line = Console.ReadLine()) ?? string.Empty).ToLower() != "exit")
            {
                ExecuteCommandInput(line);
                Console.Write("> ");
            }
            ReRouterService.StopListeners();
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
        }

        private static void ExecuteCommandInput(string line)
        {
            switch (line.Split(' ')[0].Trim())
            {
                case "":
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine(AutoFormat(HelpText));
        }

        private static string AutoFormat(string txt)
        {
            while (txt.Contains("\n~") || txt.StartsWith("~"))
            {
                var sb = new StringBuilder();
                char c = '~';
                var tilde = 0;
                if (txt.StartsWith("~"))
                {
                    c = txt.Substring(1, 1)[0];
                }
                else
                {
                    tilde = txt.IndexOf("\n~") + 1;
                    c = txt.Substring(tilde + 1, 1)[0];
                }
                for (var i = 0; i < Console.WindowWidth - 2; i++)
                {
                    sb.Append(c);
                }
                if (txt.StartsWith("~")) txt = sb.ToString() + txt.Substring(2);
                else txt = txt.Substring(0, tilde) + sb.ToString() + txt.Substring(tilde + 2);
            }
            return txt;
        }

        private static void RenderLine(char c)
        {
            if (Console.CursorLeft > 1) Console.WriteLine();
            for (var i = 0; i < Console.WindowWidth - 2; i++)
            {
                Console.Write(c);
            }
            Console.WriteLine();
        }
    }
}
