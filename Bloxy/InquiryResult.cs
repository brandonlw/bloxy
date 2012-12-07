using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public class InquiryResult
  {
    #region Declarations

    private ulong _bdAddr;
    private byte _pageScanRepititionMode;
    private byte _reserved1;
    private byte _reserved2;
    private uint _deviceClass;
    private ushort _clockOffset;

    #endregion

    #region Constructors/Teardown

    public InquiryResult(ulong bdAddr, byte pageScanRepetitionMode,
      byte reserved1, byte reserved2, uint deviceClass, ushort clockOffset)
    {
      _bdAddr = bdAddr;
      _pageScanRepititionMode = pageScanRepetitionMode;
      _reserved1 = reserved1;
      _reserved2 = reserved2;
      _deviceClass = deviceClass;
      _clockOffset = clockOffset;
    }

    #endregion

    #region Public Properties

    public ulong BDAddr
    {
      get
      {
        return _bdAddr;
      }
    }

    public byte PageScanRepetitionMode
    {
      get
      {
        return _pageScanRepititionMode;
      }
    }

    public byte Reserved1
    {
      get
      {
        return _reserved1;
      }
    }

    public byte Reserved2
    {
      get
      {
        return _reserved2;
      }
    }

    public uint DeviceClass
    {
      get
      {
        return _deviceClass;
      }
    }

    public ushort ClockOffset
    {
      get
      {
        return _clockOffset;
      }
    }

    #endregion
  }
}
