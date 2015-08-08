using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Gw2Mem;

namespace MistUtilsClient
{
    public class Position
    {
        public float X
        {
            get;
            set;
        }

        public float Y
        {
            get;
            set;
        }

        public float Z
        {
            get;
            set;
        }

        public int WorldID
        {
            get;
            set;
        }

        public int MapID
        {
            get;
            set;
        }
    }

    public delegate void PlayerInfoChangedEventHandler(object sender, PlayerInfoChangedEventArgs args);

    public class PlayerInfoChangedEventArgs : EventArgs
    {
        private Position Position;
        private string Name;

        public PlayerInfoChangedEventArgs(Position position, string name)
        {
            this.Position = position;
            this.Name = name;
        }

        public Position GetPosition()
        {
            return this.Position;
        }

        public string GetName()
        {
            return this.Name;
        }
    }

    class Avatar
    {
        public event PlayerInfoChangedEventHandler PlayerInfoChanged;

        public Position Position
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public bool IsBeingWatched
        {
            get;
            set;
        }

        public Avatar()
        {
            this.SetToEmpty(false);
        }

        private CancellationTokenSource tokenSource;

        private void SetToEmpty(bool notifyObserver = true)
        {
            this.Position = new Position
            {
                X = 0.0F,
                Y = 0.0F,
                Z = 0.0F,
                WorldID = 1,
                MapID = 1
            };
            this.Name = "";

            if (notifyObserver)
            {
                PlayerInfoChanged(this, new PlayerInfoChangedEventArgs(new Position{
                    X = 0.0F,
                    Y = 0.0F,
                    Z = 0.0F,
                    WorldID = 1,
                    MapID = 1
                }, this.Name));
            }
        }

        private void WatchPlayerInfo(CancellationToken token) {
            // If the watch has been requested to be stopped, exit the method.
            if (token.IsCancellationRequested || !this.IsBeingWatched)
            {
                this.SetToEmpty(false);
                return;
            }

            // Get a link to the Gw2 avatar information via the memory link.
            Random random = new Random();
            MumbleLink link = new MumbleLink();

            while (true)
            {
                if (token.IsCancellationRequested || !this.IsBeingWatched)
                {
                    this.SetToEmpty(false);
                    return;
                }

                // Check to see if the player information has changed.
                MumbleLink.PlayerInfo playerInfo = link.GetPlayerInfo();
                MumbleLink.Coordinate coordinate = playerInfo.Coordinates;

                if (this.Position.X != coordinate.x || this.Position.Y != coordinate.y || this.Position.WorldID != coordinate.world_id || this.Position.MapID != coordinate.map_id || this.Name != playerInfo.Name)
                {
                    // The information has changed, update and send the new information to any observers.
                    var oldName = this.Name;
                    this.Position.X = coordinate.x;
                    this.Position.Y = coordinate.y;
                    this.Position.Z = coordinate.z;
                    this.Position.WorldID = coordinate.world_id;
                    this.Position.MapID = coordinate.map_id;
                    this.Name = playerInfo.Name;

                    if (PlayerInfoChanged != null)
                    {
                        Position newPosition = new Position
                        {
                            X = this.Position.X,
                            Y = this.Position.Y,
                            Z = this.Position.Z,
                            WorldID = this.Position.WorldID,
                            MapID = this.Position.MapID
                        };

                        PlayerInfoChanged(this, new PlayerInfoChangedEventArgs(newPosition, this.Name));
                    }
                }

                Thread.Sleep(100);
            }
        }

        public void StartWatchingPlayerInfo()
        {
            if (!this.IsBeingWatched)
            {
                // Set up the process to watch the player information.
                this.IsBeingWatched = true;
                this.tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;
                Task.Factory.StartNew(() => WatchPlayerInfo(token), token);
            }
        }

        public void StopWatchingPlayerInfo()
        {
            if (this.IsBeingWatched)
            {
                // Stop the process watching the player information.
                this.IsBeingWatched = false;
                this.tokenSource.Cancel();
                this.SetToEmpty();
            }
        }
    }
}
