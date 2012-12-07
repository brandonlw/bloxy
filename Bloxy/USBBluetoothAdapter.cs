using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace Bloxy
{
  public enum AssignedNumbers
  {
    GeneralUnlimitedIAC = 0x9E8B33,
    LimitedDedicatedIAC = 0x9E8B00
  };

  public enum HCIEvent
  {
    InquiryComplete = 0x01,
    InquiryResult = 0x02,
    ConnectionComplete = 0x03,
    ConnectionRequest = 0x04,
    DisconnectionComplete = 0x05,
    RemoteNameRequestComplete = 0x07,
    QoSSetupComplete = 0x0D,
    Complete = 0x0E,
    Status = 0x0F,
    PINCodeRequestEvent = 0x16,
    MaxSlotsChangeEvent = 0x1B,
    PageScanRepetitionModeChangeEvent = 0x20
  };

  public class USBBluetoothAdapter
  {
    #region Declarations

    private ushort _vendorId;
    private ushort _productId;
    private UsbDevice _device;
    private Dictionary<Opcode, object> _commandData;
    private List<Opcode> _completedCommands;
    private UsbEndpointReader _reader;
    private UsbEndpointWriter _writer;
    private UsbEndpointReader _isoReader;
    private UsbEndpointWriter _isoWriter;

    #endregion

    #region Constructors/Teardown

    public USBBluetoothAdapter(ushort vendorId, ushort productId)
    {
      _vendorId = vendorId;
      _productId = productId;

      _commandData = new Dictionary<Opcode, object>();
      _completedCommands = new List<Opcode>();
    }

    #endregion

    #region Public Events

    public event EventHandler<HCIEventEventArgs> ConnectionComplete;
    public event EventHandler<HCIEventEventArgs> ConnectionRequestReceived;
    public event EventHandler<HCIEventEventArgs> IncomingDataReceived;

    #endregion

    #region Public Methods

    public void Open()
    {
       _device = UsbDevice.OpenUsbDevice(new UsbDeviceFinder(_vendorId, _productId));

      if (_device != null)
      {
        IUsbDevice whole = _device as IUsbDevice;

        if (!ReferenceEquals(whole, null))
        {
          whole.SetConfiguration(1);
          whole.ClaimInterface(1);
        }

        //Set up the endpoints
        var hci = _device.OpenEndpointReader(ReadEndpointID.Ep01);
        _reader = _device.OpenEndpointReader(ReadEndpointID.Ep02);
        _writer = _device.OpenEndpointWriter(WriteEndpointID.Ep02);
        _isoReader = _device.OpenEndpointReader(ReadEndpointID.Ep03);
        _isoWriter = _device.OpenEndpointWriter(WriteEndpointID.Ep03);

        //Set up our read callback(s)
        hci.DataReceived += hci_DataReceived;
        hci.DataReceivedEnabled = true;
        _reader.DataReceived += reader_DataReceived;
        _reader.DataReceivedEnabled = true;
        _isoReader.DataReceived += _isoReader_DataReceived;
        _isoReader.DataReceivedEnabled = true;

        //Reset the device
        Reset();
      }
    }

    public void Reset()
    {
      _SendHCICommand(new Opcode(OpcodeGroupField.HCBaseband, OpcodeCommandField.Reset));

      _commandData.Clear();
      _completedCommands.Clear();
    }

    public List<InquiryResult> DoInquiryScan(int timeoutSeconds)
    {
      const int iac = (int)AssignedNumbers.GeneralUnlimitedIAC;
      var data = new byte[5];
      int timeout = Convert.ToInt32(Math.Round(timeoutSeconds / 1.28));
      if (timeout <= 0 || timeout > 0x30) throw new ArgumentException("Invalid timeout");
      var ret = new List<InquiryResult>();

      data[0] = (byte)(iac & 0xFF);
      data[1] = (byte)((iac & 0xFF00) >> 8);
      data[2] = (byte)((iac & 0xFF0000) >> 16);
      data[3] = Convert.ToByte(timeout);

      var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.Inquiry);
      _SendHCICommand(opcode, data);

      if (_commandData.ContainsKey(opcode))
      {
        ret = _commandData[opcode] as List<InquiryResult>;
        _commandData.Remove(opcode);
      }

      return ret;
    }

    public string GetRemoteName(InquiryResult device)
    {
      var data = new byte[10];
      var ret = String.Empty;

      data[0] = (byte)(device.BDAddr & 0xFF);
      data[1] = (byte)((device.BDAddr & 0xFF00) >> 8);
      data[2] = (byte)((device.BDAddr & 0xFF0000) >> 16);
      data[3] = (byte)((device.BDAddr & 0xFF000000) >> 24);
      data[4] = (byte)((device.BDAddr & 0xFF00000000) >> 32);
      data[5] = (byte)((device.BDAddr & 0xFF0000000000) >> 40);
      data[6] = device.PageScanRepetitionMode;
      data[8] = (byte)(device.ClockOffset & 0xFF);
      data[9] = (byte)((byte)((device.ClockOffset & 0xFF00) >> 8) | (byte)0x80);

      var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.RemoteNameRequest);
      _SendHCICommand(opcode, data);

      if (_commandData.ContainsKey(opcode))
      {
        ret = _commandData[opcode] as string;
        _commandData.Remove(opcode);
      }

      return ret;
    }

    public void SetDiscoverableMode(bool discoverable)
    {
      var opcode = new Opcode(OpcodeGroupField.HCBaseband, OpcodeCommandField.WriteScanEnable);
      _SendHCICommand(opcode, new byte[] {discoverable ? (byte)0x03 : (byte)0x01});
    }

    public void SetLocalName(string name)
    {
      var data = new byte[name.Length + 1];
      Array.Copy(ASCIIEncoding.ASCII.GetBytes(name), data, name.Length);

      var opcode = new Opcode(OpcodeGroupField.HCBaseband, OpcodeCommandField.WriteLocalName);
      _SendHCICommand(opcode, data);
    }

    public void SetDeviceClass(uint deviceClass)
    {
      var data = new byte[3];

      data[0] = (byte)(deviceClass & 0xFF);
      data[1] = (byte)((deviceClass >> 8) & 0xFF);
      data[2] = (byte)((deviceClass >> 16) & 0xFF);

      var opcode = new Opcode(OpcodeGroupField.HCBaseband, OpcodeCommandField.WriteDeviceClass);
      _SendHCICommand(opcode, data);
    }

    public void SendACLData(byte[] buffer, int offset, int count)
    {
      int transferred;
      _writer.Write(buffer, offset, count, 10000, out transferred);

      lock (_writer)
      {
        Logger.LogData('I', buffer, offset, count);
      }
    }

    public void AcceptConnectionRequest(ulong bdAddr, byte role)
    {
      var data = new byte[7];

      data[0] = (byte)(bdAddr & 0xFF);
      data[1] = (byte)((bdAddr & 0xFF00) >> 8);
      data[2] = (byte)((bdAddr & 0xFF0000) >> 16);
      data[3] = (byte)((bdAddr & 0xFF000000) >> 24);
      data[4] = (byte)((bdAddr & 0xFF00000000) >> 32);
      data[5] = (byte)((bdAddr & 0xFF0000000000) >> 40);
      data[6] = role;

      var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.AcceptConnectionRequest);
      _SendHCICommand(opcode, data, true);
    }

    public void Connect(InquiryResult result)
    {
      Connect(result.BDAddr, result.PageScanRepetitionMode, result.ClockOffset);
    }

    public void Connect(ulong bdAddr, byte pageScanRepetitionMode, ushort clockOffset)
    {
      var data = new byte[13];
      
      data[0] = (byte)(bdAddr & 0xFF);
      data[1] = (byte)((bdAddr & 0xFF00) >> 8);
      data[2] = (byte)((bdAddr & 0xFF0000) >> 16);
      data[3] = (byte)((bdAddr & 0xFF000000) >> 24);
      data[4] = (byte)((bdAddr & 0xFF00000000) >> 32);
      data[5] = (byte)((bdAddr & 0xFF0000000000) >> 40);
      data[6] = (byte)0x18;
      data[7] = (byte)0x00;
      data[8] = pageScanRepetitionMode;
      data[9] = 0x00;
      data[10] = (byte)(clockOffset & 0xFF);
      data[11] = (byte)(((clockOffset >> 8) & 0xFF) | 0x80);
      data[12] = 0x00;

      var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.CreateConnection);
      _SendHCICommand(opcode, data, true);
    }

    //NOTE: This doesn't work if you send a PIN, not sure why.
    public void SendPINCodeReply(ulong bdAddr, string pin)
    {
      Opcode opcode;
      var data = new byte[6 + (String.IsNullOrEmpty(pin) ? 0 : pin.Length + 1)];

      data[0] = (byte)(bdAddr & 0xFF);
      data[1] = (byte)((bdAddr & 0xFF00) >> 8);
      data[2] = (byte)((bdAddr & 0xFF0000) >> 16);
      data[3] = (byte)((bdAddr & 0xFF000000) >> 24);
      data[4] = (byte)((bdAddr & 0xFF00000000) >> 32);
      data[5] = (byte)((bdAddr & 0xFF0000000000) >> 40);
      if (!String.IsNullOrEmpty(pin))
      {
        data[6] = (byte)pin.Length;
        for (int i = 0; i < pin.Length; i++)
          data[7 + i] = (byte)pin[i];

        opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.SendPINCodeRequestReply);
      }
      else
        opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.SendPINCodeRequestNegativeReply);

      _SendHCICommand(opcode, data, true);
    }

    public void Close()
    {
      if (_device != null)
      {
        if (_device.IsOpen)
        {
          IUsbDevice whole = _device as IUsbDevice;

          if (!ReferenceEquals(whole, null))
          {
            whole.ReleaseInterface(1);
          }

          _device.Close();
          UsbDevice.Exit();
        }
      }
    }

    #endregion

    #region Event Handlers

    private void hci_DataReceived(object sender, EndpointDataEventArgs e)
    {
      switch ((HCIEvent)e.Buffer[0])
      {
        case HCIEvent.InquiryComplete:
          {
            lock (_completedCommands)
            {
              _completedCommands.Add(new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.Inquiry));
            }

            break;
          }
        case HCIEvent.InquiryResult:
          {
            var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.Inquiry);
            if (!_commandData.ContainsKey(opcode)) _commandData.Add(opcode, new List<InquiryResult>());
            var list = _commandData[opcode] as List<InquiryResult>;

            int responses = Convert.ToInt32(e.Buffer[2]);
            int offset = 3;
            for (int i = 0; i < responses; i++)
            {
              list.Add(new InquiryResult(Utilities.GetLEULong(e.Buffer, offset + 0, 6), e.Buffer[offset + 6],
                e.Buffer[offset + 7], e.Buffer[offset + 8], (uint)Utilities.GetLEULong(e.Buffer, offset + 9, 3),
                (ushort)Utilities.GetLEULong(e.Buffer, offset + 12, 2)));
              offset += 14;
            }

            break;
          }
        case HCIEvent.ConnectionComplete:
          {
            Logger.WriteLine("Connection Complete, Status: " + e.Buffer[2].ToString("X2"));
            ushort connectionHandle = (ushort)(e.Buffer[3] | (e.Buffer[4] << 8));
            ulong bdAddr = Utilities.GetLEULong(e.Buffer, 5, 6);

            //Raise event out
            if (ConnectionComplete != null)
              ConnectionComplete(this, new HCIEventEventArgs(e.Buffer, e.Count));

            break;
          }
        case HCIEvent.DisconnectionComplete:
          {
            Logger.WriteLine("Disconnection Complete, Status: " + e.Buffer[2].ToString("X2"));

            break;
          }
        case HCIEvent.ConnectionRequest:
          {
            var bdAddr = Utilities.GetLEULong(e.Buffer, 2, 6);
            var deviceClass = Utilities.GetLEULong(e.Buffer, 8, 3);
            byte linkType = e.Buffer[11];
            Logger.WriteLine(String.Format("Connection Request Received, BD_ADDR {0}, class {1}, link type {2}",
              bdAddr.ToString("X12"), deviceClass.ToString("X6"), linkType.ToString("X2")));

            //Accept this request (or do whatever with it)
            if (ConnectionRequestReceived != null)
              ConnectionRequestReceived(this, new HCIEventEventArgs(e.Buffer, e.Count));

            break;
          }
        case HCIEvent.RemoteNameRequestComplete:
          {
            var opcode = new Opcode(OpcodeGroupField.LinkControl, OpcodeCommandField.RemoteNameRequest);
            if (!_commandData.ContainsKey(opcode))
              _commandData.Add(opcode, ASCIIEncoding.ASCII.GetString(e.Buffer, 9, 248).Trim(new char[] {'\0'} ));

            lock (_completedCommands)
            {
              _completedCommands.Add(opcode);
            }

            break;
          }
        case HCIEvent.QoSSetupComplete:
          {
            Logger.WriteLine("QoS Setup Complete");

            break;
          }
        case HCIEvent.Complete:
          {
            var command = new Opcode(e.Buffer, 3);

            lock (_completedCommands)
            {
              _completedCommands.Add(command);
            }

            break;
          }
        case HCIEvent.Status:
          {
            var command = new Opcode(e.Buffer, 4);

            break;
          }
        case HCIEvent.PINCodeRequestEvent:
          {
            var bdAddr = Utilities.GetLEULong(e.Buffer, 2, 6);

            //Send the reply
            SendPINCodeReply(bdAddr, String.Empty);

            break;
          }
        default:
          {
            //Uh?
            Logger.WriteLine("Unknown HCI event: " + e.Buffer[0].ToString("X2"));

            break;
          }
      }
    }

    private void reader_DataReceived(object sender, EndpointDataEventArgs e)
    {
      Logger.WriteLine("Incoming: " + BitConverter.ToString(e.Buffer, 0, e.Count));
      lock (_writer)
      {
        Logger.LogData('O', e.Buffer, 0, e.Count);
      }

      if (IncomingDataReceived != null)
        IncomingDataReceived(this, new HCIEventEventArgs(e.Buffer, e.Count));
    }

    private void _isoReader_DataReceived(object sender, EndpointDataEventArgs e)
    {
      Logger.WriteLine("Incoming isochronous data: " + BitConverter.ToString(e.Buffer, 0, e.Count));
    }

    #endregion

    #region Local Methods

    private void _SendHCICommand(Opcode command)
    {
      _SendHCICommand(command, false);
    }

    private void _SendHCICommand(Opcode command, bool returnImmediately)
    {
      _SendHCICommand(command, null, returnImmediately);
    }

    private void _SendHCICommand(Opcode command, byte[] parameterData)
    {
      _SendHCICommand(command, parameterData, false);
    }

    private void _SendHCICommand(Opcode command, byte[] parameterData, bool returnImmediately)
    {
      var cmdData = new byte[3 + (parameterData != null ? parameterData.Length : 0)];
      var packet = new UsbSetupPacket(0x20, 0x00, 0x0000, 0x0000, (short)cmdData.Length);

      cmdData[0] = (byte)(command.Data & 0xFF);
      cmdData[1] = (byte)((command.Data >> 8) & 0xFF);
      if (parameterData != null && parameterData.Length > 0)
      {
        cmdData[2] = (byte)parameterData.Length;
        for (int i = 0; i < parameterData.Length; i++)
          cmdData[3 + i] = parameterData[i];
      }

      int transferred;
      _device.ControlTransfer(ref packet, cmdData, cmdData.Length, out transferred);
      if (transferred != cmdData.Length)
        throw new InvalidOperationException(String.Format("Failed to send command; sent {0} bytes instead of {1}",
          transferred, cmdData.Length));

      if (!returnImmediately)
      {
        //Wait for command to complete
        _WaitForCompletion(command);
      }
    }

    private void _WaitForCompletion(Opcode command)
    {
      while (true)
      {
        lock (_completedCommands)
        {
          if (_completedCommands.Contains(command))
          {
            _completedCommands.Remove(command);
            break;
          }
        }

        Thread.Sleep(10);
      }
    }

    #endregion
  }
}
