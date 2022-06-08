#if !FishyFacepunch
using FishNet.Managing.Logging;
using FishNet.Transporting;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishyFacepunch
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionState _connectionState = LocalConnectionState.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionState GetLocalConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void SetLocalConnectionState(LocalConnectionState connectionState, bool asServer)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;
            if (asServer)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
        }
        #endregion

        #region Protected.
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        /// <summary>
        /// Pointers for received messages per connection.
        /// </summary>
        protected IntPtr[] MessagePointers = new IntPtr[MAX_MESSAGES];
        /// <summary>
        /// Buffer used to receive data.
        /// </summary>
        protected byte[] InboundBuffer = null;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum number of messages which can be received per connection.
        /// </summary>
        protected const int MAX_MESSAGES = 256;
        #endregion

        internal void ClearQueue(Queue<LocalPacket> lpq)
        {
            while (lpq.Count > 0)
            {
                LocalPacket lp = lpq.Dequeue();
                lp.Dispose();
            }
        }
        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal virtual void Initialize(Transport t)
        {
            Transport = t;
            //Get whichever channel has max MTU and resize buffer.
            int maxMTU = Transport.GetMTU(0);
            maxMTU = Math.Max(maxMTU, Transport.GetMTU(1));
            InboundBuffer = new byte[maxMTU];
        }

        /// <summary>
        /// Check if this is a valid address to start a p2p or c2s session.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected bool IsValidAddress(string address)
        {
            //If address is required then make sure it can be parsed.
            if (!string.IsNullOrEmpty(address))
            {
                if (!IPAddress.TryParse(address, out IPAddress result))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Sends data over the steamConnection.
        /// </summary>
        /// <param name="steamConnection"></param>
        /// <param name="segment"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        protected Result Send(Connection conn, ArraySegment<byte> segment, byte channelId)
        {
            /* Have to resize array to include channel index
             * if array isn't large enough to fit it. This is because
             * we don't know what channel data comes in on so
             * the channel has to be packed into the data sent.
             * Odds of the array having to resize are extremely low
             * so while this is not ideal, it's still very low risk. */
            if ((segment.Array.Length - 1) <= (segment.Offset + segment.Count))
            {
                byte[] arr = segment.Array;
                Array.Resize(ref arr, arr.Length + 1);
                arr[arr.Length - 1] = channelId;
            }
            //If large enough just increase the segment and set the channel byte.
            else
            {
                segment.Array[segment.Offset + segment.Count] = channelId;
            }
            //Make a new segment so count is right.
            segment = new ArraySegment<byte>(segment.Array, segment.Offset, segment.Count + 1);

            GCHandle pinnedArray = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
            IntPtr pData = pinnedArray.AddrOfPinnedObject() + segment.Offset;

            SendType sendFlag = (channelId == (byte)Channel.Unreliable) ? SendType.Unreliable : SendType.Reliable;
            Result result = conn.SendMessage(pData, segment.Count, sendFlag);
            if (result != Result.OK)
            {
                if (Transport.NetworkManager.CanLog(LoggingType.Warning))
                    Debug.LogWarning($"Send issue: {result}");
            }

            pinnedArray.Free();
            return result;
        }

        /// <summary>
        /// Returns a message from the steam network.
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected (byte[], int) ProcessMessage(IntPtr ptrs, int size)
        {
            byte[] managedArray = new byte[size];
            Marshal.Copy(ptrs, managedArray, 0, size);
            int channel = managedArray[managedArray.Length - 1];
            Array.Resize(ref managedArray, managedArray.Length - 1);
            return (managedArray, channel);
        }
    }
}
#endif