using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public class HCIEventEventArgs : EventArgs
  {
    private byte[] _buffer;

    public HCIEventEventArgs(byte[] buffer, int length)
    {
      //Only preserve the buffer up to a certain length
      _buffer = new byte[length];
      Array.Copy(buffer, 0, _buffer, 0, length);
    }

    public byte[] Buffer
    {
      get
      {
        return _buffer;
      }
    }
  }
}
