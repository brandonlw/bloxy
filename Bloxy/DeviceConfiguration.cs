using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public class DeviceConfiguration
  {
    #region Declarations

    public ulong BDAddr { get; set; }
    public string RemoteName { get; set; }
    public uint DeviceClass { get; set; }
    public byte PageScanRepetitionMode { get; set; }
    public ushort ClockOffset { get; set; } //I'm aware this shouldn't even be saved nor is it even doing it properly, but whatever.

    #endregion

    #region Constructors/Teardown

    public DeviceConfiguration()
    {
      //Do nothing
    }

    #endregion

    #region Public Methods

    public static DeviceConfiguration Load(string fileName)
    {
      var ret = new DeviceConfiguration();

      var reader = File.OpenText(fileName);
      while (!reader.EndOfStream)
      {
        var line = reader.ReadLine();

        if (!String.IsNullOrEmpty(line) && line.Contains("="))
        {
          var key = line.Substring(0, line.IndexOf("="));
          var value = line.Substring(line.IndexOf("=") + 1);

          switch (key)
          {
            case "BDAddr":
              {
                ret.BDAddr = Convert.ToUInt64(value, 16);
                break;
              }
            case "RemoteName":
              {
                ret.RemoteName = value;
                break;
              }
            case "DeviceClass":
              {
                ret.DeviceClass = Convert.ToUInt32(value, 16);
                break;
              }
            case "PageScanRepetitionMode":
              {
                ret.PageScanRepetitionMode = Convert.ToByte(value, 16);
                break;
              }
            case "ClockOffset":
              {
                ret.ClockOffset = Convert.ToUInt16(value, 16);
                break;
              }
            default:
              {
                //Do nothing, ignore it
                break;
              }
          }
        }
      }

      return ret;
    }

    public void Save(string fileName)
    {
      if (File.Exists(fileName)) File.Delete(fileName);
      var writer = File.CreateText(fileName);

      writer.WriteLine("BDAddr=" + this.BDAddr.ToString("X12"));
      writer.WriteLine("RemoteName=" + this.RemoteName);
      writer.WriteLine("DeviceClass=" + this.DeviceClass.ToString("X06"));
      writer.WriteLine("PageScanRepetitionMode=" + this.PageScanRepetitionMode.ToString("X02"));
      writer.WriteLine("ClockOffset=" + this.ClockOffset.ToString("X04"));
      writer.Close();
    }

    #endregion
  }
}
