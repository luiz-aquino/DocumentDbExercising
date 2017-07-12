using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DocumentConsole.Models
{
  public class Phone
  {
    public string Ddd { get; set; }
    public string Number { get; set; }
    public string FullNumber { get; set; }
  }
}
