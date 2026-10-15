using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace PhotoClassy.Services
{
    public static class AdminHelper
    {
        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>관리자 권한으로 앱을 다시 실행한다. UAC 에서 취소하면 false.</summary>
        public static bool RestartAsAdministrator()
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe))
                    return false;

                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" });
                Application.Current.Shutdown();
                return true;
            }
            catch (Win32Exception)
            {
                // 사용자가 UAC 프롬프트를 취소함
                return false;
            }
        }
    }
}
