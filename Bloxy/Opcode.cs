using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public enum OpcodeGroupField
  {
    LinkControl = 0x01,
    HCIPolicy = 0x02,
    HCBaseband = 0x03,
    Information = 0x04,
    Status = 0x05,
    Testing = 0x06
  };

  public enum OpcodeCommandField
  {
    Inquiry = 0x01,
    Reset = 0x03,
    CreateConnection = 0x05,
    AcceptConnectionRequest = 0x09,
    SendPINCodeRequestReply = 0x0D,
    SendPINCodeRequestNegativeReply = 0x0E,
    WriteLocalName = 0x13,
    RemoteNameRequest = 0x19,
    WriteScanEnable = 0x1A,
    WriteDeviceClass = 0x24
  };

  public class Opcode
  {
    #region Declarations

    private OpcodeGroupField _ogf;
    private OpcodeCommandField _ocf;

    #endregion

    #region Constructors/Teardown

    public Opcode(OpcodeGroupField ogf, OpcodeCommandField ocf)
    {
      _ogf = ogf;
      _ocf = ocf;
    }

    public Opcode(ushort ogf, ushort ocf)
    {
      _ogf = (OpcodeGroupField)ogf;
      _ocf = (OpcodeCommandField)ocf;
    }

    public Opcode(byte[] buffer, int offset)
    {
      _ogf = (OpcodeGroupField)(buffer[offset + 1] >> 2);
      _ocf = (OpcodeCommandField)(((buffer[offset + 1] & 0x03) << 8) | (buffer[offset + 0]));
    }

    public Opcode(ushort data)
    {
      _ogf = (OpcodeGroupField)(data >> 10);
      _ocf = (OpcodeCommandField)(data & 0x03FF);
    }

    #endregion

    #region Public Properties

    public OpcodeGroupField Ogf
    {
      get
      {
        return _ogf;
      }
    }

    public OpcodeCommandField Ocf
    {
      get
      {
        return _ocf;
      }
    }

    public ushort Data
    {
      get
      {
        //Overkill much?
        return (ushort)((ushort)(((ushort)(_ogf) << 10)) | ((ushort)_ocf));
      }
    }

    #endregion

    #region Overrides

    public override bool Equals(object obj)
    {
      Opcode o = obj as Opcode;
      bool ret = false;

      if (this == o)
      {
        ret = true;
      }
      else if (this != null && obj != null)
      {
        ret = (this.Ogf == o.Ogf && this.Ocf == o.Ocf);
      }

      return ret;
    }

    public static bool operator ==(Opcode a, Opcode b)
    {
      if (System.Object.ReferenceEquals(a, b))
        return true;

      if (((object)a == null) || ((object)b == null))
        return false;

      return a.Ogf == b.Ogf && a.Ocf == b.Ocf;
    }

    public static bool operator !=(Opcode a, Opcode b)
    {
      return !(a == b);
    }

    public override int GetHashCode()
    {
      return this.Data;
    }

    public override string ToString()
    {
      return this.Data.ToString("X4");
    }

    #endregion
  }
}
