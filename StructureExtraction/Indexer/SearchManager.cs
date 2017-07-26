using Microsoft.Extensions.Configuration;

namespace Indexer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Search;
    using Microsoft.Azure.Search.Models;

    public class SearchManager
    {
        public SearchManager()
        {
        }

        public static void BuildIndex(string indexName, IDictionary<string, string>[] documents)
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();

            SearchServiceClient serviceClient = CreateSearchServiceClient(configuration);

            Console.WriteLine($"Deleting index {indexName}...\n");
            DeleteIndexIfExists(serviceClient, indexName);

            Console.WriteLine($"Creating index {indexName}...\n");
            CreateIndex(serviceClient, indexName,
                documents.SelectMany(d => d.Keys).Select(f => new Field(f, DataType.String)).ToList());

            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexName);

            Console.WriteLine("{0}", "Uploading documents...\n");
            UploadDocuments(indexClient, documents);
        }

        private static SearchServiceClient CreateSearchServiceClient(IConfigurationRoot configuration)
        {
            string searchServiceName = configuration["SearchServiceName"];
            string adminApiKey = configuration["SearchServiceAdminApiKey"];

            SearchServiceClient serviceClient = new SearchServiceClient(searchServiceName,
                new SearchCredentials(adminApiKey));
            return serviceClient;
        }

        private static SearchIndexClient CreateSearchIndexClient(IConfigurationRoot configuration)
        {
            string searchServiceName = configuration["SearchServiceName"];
            string queryApiKey = configuration["SearchServiceQueryApiKey"];

            SearchIndexClient indexClient = new SearchIndexClient(searchServiceName, "hotels",
                new SearchCredentials(queryApiKey));
            return indexClient;
        }

        private static void DeleteIndexIfExists(SearchServiceClient serviceClient, string indexName)
        {
            if (serviceClient.Indexes.Exists(indexName))
            {
                serviceClient.Indexes.Delete(indexName);
            }
        }

        private static void CreateIndex(SearchServiceClient serviceClient, string indexName, IList<Field> fields)
        {
            var definition = new Index
            {
                Name = indexName,
                Fields = fields
            };

            serviceClient.Indexes.Create(definition);
        }

        private static void UploadDocuments(ISearchIndexClient indexClient,
            IEnumerable<IDictionary<string, string>> documents)
        {
            var docs = documents.Select(d =>
            {
                var doc = new Document();
                foreach (var field in d.Keys)
                {
                    doc[field] = d[field];
                }
                return doc;
            });

            var batch = IndexBatch.Upload(docs);

            try
            {
                indexClient.Documents.Index(batch);
            }
            catch (IndexBatchException e)
            {
                // Sometimes when your Search service is under load, indexing will fail for some of the documents in
                // the batch. Depending on your application, you can take compensating actions like delaying and
                // retrying. For this simple demo, we just log the failed document keys and continue.
                Console.WriteLine(
                    "Failed to index some of the documents: {0}",
                    String.Join(", ", e.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key)));
            }

            Console.WriteLine("Waiting for documents to be indexed...\n");
            Thread.Sleep(2000);
        }
    }
}