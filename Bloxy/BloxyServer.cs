using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Bloxy
{
  public class BloxyServer
  {
    #region Declarations

    private string _server;
    private int _inport;
    private int _outport;
    private ushort _currentConnectionHandle;
    private Thread _listenThread;
    private TcpListener _listener;
    private TcpClient _client;

    #endregion

    #region Constructors/Teardown

    public BloxyServer(string server, int inport, int outport)
    {
      _server = server;
      _inport = inport;
      _outport = outport;
    }

    #endregion

    #region Public Methods

    public void Start()
    {
      try { Stop(); }
      catch { /* Whatever... */ }

      //Set up events
      Properties.Adapter.ConnectionComplete += adapter_ConnectionComplete;
      Properties.Adapter.ConnectionRequestReceived += adapter_ConnectionRequestReceived;
      Properties.Adapter.IncomingDataReceived += adapter_IncomingDataReceived;

      _listener = new TcpListener(IPAddress.Any, _inport);
      _listener.Start();

      _listenThread = new Thread(new ThreadStart(_Listen));
      _listenThread.IsBackground = true;
      _listenThread.Start();
    }

    public void Stop()
    {
      //Tear down events
      Properties.Adapter.ConnectionComplete -= adapter_ConnectionComplete;
      Properties.Adapter.ConnectionRequestReceived -= adapter_ConnectionRequestReceived;
      Properties.Adapter.IncomingDataReceived -= adapter_IncomingDataReceived;

      if (_listenThread != null)
      {
        _listenThread.Abort();
        _listenThread = null;
      }

      if (_listener != null)
      {
        _listener.Stop();
        _listener = null;
      }
    }

    #endregion

    private void _Listen()
    {
      while (true)
      {
        var client = _listener.AcceptTcpClient();

        var th = new Thread(new ParameterizedThreadStart(_HandleMessages));
        th.IsBackground = true;
        th.Start(client);

        Thread.Sleep(100);
      }
    }

    private void _HandleMessages(object client)
    {
      try
      {
        var c = client as TcpClient;
        var stream = c.GetStream();

        while (true)
        {
          //Get message prefixed with 4-byte length field
          var length = new byte[4];
          int bytesRead = stream.Read(length, 0, 4);
          if (bytesRead <= 0)
            break; //Fall over and die if we get something weird
          ulong l = (ulong)((ulong)length[0] | (ulong)((ulong)(length[1] << 8) & 0xFF00) |
            (ulong)((ulong)(length[2] << 16) & 0xFF0000) | (ulong)((length[3] << 24) & 0xFF000000));
          var buffer = new byte[l];
          stream.Read(buffer, 0, (int)l);

          //Handle message
          switch (buffer[0])
          {
            case (byte)'I':
              {
                //Incoming ACL data from the other PC/device received
                Logger.WriteLine("Network: Received ACL data: " + BitConverter.ToString(buffer, 1, buffer.Length - 1));

                //Change the connection handle to our adapter's
                buffer[1] = (byte)(_currentConnectionHandle & 0xFF);
                byte temp = (byte)(buffer[2] & 0xF0);
                buffer[2] = (byte)((byte)((_currentConnectionHandle >> 8) & 0xFF) | (byte)temp);

                //Send it on
                Logger.WriteLine("Sending ACL data: " + BitConverter.ToString(buffer, 1, buffer.Length - 1));
                Properties.Adapter.SendACLData(buffer, 1, buffer.Length - 1);

                break;
              }
            case (byte)'C':
              {
                //The other PC/device completed connecting
                ushort connectionHandle = (ushort)(buffer[4] | (buffer[5] << 8));
                ulong bdAddr = Utilities.GetLEULong(buffer, 6, 6);

                //We only care if the device we're emulating has completed connecting
                if (bdAddr == Properties.EmulatedConfiguration.BDAddr)
                {
                  Logger.WriteLine("Network: Received connection complete");

                  //Accept this connection request
                  Properties.Adapter.AcceptConnectionRequest(Properties.RealConfiguration.BDAddr, 0x01);
                }

                break;
              }
            case (byte)'R':
              {
                //The other PC/device received a connection request
                Logger.WriteLine("Network: Received connection request");

                //We must now establish the connection on our end
                Properties.Adapter.Connect(Properties.RealConfiguration.BDAddr,
                  Properties.RealConfiguration.PageScanRepetitionMode, Properties.RealConfiguration.ClockOffset);

                break;
              }
            default:
              {
                Logger.WriteLine("Network: Unknown command received: " + ((char)buffer[0]).ToString());
                break;
              }
          }

          Thread.Sleep(100);
        }
      }
      catch
      {
        //Whatever...
      }
    }

    private void adapter_IncomingDataReceived(object sender, HCIEventEventArgs e)
    {
      //Let the other PC/device know we have incoming data
      _Send('I', e.Buffer);
    }

    private void adapter_ConnectionRequestReceived(object sender, HCIEventEventArgs e)
    {
      //Let the other PC/device know we received a connection request
     _Send('R', e.Buffer);
    }

    private void adapter_ConnectionComplete(object sender, HCIEventEventArgs e)
    {
      //The real device's connection is complete, save its handle
      _currentConnectionHandle = (ushort)(e.Buffer[3] | (e.Buffer[4] << 8));

      //Let the other PC/device know we're done
      _Send('C', e.Buffer);
    }

    private void _Send(char command, byte[] message)
    {
      //Build the whole buffer
      var msg = new byte[message.Length + 1];
      msg[0] = (byte)command;
      Array.Copy(message, 0, msg, 1, message.Length);

      //Connect if we need to
      if (_client == null)
      {
        _client = new TcpClient();
        _client.Connect(_server, _outport);
      }

      //Put the length in front of it (needs to be combined with above, I know)
      var m = new byte[msg.Length + 4];
      m[0] = (byte)(msg.Length & 0xFF);
      m[1] = (byte)((msg.Length >> 8) & 0xFF);
      m[2] = (byte)((msg.Length >> 16) & 0xFF);
      m[3] = (byte)((msg.Length >> 24) & 0xFF);
      Array.Copy(msg, 0, m, 4, msg.Length);

      //Send it off
      var stream = _client.GetStream();
      stream.Write(m, 0, m.Length);
      stream.Flush();
    }
  }
}
