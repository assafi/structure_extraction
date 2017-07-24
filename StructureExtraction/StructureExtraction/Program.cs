namespace StructureExtraction
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.ProgramSynthesis.Extraction.Text;
    using Microsoft.ProgramSynthesis.Extraction.Text.Constraints;
    using Microsoft.ProgramSynthesis.Extraction.Text.Semantics;
    using Microsoft.ProgramSynthesis.Transformation.Text;
    using Microsoft.ProgramSynthesis.Wrangling.Session;

    public class Program
    {
        public static void Main(string[] args)
        {
            string trainingFolderPath = @"C:\Shared\FormatExtraction\prose_training";
            string testFolderPath = @"C:\Shared\FormatExtraction\prose_test";
            var trainingFolder = new DirectoryInfo(trainingFolderPath);

            var extractionSession = new RegionSession();
            var transformationSession = new Session();
        
            foreach (FileInfo trainingFile in trainingFolder.EnumerateFiles())
            {
                using (var sr = new StreamReader(trainingFile.OpenRead()))
                {
                    var exampleContent = sr.ReadToEnd().ToLowerInvariant();
                    var stringRegion = new StringRegion(exampleContent, Semantics.Tokens);

                    if (exampleContent.IndexOf("ADMISSION DATE :\n", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    int startIndex = exampleContent.IndexOf("ADMISSION DATE :\n", StringComparison.OrdinalIgnoreCase) + "ADMISSION DATE :\n".Length;
                    int endIndex = startIndex + exampleContent.Substring(startIndex).IndexOf("\n");
                    var field = stringRegion.Slice((uint)startIndex, (uint)endIndex);
                    var example = new RegionExample(stringRegion, field);
                    extractionSession.AddConstraints(example);

                    DateTime date;
                    if (DateTime.TryParse(field.Value, out date))
                    {
                        transformationSession.AddConstraints(new Example(new InputRow(field.Value), date.ToString("d")));
                    }
                    else
                    {
                        date = DateTime.ParseExact(field.Value, "yyyyMMdd", CultureInfo.InvariantCulture);
                        transformationSession.AddConstraints(new Example(new InputRow(field.Value), date.ToString("d")));
                    }

                    Console.Out.WriteLine($"Sample, file: {trainingFile.Name}. Content: {field}, Date: {date.ToString("d")}");
                }
            }

            Console.Out.WriteLine();
            Console.Out.Write("Learning...");
            var program = extractionSession.Learn(RankingMode.MostLikely);
            var tProgram = transformationSession.Learn();

            if (null == program)
            {
                Console.Out.WriteLine("No program found.");
                return;
            }

            Console.Out.WriteLine(" Done");
            Console.Out.WriteLine();

            // Test on real samples
            foreach (FileInfo testFile in new DirectoryInfo(testFolderPath).EnumerateFiles())
            {
                using (var sr = new StreamReader(testFile.OpenRead()))
                {
                    var testInput = RegionSession.CreateStringRegion(sr.ReadToEnd().ToLowerInvariant());
                    var output = program.Run(testInput);
                    var datetime = tProgram.Run(new InputRow(output.Value));
                    Console.Out.WriteLine($"Output for file {testFile.Name}: {output}. Date: {datetime}");
                }
            }

            Console.Out.WriteLine($"Program {program.Serialize()}");
        }
    }
}
