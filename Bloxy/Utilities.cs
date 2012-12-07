using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public static class Utilities
  {
    public static ulong GetLEULong(byte[] buffer, int offset, int length)
    {
      ulong ret = (ulong)(buffer[offset + 0] & 0xFF);
      int shifter = 8;

      for (int i = 1; i < length; i++)
      {
        ret |= (ulong)((ulong)(((ulong)(buffer[offset + i])) << shifter));
        shifter += 8;
      }

      return ret;
    }
  }
}
