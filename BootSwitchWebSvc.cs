using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace BootSwitchSvc
{
    [ServiceContract]
    //[AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    //[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    class BootSwitchWebSvc
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region Init/Start/Stop

        WebServiceHost svcHost;

        public BootSwitchWebSvc()
        {
            svcHost = new WebServiceHost(typeof(BootSwitchWebSvc));
        }

        public void StartHosting()
        {
            // start the web service
            try { 
                svcHost.Open();
                foreach (ServiceEndpoint se in svcHost.Description.Endpoints)
                    log.DebugFormat("A: {0}, B: {1}, C: {2}", se.Address, se.Binding.Name, se.Contract.Name);
            }
            catch (Exception ex)
            {
                svcHost.Abort();
                log.Error("Error starting svcHost", ex);
            }
        }

        public void StopHosting()
        {
            // stop the web service
            try { svcHost.Close(); }
            catch (Exception ex)
            {
                svcHost.Abort();
                log.Error("Error stopping svcHost", ex);
            }
        }

        #endregion

        [WebGet(UriTemplate = "os")]
        public string GetOS()
        {
            return "Win";
        }

        [WebGet(UriTemplate = "boot/win")]
        public string BootWindows()
        {
            return "Already booted into Windows";
        }

        [WebGet(UriTemplate = "boot/mac")]
        public string BootMac()
        {
            log.Info("Boot to Mac OS requested from web service");
            try
            {
                SessionManager.Instance.SetMacBoot();
                SessionManager.Instance.Reboot();
                string username;
                if (SessionManager.Instance.IsUserLoggedIn(out username))
                    return "Request to boot Mac accepted. Waiting for current user to logout.";
                else
                    return "Request to boot Mac accepted.";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        [WebGet(UriTemplate = "boot/net")]
        public string BootNet()
        {
            return "Error: Unsupported command from Windows";
        }

        [WebGet(UriTemplate = "bootoptions")]
        public string[] GetBootOptions()
        {
            if (SessionManager.Instance.CanMacBoot())
                return new string[] { "Mac" };
            else
                return new string[] { };
        }

        [WebGet(UriTemplate = "status")]
        public BootSwitchStatus GetStatus()
        {
            var status = new BootSwitchStatus();
            string username;
            status.userLoggedIn = SessionManager.Instance.IsUserLoggedIn(out username);
            status.username = username;
            status.bootoptions = GetBootOptions();
            status.os = GetOS();
            return status;
        }
    }

    [Serializable]
    public class BootSwitchStatus
    {
        public bool userLoggedIn;
        public string username;
        public string[] bootoptions;
        public string os;
    }
}
