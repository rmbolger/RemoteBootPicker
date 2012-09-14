using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;
using Cassia;

namespace BootSwitchSvc
{
    sealed class SessionManager : IDisposable
    {
        static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static readonly ITerminalServicesManager tsManager = new TerminalServicesManager();

        // Wait timer and its callback delegate
        Timer waitTimer;
        TimerCallback timerDelegate;

        const string BOOTCAMP_EXEPATH = "%ProgramFiles%\\Boot Camp\\Bootcamp.exe";

        #region Singleton related stuff

        // Thread-safe singleton class modeled from 
        // http://www.yoda.arachsys.com/csharp/singleton.html
        static readonly SessionManager instance = new SessionManager();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static SessionManager() { }
        SessionManager() 
        {
            timerDelegate = new TimerCallback(this.StopWaiting);
            waitTimer = new Timer(timerDelegate);
        }

        public static SessionManager Instance { get { return instance; } }

        #endregion

        void StopWaiting(object stateInfo) 
        {
            lock (waitLock) { Monitor.Pulse(waitLock); }
        }

        // whether or not we've been asked to reboot
        readonly object rebootLock = new object();
        bool rebootRequested = false;

        // the object we're going to wait on when we have to postpone rebooting
        readonly object waitLock = new object();

        public void Reboot()
        {
            lock (rebootLock)
            {
                // return if we've already started the process
                if (rebootRequested) return;
                rebootRequested = true;
            }

            // create rebooter thread
            log.Info("Reboot Requested");
            ThreadPool.QueueUserWorkItem(new WaitCallback(Rebooter));
        }

        /// <summary>
        /// Checks whether the current system can boot into Mac OS (by looking for bootcamp.exe)
        /// It's not a foolproof method, but it's easy and it works. Obviously, it will break
        /// if BootCamp wasn't installed to the default location.
        /// </summary>
        /// <returns>True if bootcamp.exe was found, otherwise false.</returns>
        public bool CanMacBoot()
        {
            if (File.Exists(Environment.ExpandEnvironmentVariables(BOOTCAMP_EXEPATH)))
                return true;
            else
                return false;
        }

        public void SetMacBoot()
        {
            if (!CanMacBoot())
            {
                log.Warn("SetMacBoot was requested, but bootcamp.exe was not found at " + BOOTCAMP_EXEPATH);
                throw new Exception("Bootcamp.exe not found");
            }

            string exePath = Environment.ExpandEnvironmentVariables(BOOTCAMP_EXEPATH);
            string args = "-StartupDisk";
            log.Debug("Running: \"" + exePath + "\" " + args);
            using (var process = new Process())
            {
                process.StartInfo.FileName = exePath;
                process.StartInfo.Arguments = "-StartupDisk";
                try { process.Start(); }
                catch (Exception ex)
                {
                    log.Error("Error running command", ex);
                    throw;
                }
                // it should exit really quick, but give it 5 seconds to be nice
                if (!process.WaitForExit(5000))
                {
                    string errMsg = "Timed out waiting for Bootcamp.exe to exit.";
                    log.Error(errMsg);
                    throw new Exception(errMsg);
                }
            }
        }

        void Rebooter(object stateInfo)
        {
            bool success = false;
            while (!success)
            {
                // Check if a user is logged in
                string username;
                if (SessionManager.Instance.IsUserLoggedIn(out username))
                {
                    // Check again in 5 min
                    if (log.IsDebugEnabled) log.Debug("User still logged in");
                    log.Info("Rebooter waiting for 5 minutes");
                    lock (waitLock)
                    {
                        // set the timer to go off in 5 minutes
                        waitTimer.Change(300000, Timeout.Infinite);

                        // wait until the timer goes off or some other process wakes us up
                        Monitor.Wait(waitLock);

                        // loop so we can re-check whether we still have to wait
                        continue;
                    }
                }

                // invoke the reboot
                try { success = ForceReboot(); }
                catch (Exception ex)
                {
                    log.Error("Error calling ForceReboot", ex);
                    // wait a bit before trying again
                    lock (waitLock)
                    {
                        waitTimer.Change(5000, Timeout.Infinite);
                        Monitor.Wait(waitLock);
                    }
                }
            }
        }

        bool ForceReboot()
        {
            using (var win32OS = new ManagementClass("Win32_OperatingSystem"))
            {
                win32OS.Get();

                // You can't shutdown without security privileges
                win32OS.Scope.Options.EnablePrivileges = true;

                // http://msdn.microsoft.com/en-us/library/aa394058.aspx
                var args = win32OS.GetMethodParameters("Win32Shutdown");
                args["Flags"] = "6";    // Forced Reboot (2 + 4)
                args["Reserved"] = "0"; // ignored

                foreach (ManagementObject instance in win32OS.GetInstances())
                {
                    // The return code of the method is in the "returnValue" property of the outParams object
                    var outParams = instance.InvokeMethod("Win32Shutdown", args, null);
                    int result = Convert.ToInt32(outParams["returnValue"]);
                    if (result == 0)
                    {
                        if (log.IsDebugEnabled) log.Debug("Win32Shutdown call for forced reboot succeeded");
                        return true;
                    }
                    else throw new System.ComponentModel.Win32Exception(result);
                }
            }

            return false;
        }

        /// <summary>
        /// This let's the other components notify us when someone logs
        /// out so we can continue with a pending reboot.
        /// </summary>
        public void UserLoggedOut()
        {
            lock (waitLock) { Monitor.Pulse(waitLock); }
        }

        /// <summary>
        /// Check for non-administrator logins.  In our environment, no one should be logging in
        /// as administrator.  So we don't care if we kick them off.
        /// </summary>
        /// <param name="username">Returns the username of the logged in user if there is one, otherwise returns an empty string.</param>
        /// <returns>True if a user is logged in, otherwise false.</returns>
        public bool IsUserLoggedIn(out string username)
        {
            using (ITerminalServer ts = tsManager.GetLocalServer())
            {
                ts.Open();
                foreach (ITerminalServicesSession session in ts.GetSessions())
                {
                    if (session.ConnectionState == ConnectionState.Active && session.UserName.ToLower() != "administrator")
                    {
                        username = session.DomainName + "\\" + session.UserName;
                        return true;
                    }
                }
            }
            username = String.Empty;
            return false;
        }

        public void Dispose() { waitTimer.Dispose(); }
    }
}
