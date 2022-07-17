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

public class DmxDeviceDefinition
{
    public string DeviceIP { get; set; }
    public int StartUniverse { get; set; }
    public int LedCount { get; set; }
}


public class DmxDeviceInstance : MonoBehaviour
{
    public bool useBroadcast;
    public string remoteIP = "localhost";
    public float fps = 30;
    public int startUniverseId = 1;

    public bool IsBroadcasting { get; private set; }

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

    public void Patch(DmxDeviceDefinition deviceDefinition)
    {
        // See if the IP Address changed
        if (deviceDefinition.DeviceIP != remoteIP)
        {
            StopBroadcasting();
            remoteIP = deviceDefinition.DeviceIP;
            StartBroadcasting();
        }

        // See if the starting universe ID changed
        if (deviceDefinition.StartUniverse != startUniverseId)
        {
            int nextUniverseId = startUniverseId;
            foreach (DMXUniverse universe in universes)
            {
                universe.universeId = nextUniverseId;
                nextUniverseId++;
            }

            deviceDefinition.StartUniverse = startUniverseId;
            SendUniverseDiscoveryPackets();
        }
    }

    public void AppendDMXLayout(DmxLayoutInstance device, int starChannelIndex, int channelCount)
    {
        int layoutStartIndex = starChannelIndex;
        int channelsRemaining = channelCount;

        DMXUniverse currentUniverse = null;

        if (universes.Count > 0)
        {
            currentUniverse = universes[universes.Count - 1];
        }
        else
        {
            currentUniverse = new DMXUniverse();
            currentUniverse.universeId = startUniverseId;
            universes.Add(currentUniverse);
        }

        while (channelsRemaining > 0)
        {
            int channelsAdded = currentUniverse.AppendDMXLayout(device, layoutStartIndex, channelsRemaining);
            layoutStartIndex += channelsAdded;
            channelsRemaining -= channelsAdded;

            if (channelsRemaining > 0)
            {
                DMXUniverse newUniverse = new DMXUniverse();
                newUniverse.universeId = currentUniverse.universeId + 1;

                universes.Add(newUniverse);
                currentUniverse = newUniverse;
            }
        }
    }

    public void StartBroadcasting()
    {
        StopBroadcasting();

        packetFactory = new SacnPacketFactory(ComponentIdentifier, SacnSourceName);

        if (useBroadcast)
        {
            sacnSender = new MulticastSacnSenderIPV4();
            StartDmxPollingTimers();
        }
        else
        {
            StartCoroutine(TryStartUnicastSender(StartDmxPollingTimers));
        }
    }

    private void StartDmxPollingTimers()
    {
        if (sacnSender != null)
        {
            StartCoroutine(UniverseDiscoveryTimer());
            StartCoroutine(PublishDmxDataTimer());

            IsBroadcasting = true;
        }
    }

    public void StopBroadcasting()
    {
        StopAllCoroutines();
        sacnSender = null;

        if (IsBroadcasting)
        {
            Plugin.Log?.Info($"Halting broadcast to {remoteIP}");
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
                if (universe.sections.Count == 0)
                {
                    continue;
                }

                // Pack each DMX channel layout's buffer into the universe's channels.
                // The universe channel buffer should have been allocated at this point.
                foreach (DMXUniverseSection section in universe.sections)
                {
                    Array.Copy(
                        section.channelLayout.dmxData, section.layoutStartIndex,
                        universe.dmxData, section.universeStartIndex,
                        section.channelCount);
                }

                //Debug.Log(string.Format("Sending {0} channels", universe.dmxData.Length));
                SendDMXData((ushort)universe.universeId, universe.dmxData);
            }

            yield return new WaitForSecondsRealtime(1.0f / fps);
        }
    }

    private async void SendUniverseDiscoveryPackets()
    {
        if (universes.Count > 0 && sacnSender != null)
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
        if (sacnSender != null)
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
    }

    IEnumerator TryStartUnicastSender(Action callback)
    {
        while (sacnSender == null)
        {
            System.Func<IPAddress> concurrentMethod = FindIPFromRemoteHost;
            var concurrentResult = concurrentMethod.BeginInvoke(null, null);
            while (!concurrentResult.IsCompleted)
            {
                yield return new WaitForEndOfFrame();
            }
            IPAddress ipAddress = concurrentMethod.EndInvoke(concurrentResult);

            if (ipAddress != null && ipAddress != IPAddress.None)
            {
                sacnSender = new UnicastSacnSender(ipAddress);
                Plugin.Log?.Info(string.Format("Found sACN host {0}", ipAddress.ToString()));
            }
            else
            {
                Plugin.Log?.Error($"Failed to find sACN host {remoteIP}");
            }

            if (sacnSender == null)
            {
                yield return new WaitForSeconds(1.0f);
            }
        }

        callback();
    }

    IPAddress FindIPFromRemoteHost()
    {
        IPAddress address = null;
        bool bHasFound = false;

        try
        {
            if (IPAddress.TryParse(remoteIP, out address))
                return address;

            var addresses = Dns.GetHostAddresses(remoteIP);
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
                remoteIP, e);
        }

        return address;
    }
}