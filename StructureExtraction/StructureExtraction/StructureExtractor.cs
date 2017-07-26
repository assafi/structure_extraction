// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StructureExtractor.cs" company="Microsoft Corporation">
//  All Rights Reserved  
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace StructureExtraction
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.ProgramSynthesis.Extraction.Text;
    using Microsoft.ProgramSynthesis.Extraction.Text.Constraints;
    using Microsoft.ProgramSynthesis.Extraction.Text.Semantics;
    using Microsoft.ProgramSynthesis.Split.Text.Build.NodeTypes;

    public class StructureExtractor
    {
        private readonly RegionProgram proseProgram;

        private StructureExtractor(RegionProgram proseProgram)
        {
            this.proseProgram = proseProgram;
        }

        public static async Task<StructureExtractor> TrainExtractorAsync(IEnumerable<Tuple<string, uint, uint>> examples, IEnumerable<string> noneLabeledExamples = null)
        {
            if (null == examples || !examples.Any())
            {
                throw new AggregateException($"{nameof(examples)} must not be null or empty");
            }

            var regionSession = new RegionSession();
            foreach (var example in examples)
            {
                var stringRegion = new StringRegion(example.Item1, Semantics.Tokens);
                var field = stringRegion.Slice(example.Item2, example.Item3);
                regionSession.AddConstraints(new RegionExample(stringRegion, field));
            }

            if (noneLabeledExamples?.Any() == true)
            {
                regionSession.AddInputs(noneLabeledExamples.Select(e => new StringRegion(e, Semantics.Tokens)));
            }

            //var program = await regionSession.LearnAsync();
            await Task.FromResult(0);
            var program = regionSession.Learn();
            if (null == program)
            {
                // Try again with less examples? 
                var tmpExamples = examples;
                while (program == null && tmpExamples.Count() > 1)
                {
                    tmpExamples = tmpExamples.Skip((tmpExamples.Count() + 1) / 2);
                    regionSession = new RegionSession();
                    foreach (var example in tmpExamples)
                    {
                        var stringRegion = new StringRegion(example.Item1, Semantics.Tokens);
                        var field = stringRegion.Slice(example.Item2, example.Item3);
                        regionSession.AddConstraints(new RegionExample(stringRegion, field));
                    }
                    program = regionSession.Learn();
                }

                if (program == null)
                {
                    throw new Exception("No program found");
                }
            }

            return new StructureExtractor(program);
        }

        public async Task<IEnumerable<Document>> ExtractAsync(IEnumerable<Document> documents)
        {
            var tasks =
                documents.Select(d => Task.Run(
                    () =>
                    {
                        var result = this.proseProgram.Run(new StringRegion(d.Content, Semantics.Tokens));
                        return new Document
                        {
                            Id = d.Id,
                            Content = result?.Value ?? "",
                            Start = result != null ? (int)result.Start : -1,
                            End = result != null ? (int)result.End : -1,
                        };
                    }));

            return await Task.WhenAll(tasks);
        }

        public IEnumerable<Document> Extract(IEnumerable<Document> documents)
        {
            return documents.Select(d =>
            {
                var result = this.proseProgram.Run(new StringRegion(d.Content, Semantics.Tokens));
                return new Document
                {
                    Id = d.Id,
                    Content = result?.Value ?? "",
                    Start = result != null ? (int)result.Start : -1,
                    End = result != null ? (int)result.End : -1,
                };
            });
        }
    }
}
