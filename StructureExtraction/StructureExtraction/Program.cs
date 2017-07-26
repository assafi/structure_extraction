namespace StructureExtraction
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class Program
    {
        public static void Main(string[] args)
        {
            string trainingFolderPath = @"F:\Hackathon\FormatExtraction\train";
            string testFolderPath = @"F:\Hackathon\FormatExtraction\score";
            var trainingFolder = new DirectoryInfo(trainingFolderPath);

            var examples = new List<Tuple<string, uint, uint>>();

            if (args.Length != 5)
            {
                throw new ArgumentException($"Usage: az-search-service az-search-admin-key az-search-query-key label label-value");
            }

            var labelPrefixText = args[3];

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
                    int endIndex = startIndex + exampleContent.Substring(startIndex+1).IndexOf("\n");
                    examples.Add(Tuple.Create(exampleContent, (uint)startIndex, (uint)endIndex));
                }
            }

            var documents = new List<Document>();
            // Test on real samples
            foreach (var testFile in new DirectoryInfo(testFolderPath).EnumerateFiles())
            {
                using (var sr = new StreamReader(testFile.OpenRead()))
                {
                    documents.Add(new Document
                    {
                        Id = testFile.Name,
                        Content = sr.ReadToEnd().ToLowerInvariant()
                    });
                }
            }

            var labelFieldName =
                args[3].ToLowerInvariant().Replace(":", "").Replace("\n", "").Replace("\r", "").Trim().Replace(" ", "_");

            Console.Out.WriteLine();
            Console.Out.Write($"Learning extraction logic for '{labelFieldName}'...");

            var noneLabeledExamples = documents.Select(sc => sc.Content).Take(20);
            var extractor = StructureExtractor.TrainExtractorAsync(examples, noneLabeledExamples).Result;
            Console.Out.WriteLine(" Done");
            Console.Out.WriteLine();

            Console.Out.WriteLine($"Extracting '{args[3]}' from documents...");
            var extractions = extractor.Extract(documents).Result;
            foreach (var extraction in extractions)
            {
                Console.Out.WriteLine($"File: {extraction.Item1.Id}, Extract: {extraction.Item2.Content}");
            }

            Console.Out.WriteLine("Indexing...");

            var index = HackIndex.BuildIndex(
                args[0],
                args[1],
                args[2],
                "medicalrecords", extractions.Select(e => new Dictionary<string, string>
            {
                { "FileName", e.Item1.Id.Replace(".txt", "") },
                { "Content" , e.Item1.Content },
                { labelFieldName , e.Item2.Content }
            }).ToArray(),
            "FileName",
            new HashSet<string>(new [] { "FileName", labelFieldName }),
            new HashSet<string>(new[] { labelFieldName }),
            new HashSet<string>(new[] { labelFieldName, "Content" }),
            new HashSet<string>(new[] { "FileName", labelFieldName })
            );

            Console.Out.WriteLine("Querying...");
            var response = index.Facets(labelFieldName, args[4]);

            Console.Out.WriteLine($"labelFieldName values: ");
            var countDocs =
                response.Results.Count;
            var ids = response.Results.Select(d => d.Document["FileName"]);
            Console.Out.WriteLine($"Found {countDocs} records with the {args[4]} {labelFieldName}");
            Console.Out.WriteLine("File names:");
            Console.Out.WriteLine(string.Join(Environment.NewLine, ids));
            Console.Out.WriteLine("");
            Console.Out.WriteLine("Finished. Press any key to exit and grab a beer...");
            Console.ReadKey();
        }
    }
}
