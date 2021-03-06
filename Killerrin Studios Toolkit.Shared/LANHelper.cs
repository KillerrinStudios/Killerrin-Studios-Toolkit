﻿
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using System.Diagnostics;

using KillerrinStudiosToolkit.Datastructures;
using KillerrinStudiosToolkit.Enumerators;
using KillerrinStudiosToolkit.Events;
using Windows.Networking.Connectivity;

namespace KillerrinStudiosToolkit
{
    public class LANHelper
    {
        #region Fields/Properties
        /// <summary>
        /// Sets the mode the LANHelper API will operate under
        /// </summary>
        public HostType HostType { get; set; }

        /// <summary>
        /// Sets GUID the LANHelper API will search for
        /// </summary>
        public Guid AppGUID { get; set; }
        public bool EnforceGUIDMatch { get; set; }

        public string ConnectionCloseMessage { get; set; }

        #region TCP
        public event ReceivedMessageEventHandler TCPMessageRecieved;
        public event OnConnectedEventHandler OnTCPConnected;
        public event EventHandler OnTCPDisconnected;


        public NetworkConnectionEndpoint TCPNetworkConnectionEndpoint { get; protected set; }
        
        public bool IsTCPSetup { get; protected set; }
        public bool IsTCPConnected { get; protected set; }
        public bool IsTCPListening { get; set; }

        public object TCPLock = new object();

        private StreamSocket m_streamSocket;
        private StreamSocketListener m_streamSocketListener;

        public StreamSocketControl StreamSocketControl { get { return m_streamSocket.Control; } }
        public StreamSocketInformation StreamSocketInformation { get { return m_streamSocket.Information; } }

        public StreamSocketListenerControl StreamSocketListenerControl { get { return m_streamSocketListener.Control; } }
        public StreamSocketListenerInformation StreamSocketListenerInformation { get { return m_streamSocketListener.Information; } }

        private DataReader m_tcpDataReader;
        private DataWriter m_tcpDataWriter;
        #endregion

        #region UDP
        public event ReceivedMessageEventHandler UDPMessageRecieved;

        public NetworkConnectionEndpoint UDPNetworkConnectionEndpoint { get; protected set; }
        
        public bool IsUDPSetup { get; protected set; }
        public bool IsUDPListening { get; protected set; }
        public bool IsUDPConnected { get; protected set; }

        public object UDPLock = new object();

        private DatagramSocket m_datagramSocket;
        private DataWriter m_udpDataWriter;

        public DatagramSocketControl DatagramSocketControl { get { return m_datagramSocket.Control; } }
        public DatagramSocketInformation DatagramSocketInformation { get { return m_datagramSocket.Information; } }
        #endregion
        #endregion

        public LANHelper(Guid appGUID, HostType hostType = HostType.Client)
        {
            EnforceGUIDMatch = true;
            AppGUID = appGUID;

            ConnectionCloseMessage = "API Close The Connection";

            HostType = hostType;

            Reset();
        }

        #region Resets
        public void Reset()
        {
            HostType = HostType.Client;

            ResetTCP();
            ResetUDP();
        }

        public void ResetTCP()
        {
            TCPNetworkConnectionEndpoint = new NetworkConnectionEndpoint();

            IsTCPSetup = false;
            IsTCPConnected = false;
            IsTCPListening = false;

            // Dispose of potentially used assets
            if (m_streamSocketListener != null)
                m_streamSocketListener.Dispose();
            
            if (m_tcpDataReader != null)
                m_tcpDataReader.Dispose();

            if (m_tcpDataWriter != null)
                m_tcpDataWriter.Dispose();

            if (m_streamSocket != null)
                m_streamSocket.Dispose();

            // Set them to null now
            m_streamSocketListener = null;
            m_streamSocket = null;
            m_tcpDataReader = null;
            m_tcpDataWriter = null;

            Debug.WriteLine("Resetting TCP");
        }

        public void ResetUDP()
        {
            UDPNetworkConnectionEndpoint = new NetworkConnectionEndpoint();

            IsUDPSetup = false;
            IsUDPConnected = false;

            // Dispose of potentially used assets
            if (m_udpDataWriter != null)
                m_udpDataWriter.Dispose();

            if (m_datagramSocket != null)
                m_datagramSocket.Dispose();

            // Set them to null for reuse
            m_datagramSocket = null;
            m_udpDataWriter = null;

            Debug.WriteLine("Resetting UDP");
        }
        #endregion

        #region Helper Methods
        public static bool IsConnectedToInternet() { return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(); }

        public static string CurrentIPAddressAsString() { return CurrentIPAddress().CanonicalName; }
        public static HostName CurrentIPAddress()
        {
            var icp = NetworkInformation.GetInternetConnectionProfile();
            
            if (icp != null && icp.NetworkAdapter != null)
            {
                var hostname =
                    NetworkInformation.GetHostNames()
                        .SingleOrDefault(
                            hn =>
                            hn.IPInformation != null && hn.IPInformation.NetworkAdapter != null
                            && hn.IPInformation.NetworkAdapter.NetworkAdapterId
                            == icp.NetworkAdapter.NetworkAdapterId);

                if (hostname != null)
                {
                    // the ip address
                    return hostname;
                }
            }

            return new HostName("localhost");
        }

        /// <summary>
        /// Gets all the IP Addresses of this Device
        /// </summary>
        /// <returns>An IEnumerable containing HostNameConnectionProfilePairss</returns>
        /// <exception cref="Exception">Throws an Exception if the user is connected to WiFi and Cellular</exception>
        public static IEnumerable<HostNameConnectionProfilePair> GetCurrentIpAddresses()
        {
            try
            {
                var profiles = NetworkInformation.GetConnectionProfiles().ToList();

                // the Internet connection profile doesn't seem to be in the above list
                profiles.Add(NetworkInformation.GetInternetConnectionProfile());

                IEnumerable<HostName> hostnames =
                    NetworkInformation.GetHostNames().Where(h =>
                        h.IPInformation != null &&
                        h.IPInformation.NetworkAdapter != null).ToList();

                return (from h in hostnames
                        from p in profiles
                        where h.IPInformation.NetworkAdapter.NetworkAdapterId ==
                              p.NetworkAdapter.NetworkAdapterId
                        select new HostNameConnectionProfilePair(h, p)).ToList();
            }
            catch (Exception ex) { throw ex; }
        }


        /// <summary>
        /// Gets all the IP Addresses of this Device parsed into a string
        /// </summary>
        /// <returns>An IEnumerable containing HostNameConnectionProfilePairss</returns>
        /// <exception cref="Exception">Throws an Exception if the user is connected to WiFi and Cellular</exception>
        public static IEnumerable<string> GetCurrentIpAddressesAsString() {
            try
            {
                return (from hcpp in GetCurrentIpAddresses()
                        where !string.IsNullOrEmpty(hcpp.HostName.CanonicalName)
                        select String.Format("{0}|{1}", hcpp.ConnectionProfile.ProfileName, hcpp.HostName.CanonicalName)).ToList();
            }
            catch (Exception ex) { throw ex; }
        }

        #endregion

        #region TCP
        public void InitTCP()
        {
            IsTCPConnected = false;
            IsTCPListening = false;

            m_streamSocket = new StreamSocket();

            m_streamSocketListener = new StreamSocketListener();
            m_streamSocketListener.ConnectionReceived += m_streamSocketListener_ConnectionReceived;

            IsTCPSetup = true;
            Debug.WriteLine("TCP Initialized");
        }

        #region TCP Server
        public async void StartTCPServer(NetworkConnectionEndpoint remoteNetworkConnectionEndpoint)
        {
            if (!IsTCPSetup) InitTCP();
            if (IsTCPConnected) { ResetTCP(); InitTCP(); }

            try
            {
                Debug.WriteLine("Binding TCP Port");
                await m_streamSocketListener.BindServiceNameAsync(remoteNetworkConnectionEndpoint.Port);

                TCPNetworkConnectionEndpoint = remoteNetworkConnectionEndpoint;
                Debug.WriteLine("TCP Connected: " + TCPNetworkConnectionEndpoint.ToString());
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("StartTCPServer", ex)); }
        }

        private async void m_streamSocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Debug.WriteLine("TCP Connection Recieved");
            
            try
            {
                // Get rid of the previous socket
                Debug.WriteLine("Disposing previous TCP socket");
                m_streamSocket.Dispose();
                m_streamSocket = null;

                // Set the new socket
                Debug.WriteLine("Assigning new TCP socket");
                m_streamSocket = args.Socket;

                TCPNetworkConnectionEndpoint = new NetworkConnectionEndpoint(m_streamSocket.Information.RemoteHostName, m_streamSocket.Information.RemotePort);

                Debug.WriteLine("Beginning Connection");
                await DoConnectTCP();
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("m_streamSocketListener_ConnectionReceived", ex)); }
        }
        #endregion

        public async void ConnectTCP(NetworkConnectionEndpoint remoteNetworkConnectionEndpoint)
        {
            if (!IsTCPSetup) InitTCP();
            if (IsTCPConnected) { ResetTCP(); InitTCP(); }

            try
            {
                Debug.WriteLine("Connecting TCPSocket");
                await m_streamSocket.ConnectAsync(remoteNetworkConnectionEndpoint.HostName, remoteNetworkConnectionEndpoint.Port);

                TCPNetworkConnectionEndpoint = remoteNetworkConnectionEndpoint;
                Debug.WriteLine("TCP Connected: " + TCPNetworkConnectionEndpoint.ToString());

                await DoConnectTCP();
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("ConnectTCP", ex)); }


        }

        private async Task DoConnectTCP()
        {
            try
            {
                Debug.WriteLine("Setting TCP StreamSocket Control Settings");
                //StreamSocketControl.NoDelay = true;
                //StreamSocketControl.QualityOfService = SocketQualityOfService.LowLatency;

                Debug.WriteLine("Creating TCPDataWriter");
                m_tcpDataWriter = new DataWriter(m_streamSocket.OutputStream);

                Debug.WriteLine("Creating TCPDataReader");
                m_tcpDataReader = new DataReader(m_streamSocket.InputStream);

                IsTCPConnected = true;
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("ConnectTCP", ex)); }

            // Begin the listen loop
            if (IsTCPConnected)
            {
                if (OnTCPConnected != null)
                    OnTCPConnected(this, new OnConnectedEventArgs(TCPNetworkConnectionEndpoint, NetworkType.LAN));

                BeginListeningTCP();
            }
        }

        public async void SendTCPCloseMessage() { SendTCPMessage(ConnectionCloseMessage); }
        public async void SendTCPMessage(string message)
        {
            if (!IsTCPConnected) return;

            try
            {
                m_tcpDataWriter.WriteInt32(message.Length);
                m_tcpDataWriter.WriteString(message);
                await m_tcpDataWriter.StoreAsync();

                Debug.WriteLine("TCPMessage Sent: " + message);
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("SendTCPMessage", ex)); }
        }

        public async Task BeginListeningTCP()
        {
            IsTCPListening = true;

            while (IsTCPListening)
            {
                try
                {
                    var data = await GetTCPMessage();

                    // Check for the Close Message
                    if (data == ConnectionCloseMessage)
                    {
                        if (TCPMessageRecieved != null)
                        {
                            var outputArgs = new ReceivedMessageEventArgs(data, TCPNetworkConnectionEndpoint, NetworkType.LAN);
                            TCPMessageRecieved(this, outputArgs);
                        }

                        if (OnTCPDisconnected != null) OnTCPDisconnected(this, null);

                        IsTCPListening = false;
                        ResetTCP();
                    }

                    // Do as normal
                    if (IsTCPListening)
                    {
                        if (data != null && TCPMessageRecieved != null)
                        {
                            var outputArgs = new ReceivedMessageEventArgs(data, TCPNetworkConnectionEndpoint, NetworkType.LAN);
                            TCPMessageRecieved(this, outputArgs);
                        }
                    }
                }
                catch (Exception) { IsTCPListening = false; }
            }
        }

        private int failedCounter = 0;
        public int MaxFailedInRowBeforeTimeout = 25;
        private async Task<string> GetTCPMessage()
        {
            try
            {
                await m_tcpDataReader.LoadAsync(4);
                var messageLen = (uint)m_tcpDataReader.ReadInt32();

                await m_tcpDataReader.LoadAsync(messageLen);
                var message = m_tcpDataReader.ReadString(messageLen);
                Debug.WriteLine("Message received: " + message);

                failedCounter = 0;

                return message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(KTKDebugTools.PrintOutException("GetTCPMessage", ex));
                failedCounter++;

                if (failedCounter >= MaxFailedInRowBeforeTimeout) return ConnectionCloseMessage;
            }
            return null;
        }
        #endregion

        #region UDP
        public void InitUDP()
        {
            IsUDPListening = false;
            IsUDPConnected = false;

            m_datagramSocket = new DatagramSocket();
            m_datagramSocket.MessageReceived += OnUDPMessageReceived;

            DatagramSocketControl.QualityOfService = SocketQualityOfService.LowLatency;

            IsUDPSetup = true;
            Debug.WriteLine("UDP Initialized");
        }

        public async void StartUDPListening(string port)
        {
            if (!IsUDPSetup) InitUDP();
            if (IsUDPConnected) { ResetUDP(); InitUDP(); }

            try
            {
                Debug.WriteLine("Binding UDPPort");
                await m_datagramSocket.BindServiceNameAsync(port);
                IsUDPListening = true;
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("StartUDPListening", ex)); }
        }

        public async void ConnectUDP(NetworkConnectionEndpoint remoteNetworkConnectionEndpoint)
        {
            if (!IsUDPSetup) InitUDP();
            if (IsUDPConnected) { ResetUDP(); InitUDP(); }

            try
            {
                if (!IsUDPListening)
                    StartUDPListening(remoteNetworkConnectionEndpoint.Port);

                Debug.WriteLine("Connecting UDPSocket");
                await m_datagramSocket.ConnectAsync(remoteNetworkConnectionEndpoint.HostName, remoteNetworkConnectionEndpoint.Port);

                Debug.WriteLine("Creating UDPDataWriter");
                m_udpDataWriter = new DataWriter(m_datagramSocket.OutputStream);

                Debug.WriteLine("Completed UDP");
                UDPNetworkConnectionEndpoint = remoteNetworkConnectionEndpoint;
                IsUDPConnected = true;

                Debug.WriteLine("UDP Connected: " + UDPNetworkConnectionEndpoint.ToString());
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("ConnectUDP", ex)); }
        }

        public async void SendUDPCloseMessage() { SendUDPMessage(ConnectionCloseMessage); }
        public async void SendUDPMessage(string message)
        {
            if (!IsUDPConnected) return;

            try
            {
                m_udpDataWriter.WriteGuid(AppGUID);
                m_udpDataWriter.WriteString(message);
                await m_udpDataWriter.StoreAsync();

                Debug.WriteLine("UDPMessage Sent: " + message);
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("SendUDPMessage", ex)); }
        }

        void OnUDPMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                var reader = args.GetDataReader();

                var guid = reader.ReadGuid();

                var count = reader.UnconsumedBufferLength;
                var data = reader.ReadString(count);

                if (guid == AppGUID || !EnforceGUIDMatch)
                {
                    Debug.WriteLine("UDPMessage Recieved: " + data);

                    if (UDPMessageRecieved != null)
                    {
                        var outputArgs = new ReceivedMessageEventArgs(data, new NetworkConnectionEndpoint(args.RemoteAddress, args.RemotePort), NetworkType.LAN);
                        UDPMessageRecieved(this, outputArgs);
                    }

                    if (data == ConnectionCloseMessage) { ResetUDP(); }
                }
            }
            catch (Exception ex) { Debug.WriteLine(KTKDebugTools.PrintOutException("OnUDPMessageRecieved", ex)); }
        }
        #endregion

    }
}
