using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DocumentConsole.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DocumentConsole
{
  public class Program
  {
    private static readonly string Url = ConfigurationManager.AppSettings["url"];
    private static readonly string Key = ConfigurationManager.AppSettings["key"];
    private static readonly string DatabaseId = ConfigurationManager.AppSettings["databasename"];
    private static readonly string CollectionId = ConfigurationManager.AppSettings["collectionname"];
    private static readonly int NumTasks = 8;
    private static readonly Dictionary<int, double> RUs = new Dictionary<int, double>();
    private static readonly Dictionary<int, int> DocumentsInserted = new Dictionary<int, int>();
    private static int _entitiesToCreate = 10000;
    private static int _requestsPending;
    private static readonly Dictionary<string, StoredProcedure> sprocs = new Dictionary<string, StoredProcedure>();


    public static void Main(string[] args)
    {
      JsonConvert.DefaultSettings = () => new JsonSerializerSettings
      {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Objects,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
      };

      Console.WriteLine("Creating client...");
     
      using (var client = new DocumentClient(new Uri(Url), Key, new ConnectionPolicy { ConnectionProtocol = Protocol.Tcp, ConnectionMode = ConnectionMode.Direct }))
      {
        client.OpenAsync().Wait();
        Console.WriteLine("Client Created");
        RunProgramAsync(client, DatabaseId, CollectionId).Wait();
      }
    }

    private static async Task<DocumentCollection> GetOrCreateCollectionAsync(DocumentClient client, string databaseId, string collectionId)
    {
      var collectionDefinition = new DocumentCollection
      {
        Id = collectionId,
        IndexingPolicy =
          new IndexingPolicy(new RangeIndex(DataType.String) { Precision = -1 }) { IndexingMode = IndexingMode.Consistent }
      };

      return await client.CreateDocumentCollectionIfNotExistsAsync(
        UriFactory.CreateDatabaseUri(databaseId),
        collectionDefinition,
        new RequestOptions { OfferThroughput = 400 });
    }

    private static async Task RunProgramAsync(DocumentClient client, string databaseId, string colletionId)
    {
      Console.WriteLine("Creating database...");
      await client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseId });
      Console.WriteLine("Database created, creating collection...");
      await GetOrCreateCollectionAsync(client, databaseId, colletionId);

      var collectionUri = UriFactory.CreateDocumentCollectionUri(databaseId, colletionId);

      Console.WriteLine("Creating system procedures...");
      await CreateProcedures(client, collectionUri);

      Console.WriteLine("-----------------------------------------------------------------------------------------");
      Console.WriteLine("Welcome, what do you want to do?");

      var code = string.Empty;
      var options = GetMenuOptions();
      while (code != "666")
      {
        foreach (var option in options)
        {
          Console.WriteLine($"{option.Id} - {option.Description}");
        }

        Console.WriteLine("666 - Sair");

        code = Console.ReadLine();

        if (code == "666") continue;

        var selected = options.FirstOrDefault(x => x.Id == code);

        if (selected != null)
        {
          await selected.Action(client, collectionUri);
        }
      }
    }

    private static List<MenuOption> GetMenuOptions()
    {
      var options = new List<MenuOption>
      {
        new MenuOption
        {
          Id = "1",
          Description = "Generate data",
          Action =  GenerateData
        },
        new MenuOption
        {
          Id = "2",
          Description = "Query data",
          Action = QueryDataParallel
        },
        new MenuOption
        {
          Id = "3",
          Description = "GenerateSampleData",
          Action = GenerateSampleData
        },
        new MenuOption
        {
          Id= "4",
          Description = "Test with string json",
          Action = InsertTry
        }
      };
      
      return options;
    }

    public static async Task QueryDataParallel(DocumentClient client, Uri collectionUri)
    {
      Console.WriteLine("-----------------------------------------------------------------------------------------");
      Console.WriteLine("Type a query:");
      var query = Console.ReadLine();

      Console.WriteLine("-----------------------------------------------------------------------------------------");

      var options = new FeedOptions
      {
        MaxDegreeOfParallelism = 4,
        MaxBufferedItemCount = 1000,
        EnableCrossPartitionQuery = true
      };

      var sw = new Stopwatch();

      try
      {
        sw.Start();
        var result = client.CreateDocumentQuery<string>(collectionUri, query, options).AsDocumentQuery();
        var rus = new List<double>();
        while (result.HasMoreResults)
        {
          var documents = await result.ExecuteNextAsync();
          rus.Add(documents.RequestCharge);
          foreach (var document in documents)
          {
            Console.WriteLine(document);
          }
        }
        sw.Stop();

        Console.WriteLine();
        Console.WriteLine($"This query took {sw.ElapsedMilliseconds / 1000} seconds");
        Console.WriteLine($"each access used those RUs respectively { string.Join(", ", rus) }");

      }
      catch (Exception e)
      {
        Console.WriteLine($"Failed to execute query: {e.Message}");
      }

      Console.WriteLine("--------------------------------------------------------------------------------------------------");
    }

    private static async Task GenerateData(DocumentClient client, Uri collecctionUri)
    {
      Console.WriteLine("How much?");
      var value = Console.ReadLine();

      int temp;

      if (!int.TryParse(value, out temp))
      {
        Console.WriteLine("Value is not a number");
        return;
      }

      if (temp < 1)
      {
        Console.WriteLine("Value is less than 1");
        return;
      }

      _entitiesToCreate = temp;

      var tasks = new List<Task>();
      _requestsPending = NumTasks;

      Console.WriteLine($"Creating {NumTasks} tasks to create the documents");

      for (var i = 0; i < NumTasks; i++)
      {
        tasks.Add(InsertDocuments(client, collecctionUri, i));
      }

      tasks.Add(LogDataGenerationStats());

      await Task.WhenAll(tasks);
    }

    private static async Task InsertDocuments(DocumentClient client, Uri collectionUri, int id)
    {
      DocumentsInserted[id] = 0;
      var data = new Dictionary<string, string>();
      for (int i = 0, qtd = (_entitiesToCreate / NumTasks); i < qtd; i++)
      {
        try
        {
          var response = await ExecuteWithRetry(() => client.CreateDocumentAsync(collectionUri, GetModel(data), new RequestOptions()));

          data["id"] = null;

          RUs[id] = response.RequestCharge;

          DocumentsInserted[id]++;
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
      }

      Interlocked.Decrement(ref _requestsPending);
    }

    private static Dictionary<string, string> GetModel(Dictionary<string, string> data)
    {
      for (var i = 0; i < 20; i++)
      {
        data[$"prop{i}"] = Guid.NewGuid().ToString().PadRight(200, '0');
      }

      return data;
    }

    private static async Task<TV> ExecuteWithRetry<TV>(Func<Task<TV>> function)
    {
      while (true)
      {
        TimeSpan sleep;
        try
        {
          return await function();
        }
        catch (DocumentClientException dce)
        {
          if ((int?)dce.StatusCode != 429) throw;

          sleep = dce.RetryAfter;
        }
        catch (AggregateException age)
        {
          if (!(age.InnerException is DocumentClientException)) throw;

          var dce = (DocumentClientException)age.InnerException;
          if ((int?)dce?.StatusCode != 429) throw;

          sleep = dce.RetryAfter;
        }

        Console.WriteLine($"Error 429 retrying in {sleep.TotalMilliseconds} milis");
        await Task.Delay(sleep);
      }
    }

    private static async Task LogDataGenerationStats()
    {
      while (_requestsPending > 0)
      {
        await Task.Delay(TimeSpan.FromSeconds(3));
        Console.WriteLine(
          $"Remaining {_requestsPending} tasks. and {DocumentsInserted.Select(x => x.Value).Sum()} documents were created. {RUs.Select(x => x.Value).FirstOrDefault()}");
      }

      Console.WriteLine("------------------------------------------------------------------------------------------------------------");
    }


    private static async Task GenerateSampleData(DocumentClient client, Uri collectionUri)
    {
      var mainEntityDocument = Guid.NewGuid().ToString("N");

      Console.WriteLine("Creating main entity...");

      var mainEntity = new Entity
      {
        IsBaseEntity = true,
        Document = mainEntityDocument,
        Name = "Main",
        Type = "entity"
      };

      var sponsors = new List<IEntity>();
      var parents = new List<IEntity>() { mainEntity };

      Console.WriteLine("Creating sponsors....");

      for (var i = 0; i < 10; i++)
      {
        var curr = new Entity
        {
          Document = Guid.NewGuid().ToString("N"),
          Name = $"Sponsor{i}",
          Type = "entity"
        };

        await AddEntity(client, collectionUri, curr, parents);

        sponsors.Add(curr);
      }

      var contacts = new List<IEntity>();

      Console.WriteLine("Creating clients....");

      foreach (var sponsor in sponsors)
      {
        parents.Clear();
        parents.Add(sponsor);
        for (var i = 0; i < 100; i++)
        {
          var number = Guid.NewGuid().ToString("N").Substring(0, 11);
          var number2 = Guid.NewGuid().ToString("N").Substring(0, 11);

          var curr = new Contact
          {
            Document = Guid.NewGuid().ToString("N"),
            Name = $"Cliente{i}_{sponsor.Name}",
            Phones = new List<Phone> { new Phone { Ddd = number.Substring(0, 2), Number = number.Substring(2), FullNumber = number }, new Phone { Ddd = number2.Substring(0, 2), Number = number2.Substring(2), FullNumber = number2 } }
          };

          await AddEntity(client, collectionUri, curr, parents);

          contacts.Add(curr);
        }

        Console.WriteLine($"{contacts.Count} contacts created....");
      }
      Console.WriteLine("Creating history....");
      for (var i = 0; i < 100; i++)
      {
        foreach (var contact in contacts)
        {
          var curr = new History
          {
            Document = contact.Document,
            Type = "history",
            Name = $"History{i}_{contact.Name}",
            Date = DateTime.Now.AddMinutes(i)
          };

          await AddEntity(client, collectionUri, curr);
        }
        Console.WriteLine($"{contacts.Count * i} histories created....");
      }

    }
    //response > resource > id
    private static async Task AddEntity(DocumentClient client, Uri collectionUri, IEntity entity, List<IEntity> parents = null)
    {
      var response = await ExecuteWithRetry(() => client.UpsertDocumentAsync(collectionUri, entity));

      var id = response.Resource.Id;//Auto generated id

      if (parents != null && parents.Any())
      {
        foreach (var parent in parents)
        {
          entity.Parents = new List<IEntity>
          {
            new Entity
            {
              Id = parent.Id
            }
          };

          await ExecuteWithRetry(() => client.UpsertDocumentAsync(collectionUri, entity));
        }
      }

      entity.Parents = null;
      entity.Id = id;
    }

    private static async Task InsertTry(DocumentClient client, Uri collectionUri)
    {
      var directory = Directory.GetCurrentDirectory();

      Console.WriteLine("Filename (must be a json)");

    }

    private static async Task CreateProcedures(DocumentClient client, Uri collectionUri)
    {
      foreach (var file in Directory.GetFiles(@"Procedures"))
      {
        var scriptId = Path.GetFileNameWithoutExtension(file);

        if(scriptId == null) continue;

        Console.WriteLine($"Creating procedure {scriptId}...");

        var sproc = new StoredProcedure
        {
          Id = scriptId,
          Body = File.ReadAllText(file)
        };

        await TryDeleteProcedure(client, collectionUri, scriptId);
        sproc = await client.CreateStoredProcedureAsync(collectionUri, sproc);

        sprocs[scriptId] = sproc;
      }
    }

    private static async Task TryDeleteProcedure(DocumentClient client, Uri collectionUri, string sprocId)
    {
      var sproc = client.CreateStoredProcedureQuery(collectionUri)
        .Where(s => s.Id == sprocId).AsEnumerable().FirstOrDefault();

      if (sproc == null) return;

      await client.DeleteStoredProcedureAsync(sproc.SelfLink);
    }
  }
}

