namespace StructureExtraction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.Configuration;
    using Microsoft.ProgramSynthesis.Utils;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var config = builder.Build();

            bool pause = bool.Parse(config["Pause"]);

            string trainingFolderPath = config["trainingFolderPath"];
            string testFolderPath = config["testFolderPath"];
            var trainingFolder = new DirectoryInfo(trainingFolderPath);

            var extractorPerAnnotationLabel = new Dictionary<string, StructureExtractor>();
            foreach (var annotationType in config.GetSection("AnnotationTypes").GetChildren())
            {
                var examples = new List<Tuple<string, uint, uint>>();

                var labelPrefixText = annotationType["PrefixString"];
                var labelFieldName = annotationType["Label"];

                Console.Out.WriteLine($"Reading training folder {trainingFolderPath}");
                foreach (var trainingFile in trainingFolder.EnumerateFiles())
                {
                    using (var sr = new StreamReader(trainingFile.OpenRead()))
                    {
                        var exampleContent = sr.ReadToEnd().ToLowerInvariant();

                        if (exampleContent.IndexOf(labelPrefixText, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        int startIndex = exampleContent.IndexOf(labelPrefixText, StringComparison.OrdinalIgnoreCase) + labelPrefixText.Length;
                        int endIndex = startIndex + exampleContent.Substring(startIndex).IndexOf("\n");
                        examples.Add(Tuple.Create(exampleContent, (uint)startIndex, (uint)endIndex));
                        Console.Out.WriteLine($"PROSE sample for {labelFieldName} {trainingFile} [{startIndex} , {endIndex} , \"{exampleContent.Substring(startIndex, endIndex - startIndex)}\"]");
                    }
                }

                var noneLabeledExamples = new List<string>();
                foreach (var testFile in new DirectoryInfo(testFolderPath).EnumerateFiles().Take(20))
                {
                    using (var sr = new StreamReader(testFile.OpenRead()))
                    {
                        noneLabeledExamples.Add(sr.ReadToEnd().ToLowerInvariant());
                    }
                }

                Console.Out.WriteLine();
                Console.Out.Write($"Learning extraction logic for '{labelFieldName}'...");

                var extractor = StructureExtractor.TrainExtractorAsync(labelFieldName, examples, noneLabeledExamples).Result;
                Console.Out.WriteLine(" Done");
                extractorPerAnnotationLabel[labelFieldName] = extractor;
                Console.Out.WriteLine();
            }

            if (pause)
            {
                Console.Out.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            Console.Out.WriteLine($"Processing documents from folder {testFolderPath}");
            var documents = new List<Document>();
            foreach (var testFile in new DirectoryInfo(testFolderPath).EnumerateFiles())
            {
                using (var sr = new StreamReader(testFile.OpenRead()))
                {
                    documents.Add(new Document
                    {
                        Id = testFile.Name.Replace(".txt",""),
                        Content = sr.ReadToEnd().ToLowerInvariant()
                    });
                }
            }

            foreach (var annotationLabel in extractorPerAnnotationLabel.Keys)
            {
                Console.Out.WriteLine($"Extracting '{annotationLabel}' from documents...");
                extractorPerAnnotationLabel[annotationLabel].Extract(documents).Wait();
                foreach (var d in documents)
                {
                    Console.Out.WriteLine($"File: {d.Id}, Extract: {d.Fields[annotationLabel]}");
                }

                if (pause)
                {
                    Console.Out.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }

            Console.Out.WriteLine("Indexing...");

            var filterableFields = GetAnnotationTypeWithProperty(extractorPerAnnotationLabel.Keys, "Filterable", config);
            var facetableFields = GetAnnotationTypeWithProperty(extractorPerAnnotationLabel.Keys, "Facetable", config);
            var sortableFields = GetAnnotationTypeWithProperty(extractorPerAnnotationLabel.Keys, "Sortable", config);
            sortableFields.Add("FileName");
            var searchableFields = GetAnnotationTypeWithProperty(extractorPerAnnotationLabel.Keys, "Searchable", config);
            searchableFields.Add("Content");

            var index = HackIndex.BuildIndex(
                config["SearchServiceName"],
                config["SearchServiceAdminApiKey"],
                config["SearchServiceQueryApiKey"],
                config["IndexName"],
                documents,
                "FileName",
                filterableFields,
                facetableFields,
                searchableFields,
                sortableFields
            );

            if (pause)
            {
                Console.Out.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            Console.Out.WriteLine("Querying...");

            foreach (var annotationLabel in extractorPerAnnotationLabel.Keys)
            {
                var query = config.GetSection(annotationLabel)["Query"];
                Console.Out.WriteLine($"Querying {annotationLabel} for value '{query}'...");

                if (bool.Parse(config.GetSection(annotationLabel)["Facetable"]))
                {
                    Console.Out.WriteLine($"Facet query for {annotationLabel} with query {query}...");
                    var response = index.Facets(annotationLabel, query);
                    var countDocs =
                        response.Results.Count;
                    var ids = response.Results.Select(d => d.Document["FileName"]);
                    Console.Out.WriteLine($"Found {countDocs} records with the {annotationLabel} '{query}'");
                    Console.Out.WriteLine("File names:");
                    Console.Out.WriteLine(string.Join(Environment.NewLine, ids));
                    Console.Out.WriteLine("");
                }
                else if (bool.Parse(config.GetSection(annotationLabel)["Searchable"]))
                {
                    Console.Out.WriteLine($"Search query for '{query}'");
                    var response = index.Search(annotationLabel, query);
                    var countDocs =
                        response.Results.Count;
                    var ids = response.Results.Select(d => d.Document["FileName"]);
                    Console.Out.WriteLine($"Found {countDocs} records.");
                    Console.Out.WriteLine("File names:");
                    Console.Out.WriteLine(string.Join(Environment.NewLine, ids));
                    Console.Out.WriteLine("");
                }

                if (pause)
                {
                    Console.Out.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            
            Console.Out.WriteLine("Finished. Press any key to exit and grab a beer...");
            Console.ReadKey();
        }

        private static ISet<string> GetAnnotationTypeWithProperty(IEnumerable<string> annotationTypes, string property, IConfiguration config)
        {
            return annotationTypes.Where(aType => bool.Parse(config.GetSection(aType)[property])).ToHashSet();
        }
    }
}
