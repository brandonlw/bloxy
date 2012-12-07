using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public static class Properties
  {
    public static BloxyServer Server { get; set; }
    public static USBBluetoothAdapter Adapter { get; set; }
    public static DeviceConfiguration RealConfiguration { get; set; }
    public static DeviceConfiguration EmulatedConfiguration { get; set; }
  }
}
