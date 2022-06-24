using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class DMXChannelLayout : MonoBehaviour
{
  public byte[] dmxData = new byte[0];
  public abstract int NumChannels { get; }

  public virtual void SetData(byte[] dmxData)
  {
    this.dmxData = dmxData;
  }
}