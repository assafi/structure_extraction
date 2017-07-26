// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SearchManager.cs" company="Microsoft Corporation">
//  All Rights Reserved  
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace StructureExtraction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Search;
    using Microsoft.Azure.Search.Models;

    public class HackIndex
    {
        private readonly ISearchIndexClient indexClient;

        public HackIndex(ISearchIndexClient indexClient)
        {
            this.indexClient = indexClient;
        }

        public DocumentSearchResult Search(string query)
        {
            return this.indexClient.Documents.Search(query);
        }

        public DocumentSearchResult Facets(string field)
        {
            return this.indexClient.Documents.Search("*", new SearchParameters
            {
                Facets = new List<string> {field}
            });
        }

        public DocumentSearchResult Facets(string field, string query)
        {
            return this.indexClient.Documents.Search(query, new SearchParameters
            {
                Facets = new List<string> { field }
            });
        }

        public static HackIndex BuildIndex(string serviceName, string adminKey, string queryKey, 
            string indexName, Dictionary<string, string>[] documents, string keyField, ISet<string> filterFields, 
            ISet<string> facetableFields, ISet<string> searchableFields, ISet<string> sortableFields)
        {
            SearchServiceClient serviceClient = CreateSearchServiceClient(serviceName, adminKey);

            Console.WriteLine($"Deleting index {indexName}...");
            DeleteIndexIfExists(serviceClient, indexName);

            Console.WriteLine($"Creating index {indexName}...");
            CreateIndex(serviceClient, indexName, 
                documents.First().Keys.Select(f => new Field(f, DataType.String)).ToList(),
                keyField,
                filterFields,
                facetableFields,
                searchableFields,
                sortableFields);

            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexName);

            Console.WriteLine("Uploading documents...");
            UploadDocuments(indexClient, documents);

            return new HackIndex(CreateSearchServiceClient(serviceName, indexName, queryKey));
        }

        private static SearchServiceClient CreateSearchServiceClient(string serviceName, string serviceAdmingApiKey)
        {
            return new SearchServiceClient(serviceName, new SearchCredentials(serviceAdmingApiKey));
        }

        private static SearchIndexClient CreateSearchServiceClient(string serviceName, string indexName, string queryApiKey)
        {
            return new SearchIndexClient(serviceName, indexName, new SearchCredentials(queryApiKey));
        }

        private static void DeleteIndexIfExists(SearchServiceClient serviceClient, string indexName)
        {
            if (serviceClient.Indexes.Exists(indexName))
            {
                serviceClient.Indexes.Delete(indexName);
            }
        }

        private static void CreateIndex(SearchServiceClient serviceClient, string indexName, IList<Field> fields, string keyField, ISet<string> filterFields, ISet<string> facetableFields, ISet<string> searchableFields, ISet<string> sortableFields)
        {
            foreach (var field in fields)
            {
                if (field.Name == keyField)
                {
                    field.IsKey = true;
                }

                field.IsFilterable = filterFields.Contains(field.Name);
                field.IsFacetable = facetableFields.Contains(field.Name);
                field.IsSearchable = searchableFields.Contains(field.Name);
                field.IsSortable = sortableFields.Contains(field.Name);
            }

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
                var doc = new Microsoft.Azure.Search.Models.Document();
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
                    string.Join(", ", e.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key)));
            }

            Console.WriteLine("Waiting for documents to be indexed...\n");
            Thread.Sleep(2000);
        }
    }
}
