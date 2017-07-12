using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace DocumentDbConsole
{
  public class DocumentServices: IDisposable
  {
    private readonly DocumentClient _client;
    private Uri _collection;
    private Database _database;

    public DocumentServices(string url, string key, string databaseName)
    {
      _client = new DocumentClient(new Uri(url), key, new ConnectionPolicy { UserAgentSuffix = "db_console/"});

      this.initAll(databaseName).Wait();
    }

    public async Task initAll(string databaseName)
    {
      _database = await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });
      //_collection = await _client.CreateDocumentCollectionIfNotExistsAsync()

    }

    public async Task<string> RunQuery(string query)
    {
      var result = string.Empty;

      //_client.CreateDocumentQuery<Entidade>()


      return result;
    }

    public  sealed class Entidade
    {
      [JsonProperty(PropertyName = "id")]
      public string Id { get; set; }

      public string Document { get; set; }
    }

    public void Dispose()
    {
      _client?.Dispose();
    }
  }
}
