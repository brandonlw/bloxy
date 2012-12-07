using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bloxy
{
  class Startup
  {
    private const string REAL_CONFIG_FILENAME = "real.txt";
    private const string EMULATED_CONFIG_FILENAME = "emulated.txt";
    private const int INQUIRY_TIMEOUT_SECS = 10;

    //This application allows two computers, each equipped with a USB Bluetooth adapter, to sit between
    //  the connection for two Bluetooth devices:
    //    Bluetooth Host <-> PC1 <-> PC2 <-> Bluetooth Peripheral
    //The PCs can log communication between the Bluetooth devices, extend the range, or
    //  whatever else you can think of.
    //Each PC requires knowledge of where the other one is on the network, as well as information
    //  about both the Bluetooth device it's "emulating" AND the one it's talking to.
    //It stores information about the device it's "emulating" in a file called emulated.txt.
    //It stores information about the device it's actually talking to in a file called real.txt.
    //When running it the first time, it will perform an inquiry scan to find each device;
    //  you select the device from a list, and it will save the information to the text file.
    //Parameters:
    //  /vid=[USB vendor ID of the USB Bluetooth adapter to use]
    //  /pid=[USB product ID of the USB Bluetooth adapter to use]
    //  /buddy=[IP address/hostname of the other PC]
    //  /inport=[TCP port number for incoming connections; should match other PC's outport]
    //  /outport=[TCP port number for outgoing connections; should match other PC's inport]
    //Remember to install the LibUsbDotNet filter driver for the USB Bluetooth adapter as well.
    //  You should probably just install a generated libusb-win32 driver instead, so that Windows
    //    won't try to communicate with it at the same time.
    static void Main(string[] args)
    {
      try
      {
        //Parse command line arguments
        ushort? vid = null;
        ushort? pid = null;
        string buddy = String.Empty;
        int? inport = null;
        int? outport = null;
        foreach (var arg in args)
        {
          if (arg.StartsWith("/") && arg.Contains("="))
          {
            var key = arg.Substring(1, arg.IndexOf("=") - 1);
            var value = arg.Substring(arg.IndexOf("=") + 1);

            switch (key.ToLower())
            {
              case "vid":
                {
                  vid = Convert.ToUInt16(value, 16);
                  break;
                }
              case "pid":
                {
                  pid = Convert.ToUInt16(value, 16);
                  break;
                }
              case "inport":
                {
                  inport = Convert.ToInt32(value);
                  break;
                }
              case "outport":
                {
                  outport = Convert.ToInt32(value);
                  break;
                }
              case "buddy":
                {
                  buddy = value;
                  break;
                }
              default:
                {
                  Logger.WriteLine("Unknown command line argument: " + arg);
                  break;
                }
            }
          }
        }

        //Validate parameters
        if (String.IsNullOrEmpty(buddy))
          throw new ArgumentException("No buddy name/IP specified");
        if (!inport.HasValue || !outport.HasValue)
          throw new ArgumentException("No incoming/outgoing port(s) specified");
        if (!vid.HasValue || !pid.HasValue)
          throw new ArgumentException("No vendor/product ID(s) specified");

        //Initialize the USB Bluetooth adapter device
        Properties.Adapter = new USBBluetoothAdapter(vid.Value, pid.Value);
        Properties.Adapter.Open();

        //Make sure we have device configurations; get them if not
        if (!File.Exists(REAL_CONFIG_FILENAME))
          if (!_CreateConfiguration(REAL_CONFIG_FILENAME)) return;
        if (!File.Exists(EMULATED_CONFIG_FILENAME))
          if (!_CreateConfiguration(EMULATED_CONFIG_FILENAME)) return;

        //Retrieve device configurations
        Properties.RealConfiguration = DeviceConfiguration.Load(REAL_CONFIG_FILENAME);
        Properties.EmulatedConfiguration = DeviceConfiguration.Load(EMULATED_CONFIG_FILENAME);

        //Set ourselves as the device from the configuration file
        Properties.Adapter.SetLocalName(Properties.EmulatedConfiguration.RemoteName);
        Properties.Adapter.SetDeviceClass(Properties.EmulatedConfiguration.DeviceClass);

        //Set ourselves discoverable
        Properties.Adapter.SetDiscoverableMode(true);

        //Set up the server for talking to the other PC
        Properties.Server = new BloxyServer(buddy, inport.Value, outport.Value);
        Properties.Server.Start();

        //Main loop...
        Logger.WriteLine("Waiting for event...");
        while (true)
        {
          if (Console.KeyAvailable)
            if (Console.ReadKey(true).Key == ConsoleKey.Escape)
              break;

          Thread.Sleep(100);
        }

        //Clean up
        Properties.Server.Stop();
        Logger.WriteLine("Done.");
      }
      catch (Exception ex)
      {
        Logger.WriteLine("ERROR: " + ex.ToString());
      }
      finally
      {
        try
        {
          //More cleanup...
          if (Properties.Adapter != null)
            Properties.Adapter.Close();
        }
        catch
        {
          //Whatever...
        }
      }
    }

    private static bool _CreateConfiguration(string fileName)
    {
      //Do an inquiry scan, find a device, and save its information
      Logger.WriteLine(String.Format("Configuration file '{0}' does not exist.", fileName));
      Logger.WriteLine("Performing inquiry scan...");

      var devices = Properties.Adapter.DoInquiryScan(INQUIRY_TIMEOUT_SECS);
      Logger.WriteLine(String.Format("Found {0} devices:", devices.Count));
      var list = new Dictionary<int, InquiryInfo>();
      for (int i = 1; i < devices.Count + 1; i++)
      {
        var name = Properties.Adapter.GetRemoteName(devices[i - 1]);
        Logger.WriteLine(String.Format("\t{0}: {1} - {2}", i.ToString(), devices[i - 1].BDAddr.ToString("X12"), name));

        list.Add(i, new InquiryInfo(devices[i - 1], name));
      }

      while (true)
      {
        Logger.WriteLine("Enter the index of the device you want to use (or 'x' to quit): ");
        var selected = Console.ReadKey().KeyChar;
        Logger.WriteLine();

        //HACK: Yeah, I know this would freak out with more than 9 devices...
        if (selected == 'x')
          return false;
        else if (list.ContainsKey(selected - 0x30))
        {
          var item = list[selected - 0x30];

          //Build the configuration
          var file = new DeviceConfiguration();
          file.BDAddr = item.Result.BDAddr;
          file.RemoteName = item.RemoteName;
          file.DeviceClass = item.Result.DeviceClass;
          file.PageScanRepetitionMode = item.Result.PageScanRepetitionMode;
          file.ClockOffset = item.Result.ClockOffset;

          //Write it out
          Logger.WriteLine(String.Format("Saving configuration file '{0}'...", fileName));
          file.Save(fileName);

          break;
        }
        else
        {
          Logger.WriteLine("Invalid selection, try again.");
        }
      }

      return true;
    }
  }
}
