using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bloxy
{
  public class InquiryInfo
  {
    public InquiryResult Result { get; set; }
    public string RemoteName { get; set; }

    public InquiryInfo(InquiryResult result, string remoteName)
    {
      Result = result;
      RemoteName = remoteName;
    }
  }
}
