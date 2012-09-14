using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]
namespace BootSwitchSvc
{
    static class EntryPoint
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static int Main(string[] args)
        {
            bool rethrow = false;
            try
            {
                // Check for command line arguments
                foreach (string arg in args)
                {
                    switch (arg.ToLower())
                    {
                        case "-i":
                        case "-install":
                            Install(false, args); return 0;
                        case "-u":
                        case "-uninstall":
                            Install(true, args); return 0;
                        case "-?":
                        case "/?":
                            ShowConsoleHelp(); return 0;
                        default:
                            Console.Error.WriteLine("Unknown argument: " + arg + ". Try -?");
                            break;
                    }
                }

                if (System.Environment.UserInteractive)
                {
                    ShowConsoleHelp();
                    return 0;
                }
                rethrow = true; // so that windows sees error...
                ServiceBase[] services = { new WinSvc() };
                ServiceBase.Run(services);
                rethrow = false;

                return 0;
            }
            catch (Exception ex)
            {
                log.Error("Unknown exception in Main", ex);
                if (rethrow) throw;
                return -1;
            }

        }

        static void Install(bool undo, string[] args)
        {
            try
            {
                Console.WriteLine(undo ? "Uninstalling" : "Installing");
                using (AssemblyInstaller inst = new AssemblyInstaller(typeof(WinSvc).Assembly, args))
                {
                    IDictionary state = new Hashtable();
                    inst.UseNewContext = true;
                    try
                    {
                        if (undo) inst.Uninstall(state);
                        else
                        {
                            inst.Install(state);
                            inst.Commit(state);
                        }
                    }
                    catch
                    {
                        try { inst.Rollback(state); }
                        catch { }
                        throw;
                    }
                }
                Console.WriteLine("Done");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        private static void ShowConsoleHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Usage: BootSwitchSvc [-i|-u|-?]");
            Console.WriteLine("    -i    Installs the service.");
            Console.WriteLine("    -u    Uninstalls the service.");
            Console.WriteLine("    -?    Displays this help message");
            Console.WriteLine();
        }
    }

    [RunInstaller(true)]
    public sealed class MyServiceInstallerProcess : ServiceProcessInstaller
    {
        public MyServiceInstallerProcess()
        {
            this.Account = ServiceAccount.LocalSystem;
        }
    }

    [RunInstaller(true)]
    public sealed class MyServiceInstaller : ServiceInstaller
    {
        public MyServiceInstaller()
        {
            this.Description = "Hosts a REST service to allow remote selection of boot volume on dual-boot Macs.";
            this.DisplayName = "Remote Boot Picker";
            this.ServiceName = "BootSwitchSvc";
            this.StartType = System.ServiceProcess.ServiceStartMode.Automatic;

            // The Terminal Services service dependency is only needed in XP.  It enables the use of the WTS* Win32 APIs.
            // But it doesn't hurt to add it in Vista+ OSes as well.
            this.ServicesDependedOn = new string[] { "TermService" };
        }

        protected override void OnAfterInstall(IDictionary savedState)
        {
            base.OnAfterInstall(savedState);

            var svc = new ServiceController("BootSwitchSvc");
            if (svc.Status != ServiceControllerStatus.Running)
                svc.Start();
        }
    }

}
