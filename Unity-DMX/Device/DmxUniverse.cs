using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public struct DMXUniverseSection
{
    public DmxLayoutInstance channelLayout;
    public int layoutStartIndex;
    public int universeStartIndex;
    public int channelCount;
}
public class DMXUniverse
{
    public static int kMaxChannelsPerUniverse = 510;

    public int universeId = 1;
    public List<DMXUniverseSection> sections = new List<DMXUniverseSection>();
    public byte[] dmxData = new byte[0];

    public int AppendDMXLayout(DmxLayoutInstance layout, int layoutStartIndex, int channelsToAdd)
    {
        if (dmxData.Length < kMaxChannelsPerUniverse)
        {
            DMXUniverseSection newSection = new DMXUniverseSection();
            newSection.channelLayout = layout;
            newSection.layoutStartIndex = layoutStartIndex;
            newSection.universeStartIndex = dmxData.Length;
            newSection.channelCount = channelsToAdd;

            // Only add channels up to the max allowed 
            if (dmxData.Length + channelsToAdd > kMaxChannelsPerUniverse)
            {
                newSection.channelCount = kMaxChannelsPerUniverse - dmxData.Length;
            }

            dmxData = new byte[dmxData.Length + newSection.channelCount];

            sections.Add(newSection);

            return newSection.channelCount;
        }
        else
        {
            // Universe is full! No channels added.
            return 0;
        }
    }
}