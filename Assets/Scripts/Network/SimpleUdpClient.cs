using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Kotatsu.Network
{
    public class SimpleUdpClient
    {
        private UdpClient udpClient;
        private IPEndPoint serverEndpoint;
        private bool isRunning = false;

        // Packet type identifiers (must match backend)
        private const byte PKT_RELIABLE = 0x01;
        private const byte PKT_UNRELIABLE = 0x02;

        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnReliableMessage;   // JSON string
        public event Action<string> OnUnreliableMessage; // JSON string
        public event Action<string> OnError;

        public bool IsConnected => isRunning && udpClient != null;

        public void Connect(string host, int port)
        {
            try
            {
                if (!TryResolveAddress(host, out IPAddress address))
                {
                    throw new SocketException((int)SocketError.HostNotFound);
                }

                udpClient = new UdpClient();
                serverEndpoint = new IPEndPoint(address, port);
                udpClient.Connect(serverEndpoint);
                isRunning = true;

                // Start receiving
                Task.Run(ReceiveLoop);
                OnConnected?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        public void Disconnect()
        {
            isRunning = false;
            udpClient?.Close();
            udpClient = null;
            OnDisconnected?.Invoke();
        }

        public void SendReliable(string jsonMessage)
        {
            if (!IsConnected) return;

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonMessage);
            byte[] packet = new byte[jsonBytes.Length + 1];
            packet[0] = PKT_RELIABLE;
            Array.Copy(jsonBytes, 0, packet, 1, jsonBytes.Length);

            try
            {
                udpClient.Send(packet, packet.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to send reliable message: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        public void SendUnreliable(string jsonMessage)
        {
            if (!IsConnected) return;

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonMessage);
            byte[] packet = new byte[jsonBytes.Length + 1];
            packet[0] = PKT_UNRELIABLE;
            Array.Copy(jsonBytes, 0, packet, 1, jsonBytes.Length);

            try
            {
                udpClient.Send(packet, packet.Length);
            }
            catch (Exception)
            {
                // Silently ignore errors for unreliable messages
            }
        }

        private static bool TryResolveAddress(string host, out IPAddress address)
        {
            address = null;

            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (IPAddress.TryParse(host.Trim(), out address))
            {
                return true;
            }

            try
            {
                IPAddress[] addresses = Dns.GetHostAddresses(host.Trim());
                for (int i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = addresses[i];
                        return true;
                    }
                }

                if (addresses.Length > 0)
                {
                    address = addresses[0];
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to resolve host '{host}': {e.Message}");
            }

            return false;
        }

        private async void ReceiveLoop()
        {
            while (isRunning && udpClient != null)
            {
                try
                {
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    ProcessPacket(result.Buffer);
                }
                catch (SocketException)
                {
                    if (isRunning)
                    {
                        Debug.LogError("Socket error in receive loop");
                        isRunning = false;
                        OnDisconnected?.Invoke();
                    }
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Expected when closing
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Receive error: {e.Message}");
                }
            }
        }

        private void ProcessPacket(byte[] packet)
        {
            if (packet.Length < 2) return; // Need at least header + some data

            byte packetType = packet[0];
            string json = Encoding.UTF8.GetString(packet, 1, packet.Length - 1);

            switch (packetType)
            {
                case PKT_RELIABLE:
                    OnReliableMessage?.Invoke(json);
                    break;
                case PKT_UNRELIABLE:
                    OnUnreliableMessage?.Invoke(json);
                    break;
                default:
                    Debug.LogWarning($"Unknown packet type: {packetType:X2}");
                    break;
            }
        }
    }
}
