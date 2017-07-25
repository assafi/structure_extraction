namespace StructureExtraction
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class Program
    {
        public static void Main(string[] args)
        {
            string trainingFolderPath = @"F:\Hackathon\FormatExtraction\train";
            string testFolderPath = @"F:\Hackathon\FormatExtraction\score";
            var trainingFolder = new DirectoryInfo(trainingFolderPath);

            var examples = new List<Tuple<string, uint, uint>>();
        
            foreach (var trainingFile in trainingFolder.EnumerateFiles())
            {
                using (var sr = new StreamReader(trainingFile.OpenRead()))
                {
                    var exampleContent = sr.ReadToEnd().ToLowerInvariant();

                    if (exampleContent.IndexOf("ADMISSION DATE :\n", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    int startIndex = exampleContent.IndexOf("ADMISSION DATE :\n", StringComparison.OrdinalIgnoreCase) + "ADMISSION DATE :\n".Length;
                    int endIndex = startIndex + exampleContent.Substring(startIndex).IndexOf("\n");
                    examples.Add(Tuple.Create(exampleContent, (uint)startIndex, (uint)endIndex));
                }
            }

            Console.Out.WriteLine();
            Console.Out.Write("Learning...");
            var extractor = StructureExtractor.TrainExtractorAsync(examples).Result;
            Console.Out.WriteLine(" Done");
            Console.Out.WriteLine();

            var scoreContents = new List<Document>();
            // Test on real samples
            foreach (var testFile in new DirectoryInfo(testFolderPath).EnumerateFiles())
            {
                using (var sr = new StreamReader(testFile.OpenRead()))
                {
                    scoreContents.Add(new Document {
                        Id = testFile.Name,
                        Content = sr.ReadToEnd().ToLowerInvariant()
                    });
                }
            }

            var extractions = extractor.ExtractAsync(scoreContents).Result;
            foreach (var extraction in extractions)
            {
                Console.Out.WriteLine($"File: {extraction.Id}, Extract: {extraction.Content}");
            }
        }
    }
}
