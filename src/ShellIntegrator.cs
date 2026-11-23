using System;
using System.IO;
using Microsoft.Win32;

namespace ZynCopy
{
    public static class ShellIntegrator
    {
        private const string MenuName = "Copy with ZynCopy";

        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\ZynCopy"))
                {
                    return key != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Register()
        {
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                // 1. Context menu for individual files
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\ZynCopy"))
                {
                    if (key != null)
                    {
                        key.SetValue("", MenuName);
                        key.SetValue("Icon", "\"" + exePath + "\",0");
                        using (RegistryKey cmdKey = key.CreateSubKey("command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                            }
                        }
                    }
                }

                // 2. Context menu for directories/folders
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\ZynCopy"))
                {
                    if (key != null)
                    {
                        key.SetValue("", MenuName);
                        key.SetValue("Icon", "\"" + exePath + "\",0");
                        using (RegistryKey cmdKey = key.CreateSubKey("command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%1\"");
                            }
                        }
                    }
                }

                // 3. Context menu for directory background (right-click inside folder)
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\Background\shell\ZynCopy"))
                {
                    if (key != null)
                    {
                        key.SetValue("", "Open ZynCopy Here");
                        key.SetValue("Icon", "\"" + exePath + "\",0");
                        using (RegistryKey cmdKey = key.CreateSubKey("command"))
                        {
                            if (cmdKey != null)
                            {
                                cmdKey.SetValue("", "\"" + exePath + "\" \"%V\"");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shell registration warning: " + ex.Message);
            }
        }

        public static void Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\ZynCopy", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\ZynCopy", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\ZynCopy", false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shell unregistration warning: " + ex.Message);
            }
        }
    }
}
