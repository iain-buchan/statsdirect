using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace StatsDirect.UI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
#if !WATCH_EXCEPTIONS
            // Right at the start, cope with as many variants of chaos as we can.
            LastChanceCatcher.CatchMostErrors();
#endif

            if (args.Length > 0)
            {
                string command = args[0];
                switch (command)
                {
                    case "-calculator":
                        CalculatorStarter.Start();
                        return;
                    case "-sanity-check":
                        new SanityChecker(new OperationTestHost()).Check();
                        return;
                    case "-test-operations":
                        OperationsTester.TestAll();
                        return;
                    case "FileOpen":
                        // If we're opening a file on behalf of someone, check whether we should open it in an older StatsDirect process that owns the user interface.  If so, we need do nothing more.
                        if (args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1]))
                        {
                            string toOpen = args[1];
                            if (IpcSender.TryToTellAnotherStatsDirectToOpen(toOpen))
                                return;
                            BuildStatsDirectUi(toOpen);
                        }
                        break;
                }
            }

            // If we get here, there were no extra arguments or we didn't understand them.  Launch the default UI.
            BuildStatsDirectUi();
        }

        public static void BuildStatsDirectUi(string? toOpen = default)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            // Pass toOpen to the service constructor.
            // Arguably the least-awful way to get config through; an alternative would be to add the command line as a configuration provider, but that restricts the available argument syntax quite severaly.
            builder.Services.AddSingleton<IHostedService>(x => ActivatorUtilities.CreateInstance<StatsDirectUIService>(x, toOpen));
            IHost host = builder.Build();
            host.Run();
        }
    }
}
