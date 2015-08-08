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
    public partial class Gw2ClientForm : Form
    {
        public Gw2ClientForm(string sessionId)
        {
            this.sessionId = sessionId;
            InitializeComponent();
        }

        WebSocket conn = null;
        Avatar avatar = null;
        string sessionId = null;
        private ManagementEventWatcher gw2StartWatcher;
        private ManagementEventWatcher gw2StopWatcher;
        private bool isConnectedToGw2 = false;
        private DateTime lastMessageSentDate;
        private string lastMessageSent;
        private string errorMessage;

        private void Gw2ClientForm_Load(object sender, EventArgs e)
        {
            // Set up the session id.
            if (this.sessionId == null)
            {
                this.sessionId = Guid.NewGuid().ToString();
            }
            this.UpdateText();

            // Set up a connection to the websocket server.
            this.conn = new WebSocket("ws://www.tichi.org:44791");
            this.conn.Opened += new EventHandler(Connected);
            this.conn.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(Errored);
            this.conn.Closed += new EventHandler(ConnClosed);
            this.conn.MessageReceived += new EventHandler<MessageReceivedEventArgs>(Received);

            // Link up to the Gw2 application and start watching it for changes.
            Process[] pname = Process.GetProcessesByName("Gw2");
            if (pname.Length > 0)
            {
                this.conn.Open();
            }

            gw2StartWatcher = WatchForProcessStart("Gw2.exe");
            gw2StopWatcher = WatchForProcessEnd("Gw2.exe");
        }

        private void OnExit(object sender, EventArgs e)
        {
            // Close the application.
            this.Close();
        }

        private void Connected(object sender, EventArgs e)
        {
            // Send the registration request to the websocket.
            var msg = new {
                method = "register",
                type = "AvatarSource",
                guid = this.sessionId
            };

            var strMsg = JsonConvert.SerializeObject(msg);
            this.conn.Send(strMsg);

            this.lastMessageSent = "register";
            this.lastMessageSentDate = DateTime.Now;
            this.UpdateText();
        }

        private void avatar_PlayerInfoChanged(object sender, PlayerInfoChangedEventArgs e)
        {
            // The player information has changed, if the connection is open, send it to the websocket server.
            if (this.conn.State == WebSocketState.Open)
            {
                Position newPosition = e.GetPosition();
                string newName = e.GetName();

                if (newName != "")
                {
                    // Send the update request to the websocket server.
                    var msg = new
                    {
                        method = "updateAvatar",
                        guid = this.sessionId,
                        name = newName,
                        x = newPosition.X,
                        y = newPosition.Y,
                        z = newPosition.Z,
                        mapId = newPosition.MapID,
                        worldId = newPosition.WorldID
                    };

                    var strMsg = JsonConvert.SerializeObject(msg);
                    this.conn.Send(strMsg);

                    this.lastMessageSent = "updateAvatar";
                    this.lastMessageSentDate = DateTime.Now;
                }

                this.UpdateText();
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
            this.errorMessage = e.Exception.Message;
            this.UpdateText();
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
                this.UpdateText();
            }
        }

        private void Received(object sender, MessageReceivedEventArgs e)
        {
            // Report the received message to the user
            this.errorMessage = "";

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
            catch (Exception)
            {
                // Report to the user that the message couldn't be understood.
                this.errorMessage = "Could not parse message: " + e.Message;
                this.UpdateText();
            }
        }

        delegate void SetTextCallback(string text);

        private void SetText(string text)
        {
            if (this.IsDisposed)
            {
                return;
            }

            // Since threading is involved for watching the avatar information, check to see if the textbox is writeable, and either write to it if it is or invoke the textbox on the other thread to write to it.
            if (this.displayLabel.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.displayLabel.Text = text;
            }
        }

        private void UpdateText()
        {
            string displayText = "Session Id: " + this.sessionId + Environment.NewLine
                + "Connected to Gw2: " + (this.isConnectedToGw2 ? "Yes" : "No") + Environment.NewLine
                + "Character: " + (this.avatar != null ? this.avatar.Name : "") + Environment.NewLine
                + "World Id: " + (this.avatar != null ? this.avatar.Position.WorldID.ToString() : "") + Environment.NewLine
                + "Map Id: " + (this.avatar != null ? this.avatar.Position.MapID.ToString() : "") + Environment.NewLine
                + "Position: " + (this.avatar != null ? "X" + this.avatar.Position.X.ToString() + " Y" + this.avatar.Position.Y.ToString() + " Z" + this.avatar.Position.Z.ToString() : "") + Environment.NewLine
                + Environment.NewLine
                + "Connected to Webserver: " + (this.conn != null && this.conn.State == WebSocketState.Open ? "Yes" : "No") + Environment.NewLine
                + "Last Message Sent: " + (this.lastMessageSent != null ? "Method " + this.lastMessageSent + ", Time: " + this.lastMessageSentDate.ToString() : "") + Environment.NewLine
                + Environment.NewLine
                + "Last Error: " + (this.errorMessage != null ? this.errorMessage : "");

            this.SetText(displayText);
        }

        private void Gw2ClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // When closing the application, shut down all the resources such as the avatar watching code and the websocket server.
            if (this.conn.State == WebSocketState.Open)
            {
                this.conn.Close();
            }

            gw2StartWatcher.Stop();
            gw2StopWatcher.Stop();
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
            this.isConnectedToGw2 = false;
            this.conn.Close();
            this.UpdateText();
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            // Gw2 was opened, start up the connection to the websocket server.
            this.isConnectedToGw2 = true;
            this.conn.Open();
            this.UpdateText();
        }
    }
}
