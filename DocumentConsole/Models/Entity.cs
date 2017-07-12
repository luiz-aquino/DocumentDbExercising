using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DocumentConsole.Models
{
  public class Entity: IEntity
  {
    public string Id { get; set; }
    public string Type { get; set; }
    public bool IsBaseEntity { get; set; }
    public string Document { get; set; }
    public List<IEntity> Parents { get; set; }
    public string Name { get; set; }
    public JToken Properties { get; set; }
  }
}
