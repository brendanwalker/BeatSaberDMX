using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using UnityEngine;
using Kadmium_sACN.SacnSender;
using Kadmium_sACN;
using System;
using System.Threading.Tasks;
using BeatSaberDMX;

public class DmxController : MonoBehaviour
{
    public bool useBroadcast;
    public string remoteIP = "localhost";
    public float fps = 30;

    public bool IsBroadcasting { get; private set; }

    public class DMXUniverse
    {
        public int universeId = 0;
        public List<DMXDevice> devices = new List<DMXDevice>();
        public byte[] dmxData = new byte[0];
    }

    protected List<DMXUniverse> universes = new List<DMXUniverse>();

    static byte[] ComponentIdentifier = Guid.Parse("29d71352-c9a8-4066-97a6-117bd10076f6").ToByteArray();
    static string SacnSourceName = "Unity DMX Source";

    SacnSender sacnSender;
    SacnPacketFactory packetFactory;

    private void Start()
    {
        StartBroadcasting();
    }

    private void OnDestroy()
    {
        Plugin.Log?.Error($"DMXController getting destroyed");
        Plugin.Log?.Error(UnityEngine.StackTraceUtility.ExtractStackTrace());

        StopBroadcasting();
    }

    public DMXUniverse FindUniverseById(int UniverseId)
    {
        return universes.Find(u => u.universeId == UniverseId);
    }

    public bool AddDMXDeviceToUniverse(int UniverseId, DMXDevice device)
    {
        DMXUniverse universe = FindUniverseById(UniverseId);

        if (universe == null)
        {
            universe = new DMXUniverse();
            universe.universeId = UniverseId;
            universes.Add(universe);
        }

        int newDmxDataLength = universe.dmxData.Length + device.NumChannels;
        if (newDmxDataLength <= 512)
        {
            universe.devices.Add(device);
            universe.dmxData = new byte[newDmxDataLength];

            return true;
        }
        else
        {
            return false;
        }
    }

    public void StartBroadcasting()
    {
        StopBroadcasting();

        packetFactory = new SacnPacketFactory(ComponentIdentifier, SacnSourceName);

        if (useBroadcast)
        {
            sacnSender = new MulticastSacnSenderIPV4();
        }
        else
        {
            IPAddress ipAddress = FindFromHostName(remoteIP, 10);

            if (ipAddress != null && ipAddress != IPAddress.None)
            {
                sacnSender = new UnicastSacnSender(ipAddress);
                Plugin.Log?.Info(string.Format("Found sACN host {0}", ipAddress.ToString()));
            }
            else
            {
                Plugin.Log?.Error($"Failed to find sACN host {remoteIP}");
            }
        }

        if (sacnSender != null)
        {
            StartCoroutine(UniverseDiscoveryTimer());
            StartCoroutine(PublishDmxDataTimer());

            IsBroadcasting = true;
        }
    }

    public void StopBroadcasting()
    {
        if (IsBroadcasting)
        {
            Plugin.Log?.Info($"Halting broadcast to {remoteIP}");
            StopCoroutine(UniverseDiscoveryTimer());
            StopCoroutine(PublishDmxDataTimer());

            IsBroadcasting = false;
        }
    }

    IEnumerator UniverseDiscoveryTimer()
    {
        while (true)
        {
            SendUniverseDiscoveryPackets();

            // It's good manners to send Universe Discovery packets every 10 seconds.
            // See section 4.3 "E1.31 Universe Discovery Packet" in
            // https://tsp.esta.org/tsp/documents/docs/E1-31-2016.pdf
            yield return new WaitForSecondsRealtime(10);
        }
    }

    IEnumerator PublishDmxDataTimer()
    {
        while (true)
        {
            foreach (var universe in universes)
            {
                //Debug.Log(string.Format("universe {0} has {1} devices", universe.universeId, universe.devices.Count));
                if (universe.devices.Count == 0)
                {
                    continue;
                }

                // Pack each device's DMX channel buffer into the universe's channels.
                // The universe channel buffer should have been allocated at this point.
                int startChannel = 0;
                foreach (DMXDevice device in universe.devices)
                {
                    Array.Copy(
                        device.dmxData, 0,
                        universe.dmxData, startChannel,
                        device.dmxData.Length);
                    startChannel += device.dmxData.Length;
                }

                //Debug.Log(string.Format("Sending {0} channels", universe.dmxData.Length));
                SendDMXData((ushort)universe.universeId, universe.dmxData);
            }

            yield return new WaitForSecondsRealtime(1.0f / fps);
        }
    }

    private async void SendUniverseDiscoveryPackets()
    {
        if (universes.Count > 0)
        {
            UInt16[] universeIDs = universes.Select(u => (UInt16)u.universeId).ToArray();

            var packets = packetFactory.CreateUniverseDiscoveryPackets(universeIDs);
            foreach (var packet in packets)
            {
                if (useBroadcast)
                    await ((MulticastSacnSenderIPV4)sacnSender).Send(packet);
                else
                    await ((UnicastSacnSender)sacnSender).Send(packet);
            }
        }
    }

    private async void SendDMXData(ushort universe, byte[] dmxData)
    {
        var packet = packetFactory.CreateDataPacket(universe, dmxData);

        if (useBroadcast)
        {
            await ((MulticastSacnSenderIPV4)sacnSender).Send(packet);
        }
        else
        {
            //string data = BitConverter.ToString(dmxData).Replace("-", "");
            //Debug.Log(string.Format("u:{0}, d:{1}", universe, data));
            await ((UnicastSacnSender)sacnSender).Send(packet);
        }
    }

    static IPAddress FindFromHostName(string hostname, int maxAttempts)
    {
        IPAddress address = null;
        bool bHasFound = false;

        for (int attempt=0; attempt < maxAttempts && !bHasFound; ++attempt)
        {
            try
            {
                if (IPAddress.TryParse(hostname, out address))
                    return address;

                var addresses = Dns.GetHostAddresses(hostname);
                for (var i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = addresses[i];
                        break;
                    }
                }

                bHasFound = address != null && address != IPAddress.None;
            }
            catch (System.Exception e)
            {
                Debug.LogErrorFormat(
                    "Failed to find IP for :\n host name = {0}\n exception={1}",
                    hostname, e);
                System.Threading.Thread.Sleep(1000);
            }
        }

        return address;
    }
}