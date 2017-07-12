using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DocumentConsole.Models
{
  public interface IEntity
  {
    [JsonProperty(PropertyName = "id")]
    string Id { get; set; }
    string Type { get; set; }
    bool IsBaseEntity { get; set; }
    string Document { get; set; }
    List<IEntity> Parents { get; set; }
    string Name { get; set; }
    JToken Properties { get; set; }
  }
}
