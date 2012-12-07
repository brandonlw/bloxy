using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public static class Logger
  {
    public const bool _DO_LOGGING = true;
    public const bool _LOG_DATA = true;

    public static void WriteLine()
    {
      Logger.WriteLine(String.Empty);
    }

    public static void WriteLine(string message)
    {
      if (_DO_LOGGING)
        Console.WriteLine(message);
    }

    public static void LogData(char cmd, byte[] data, int offset, int count)
    {
      if (_LOG_DATA)
        File.AppendAllText("log.txt", cmd + ": " +
          BitConverter.ToString(data, offset, count) + Environment.NewLine);
    }
  }
}
