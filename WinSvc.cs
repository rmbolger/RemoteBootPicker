using System.ServiceProcess;
using System.Threading;

namespace BootSwitchSvc
{
    class WinSvc : ServiceBase
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        BootSwitchWebSvc bootSwitchSvc;

        public WinSvc()
        {
            // make sure we get notified about the Login/Logout events
            this.CanHandleSessionChangeEvent = true;
        }

        protected override void OnStart(string[] args)
        {
            log.Info("Service Starting");

            if (log.IsDebugEnabled) log.Debug("Starting BootSwitchWebSvc");
            bootSwitchSvc = new BootSwitchWebSvc();
            bootSwitchSvc.StartHosting();
        }

        protected override void OnStop()
        {
            if (log.IsDebugEnabled) log.Debug("Stopping BootSwitchWebSvc");
            bootSwitchSvc.StopHosting();

            log.Info("Service Stopped");
        }

        // Primarily for handling logon/logoff events
        protected override void OnSessionChange(SessionChangeDescription msg)
        {
            if (msg.Reason == SessionChangeReason.SessionLogoff)
            {
                SessionManager.Instance.UserLoggedOut();
            }
        }

    }
}
