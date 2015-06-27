using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Win32;
using System.IO;

namespace Gw2ClientInstallerActions
{
    [RunInstaller(true)]
    public partial class UpdateRegistry : System.Configuration.Install.Installer
    {
        public UpdateRegistry()
        {
            InitializeComponent();
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            try
            {
                // Remove the registry key used by the older version.
                RegistryKey oldKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (oldKey.GetValue("MistUtils Client") != null)
                {
                    oldKey.DeleteValue("MistUtils Client");
                }

                // Check for the url protocol, and create it if it doesn't exist.
                RegistryKey urlKey = Registry.ClassesRoot.OpenSubKey("tichigw2client");
                if (urlKey != null)
                {
                    // Remove the registry keys for the url protocol.
                    Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell\\open\\command");
                    Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell\\open");
                    Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell");
                    Registry.ClassesRoot.DeleteSubKey("tichigw2client");
                }

                // Create new key for desired URL protocol
                urlKey = Registry.ClassesRoot.CreateSubKey("tichigw2client");

                // Assign protocol
                urlKey.SetValue(null, "URL:gw2client Protocol");
                urlKey.SetValue("URL Protocol", string.Empty);

                // Register Shell values
                Registry.ClassesRoot.CreateSubKey("tichigw2client\\Shell");
                Registry.ClassesRoot.CreateSubKey("tichigw2client\\Shell\\open");
                urlKey = Registry.ClassesRoot.CreateSubKey("tichigw2client\\Shell\\open\\command");

                // Specify application handling the URL protocol
                urlKey.SetValue(null, "\"" + Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\Gw2Client.exe\" %1");
            }
            catch (Exception)
            {

            }
        }

        public override void Uninstall(IDictionary savedState)
        {
            try
            {
                // Remove the registry keys for the url protocol.
                Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell\\open\\command");
                Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell\\open");
                Registry.ClassesRoot.DeleteSubKey("tichigw2client\\Shell");
                Registry.ClassesRoot.DeleteSubKey("tichigw2client");
            }
            catch (Exception)
            {

            }

            base.Uninstall(savedState);
        }
    }
}
