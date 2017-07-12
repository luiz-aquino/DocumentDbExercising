using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;

namespace DocumentConsole
{
  public class MenuOption
  {
    public string Id { get; set; }
    public string Description { get; set; }
    public Func<DocumentClient, Uri, Task> Action { get; set; }
  }
}
