using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Threading;

using WebSocket4Net;
using SuperSocket.ClientEngine;

using Newtonsoft.Json;

using IWshRuntimeLibrary;
using Shell32;
using System.IO;
using Microsoft.Win32;
using System.Management;
using System.Diagnostics;

namespace MistUtilsClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        WebSocket conn = null;
        Avatar avatar = null;
        string GUID = null;
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private ManagementEventWatcher gw2StartWatcher;
        private ManagementEventWatcher gw2StopWatcher;

        private void Form1_Load(object sender, EventArgs e)
        {
            // Set up a connection to the websocket server.
            this.GUID = Guid.NewGuid().ToString();

            this.conn = new WebSocket("ws://www.tichi.org:44791");
            this.conn.Opened += new EventHandler(Connected);
            this.conn.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(Errored);
            this.conn.Closed += new EventHandler(ConnClosed);
            this.conn.MessageReceived += new EventHandler<MessageReceivedEventArgs>(Received);

            // Add a context menu to allow the program to be shut down.
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Create a tray icon.
            trayIcon = new NotifyIcon();
            trayIcon.Text = "MistUtils Client";
            trayIcon.BalloonTipTitle = "MistUtils Client";
            trayIcon.BalloonTipText = "Still running in the system tray!";
            trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            // Add menu to tray icon and show it.
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            // Show the application when the tray icon is double-clicked.
            trayIcon.MouseDoubleClick += new MouseEventHandler(trayIcon_MouseDoubleClick);

            // Set up a registry keep to track whether or not the program should start with windows or not.
            RegistryKey key = Registry.CurrentUser.OpenSubKey(
              @"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (key.GetValue("MistUtils Client") == null)
            {
                checkBox1.Checked = false;
            }
            else
            {
                checkBox1.Checked = true;
            }

            // Link up to the Gw2 application and start watching it for changes.
            Process[] pname = Process.GetProcessesByName("Gw2");
            if (pname.Length > 0)
            {
                this.AppendText("Establishing connection.");
                this.conn.Open();
            }

            gw2StartWatcher = WatchForProcessStart("Gw2.exe");
            gw2StopWatcher = WatchForProcessEnd("Gw2.exe");
        }

        private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // Show the application.
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void OnExit(object sender, EventArgs e)
        {
            // Close the application.
            this.Close();
        }

        private void Connected(object sender, EventArgs e)
        {
            // Show that the connection succeeded.
            this.AppendText("Connection established!\n");

            // Send the registration request to the websocket.
            var msg = new {
                method = "register",
                type = "AvatarSource",
                guid = this.GUID
            };

            var strMsg = JsonConvert.SerializeObject(msg);
            this.conn.Send(strMsg);
            this.AppendText("Sent: " + strMsg + "\n");
        }

        private void avatar_PlayerInfoChanged(object sender, PlayerInfoChangedEventArgs e)
        {
            // The player information has changed, if the connection is open, send it to the websocket server.
            if (this.conn.State == WebSocketState.Open)
            {
                Position newPosition = e.GetPosition();
                string newName = e.GetName();

                // Send the update request to the websocket server.
                var msg = new
                {
                    method = "updateAvatar",
                    guid = this.GUID,
                    name = newName,
                    x = newPosition.X,
                    y = newPosition.Y,
                    z = newPosition.Z,
                    mapId = newPosition.MapID,
                    worldId = newPosition.WorldID
                };

                var strMsg = JsonConvert.SerializeObject(msg);
                this.conn.Send(strMsg);
                this.AppendText("Sent: " + strMsg + "\n");
            }
            else
            {
                // If the connection is closed, release resources and stop watching the Gw2 avatar info.
                avatar.StopWatchingPlayerInfo();
            }
        }

        private void Errored(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            // Report the error to the user.
            this.AppendText("Error: " + e.Exception.Message + "\n");
        }

        private void ConnClosed(object sender, EventArgs e)
        {
            // On closing of the connection, stop watching the Gw2 avatar info and shut down the websocket server connecton.
            avatar.StopWatchingPlayerInfo();
            
            if (this.conn.State == WebSocketState.Open)
            {
                this.conn.Close();
            }

            if (!this.IsDisposed)
            {
                this.AppendText("Connection closed.\n");
            }
        }

        private void Received(object sender, MessageReceivedEventArgs e)
        {
            // Report the received message to the user
            this.AppendText("Received: " + e.Message + "\n");

            try
            {
                // Decode the message into an object to be read.
                dynamic msg = JsonConvert.DeserializeObject<dynamic>(e.Message);

                // If it is a registration result, start watching the Gw2 avatar information and register the handler for it.
                if (msg.requestedMethod == "register" && msg.result == true)
                {
                    avatar = new Avatar();
                    avatar.PlayerInfoChanged += new PlayerInfoChangedEventHandler(avatar_PlayerInfoChanged);
                    avatar.StartWatchingPlayerInfo();
                }
            }
            catch (Exception ex)
            {
                // Report to the user that the message couldn't be understood.
                this.AppendText("Could not parse message.\n");
            }
        }

        delegate void AppendTextCallback(string text);

        private void AppendText(string text)
        {
            // Since threading is involved for watching the avatar information, check to see if the textbox is writeable, and either write to it if it is or invoke the textbox on the other thread to write to it.
            if (this.textBox1.InvokeRequired)
            {
                AppendTextCallback d = new AppendTextCallback(AppendText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox1.AppendText(text);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // When closing the application, shut down all the resources such as the avatar watching code and the websocket server.
            if (this.conn.State == WebSocketState.Open)
            {
                this.conn.Close();
            }

            // Remove it from the system tray.
            trayIcon.Visible = false;
            trayIcon.Dispose();

            gw2StartWatcher.Stop();
            gw2StopWatcher.Stop();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            // When minimizing the application, remove it from the taskbar and keep it only in the system tray.
            if (FormWindowState.Minimized == this.WindowState)
            {
                trayIcon.ShowBalloonTip(500);
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        private void checkBox1_Click(object sender, EventArgs e)
        {
            // Toggle whether or not the application starts with windows in the registry.
            RegistryKey key = Registry.CurrentUser.OpenSubKey(
              @"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (checkBox1.Checked)
            {
                key.SetValue("MistUtils Client", "\"" + Application.ExecutablePath + "\"");
            }
            else
            {
                key.DeleteValue("MistUtils Client");
            }
        }

        private ManagementEventWatcher WatchForProcessStart(string processName)
        {
            // Find the requested process and watch for it starting.
            string queryString =
                "SELECT TargetInstance" +
                "  FROM __InstanceCreationEvent " +
                "WITHIN  10 " +
                " WHERE TargetInstance ISA 'Win32_Process' " +
                "   AND TargetInstance.Name = '" + processName + "'";

            // The dot in the scope means use the current machine
            string scope = @"\\.\root\CIMV2";

            // Create a watcher and listen for events
            ManagementEventWatcher watcher = new ManagementEventWatcher(scope, queryString);
            watcher.EventArrived += ProcessStarted;
            watcher.Start();
            return watcher;
        }

        private ManagementEventWatcher WatchForProcessEnd(string processName)
        {
            // Find the requested process and watch for it stopping.
            string queryString =
                "SELECT TargetInstance" +
                "  FROM __InstanceDeletionEvent " +
                "WITHIN  10 " +
                " WHERE TargetInstance ISA 'Win32_Process' " +
                "   AND TargetInstance.Name = '" + processName + "'";

            // The dot in the scope means use the current machine
            string scope = @"\\.\root\CIMV2";

            // Create a watcher and listen for events
            ManagementEventWatcher watcher = new ManagementEventWatcher(scope, queryString);
            watcher.EventArrived += ProcessEnded;
            watcher.Start();
            return watcher;
        }

        private void ProcessEnded(object sender, EventArrivedEventArgs e)
        {
            // Gw2 was closed, shut down the connection to the websocket server.
            this.AppendText("Gw2 closed, shutting down connection.");
            this.conn.Close();
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            // Gw2 was opened, start up the connection to the websocket server.
            this.AppendText("Establishing connection.");
            this.conn.Open();
        }
    }
}
