#if !FishyFacepunch
using FishNet.Managing.Logging;
using FishNet.Transporting;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FishyFacepunch.Client
{
    public class ClientSocket : CommonSocket
    {
        #region Private.
        /// <summary>
        /// SteamId for host.
        /// </summary>
        private SteamId _hostSteamID = 0;
        /// <summary>
        /// Socket to use.
        /// </summary>
        private Connection HostConnection => HostConnectionManager.Connection;

        /// <summary>
        /// Use the internal connection manager from steam.
        /// </summary>
        private FishyConnectionManager HostConnectionManager;

        /// <summary>
        /// Task used to check for timeout.
        /// </summary>
        private CancellationTokenSource cancelToken;
        private TaskCompletionSource<Task> connectedComplete;
        private TimeSpan ConnectionTimeout;

        /// <summary>
        /// Task used to check for timeout.
        /// </summary>
        private bool _Error = false;

        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal override void Initialize(Transport t)
        {
            base.Initialize(t);
        }       

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal async void StartConnection(string address, ushort port)
        {
            cancelToken = new CancellationTokenSource();
            SteamNetworkingSockets.OnConnectionStatusChanged += OnConnectionStatusChanged;
            ConnectionTimeout = TimeSpan.FromSeconds(Math.Max(1, base.Transport.GetTimeout(false)));

            SetLocalConnectionState(LocalConnectionState.Starting, false);

            try
            {
                if (SteamClient.IsValid)
                {                    
                    connectedComplete = new TaskCompletionSource<Task>();
                    if (!IsValidAddress(address))
                    {
                        _hostSteamID = UInt64.Parse(address);
                        HostConnectionManager = SteamNetworkingSockets.ConnectRelay<FishyConnectionManager>(_hostSteamID);
                    }
                    else
                    {
                        HostConnectionManager = SteamNetworkingSockets.ConnectNormal<FishyConnectionManager>(NetAddress.From(address, port));
                    }
                    HostConnectionManager.ForwardMessage = OnMessageReceived;
                    Task connectedCompleteTask = connectedComplete.Task;
                    Task timeOutTask = Task.Delay(ConnectionTimeout, cancelToken.Token);

                    if (await Task.WhenAny(connectedCompleteTask, timeOutTask) != connectedCompleteTask)
                    {
                        if (cancelToken.IsCancellationRequested)
                        {
                            if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                                Debug.LogError($"The connection attempt was cancelled.");
                        }
                        else if (timeOutTask.IsCompleted)
                        {
                            if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                                Debug.LogError($"Connection to {address} timed out.");
                            StopConnection();
                        }
                        SetLocalConnectionState(LocalConnectionState.Stopped, false);
                    }
                }
                else
                {
                    if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError("SteamWorks not initialized");
                    SetLocalConnectionState(LocalConnectionState.Stopped, false);
                }
            }
            catch (FormatException)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Connection string was not in the right format. Did you enter a SteamId?");
                SetLocalConnectionState(LocalConnectionState.Stopped, false);
                _Error = true;
            }
            catch (Exception ex)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError(ex.Message);
                SetLocalConnectionState(LocalConnectionState.Stopped, false);
                _Error = true;
            }
            finally
            {
                if (_Error)
                {
                    if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError("Connection failed.");
                    SetLocalConnectionState(LocalConnectionState.Stopped, false);
                }
            }
        }

        /// <summary>
        /// Called when local connection state changes.
        /// </summary>
        private void OnConnectionStatusChanged(Connection conn, ConnectionInfo info)
        {
            if (info.State == ConnectionState.Connected)
            {
                SetLocalConnectionState(LocalConnectionState.Started, false);
                connectedComplete.SetResult(connectedComplete.Task);
            }
            else if (info.State == ConnectionState.ClosedByPeer || info.State == ConnectionState.ProblemDetectedLocally)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Common))
                    Debug.Log($"Connection was closed by peer, {info.EndReason}");
                StopConnection();
            }
            else
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Common))
                    Debug.Log($"Connection state changed: {info.State.ToString()} - {info.EndReason}");
            }
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped || base.GetLocalConnectionState() == LocalConnectionState.Stopping)
                return false;

            SetLocalConnectionState(LocalConnectionState.Stopping, false);

            cancelToken?.Cancel();

            //Reset callback.
            SteamNetworkingSockets.OnConnectionStatusChanged -= OnConnectionStatusChanged;

            if (HostConnectionManager != null)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Common))
                    Debug.Log("Sending Disconnect message");
                HostConnection.Close(false, 0, "Graceful disconnect");
                HostConnectionManager = null;
            }

            SetLocalConnectionState(LocalConnectionState.Stopped, false);

            return true;
        }

        /// <summary>
        /// Iterations data received.
        /// </summary>
        internal void IterateIncoming()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            HostConnectionManager.Receive(MAX_MESSAGES);
        }

        private void OnMessageReceived(IntPtr dataPtr, int size)
        {
            (byte[] data, int ch) = ProcessMessage(dataPtr, size);
            base.Transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(new ArraySegment<byte>(data), (Channel)ch, Transport.Index));
        }

        /// <summary>
        /// Queues data to be sent to server.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            Result res = base.Send(HostConnection, segment, channelId);
            if (res == Result.NoConnection || res == Result.InvalidParam)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Common))
                    Debug.Log($"Connection to server was lost.");
                StopConnection();
            }
            else if (res != Result.OK)
            {
                if (base.Transport.NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"Could not send: {res.ToString()}");
            }
        }

        /// <summary>
        /// Sends queued data to server.
        /// </summary>
        internal void IterateOutgoing()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            HostConnection.Flush();
        }
    }
}
#endif // !DISABLESTEAMWORKS