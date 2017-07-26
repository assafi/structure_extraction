using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json;
using StructureExtraction;

namespace IndexIt.ApiControllers
{
    public class Word
    {
        public string text;
        public int startPos;
        public int endPos;
    }

    public class Rule
    {
        public string file;
        public string field;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? startPos;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? endPos;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string text;
    }

    public class RenameRule
    {
        public string name;
    }

    public class Match
    {
        public bool failed;
        public string file;
        public string field;
        public int startPos;
        public int endPos;
        public string text;
    }

    public class FileText
    {
        public List<Word> value { get; set; }
        public List<Match> matches { get; set; }
    }

    public class RuleText
    {
        public List<Rule> value { get; set; }
    }

    // A rule is an example output for a file
    public class FilesController : ApiController
    {
        static string trainingDir = @"C:\Users\tilovell\Desktop\hackathon\PARSED_TRAINING_RECORDS";
        static string testDir = @"C:\Users\tilovell\Desktop\hackathon\PARSED_TEST_RECORDS";

        static string sidDir = @"C:\Users\tilovell\Desktop\hackathon\sid";
        [NonAction]
        static string GetSidFile(int sid) => Path.Combine(sidDir, sid.ToString());
        [NonAction]
        static RuleText LoadRules(int sid)
        {
            var sidFile = GetSidFile(sid);
            var json = "{ \"value\": [] }";
            try
            {
                json = System.IO.File.ReadAllText(sidFile);
            }
            catch (IOException)
            {
            }
            return JsonConvert.DeserializeObject<RuleText>(json);
        }
        [NonAction]
        static void SaveRules(int sid, RuleText rules)
        {
            var sidFile = GetSidFile(sid);
            string json = JsonConvert.SerializeObject(rules);
            System.IO.File.WriteAllText(sidFile, json);
        }

        static Dictionary<string, string> _fileCache = new Dictionary<string, string>();
        [NonAction]
        static string GetFileText(string file)
        {
            string fileText = "file not found";
            if (!_fileCache.ContainsKey(file))
            {
                try
                {
                    fileText = System.IO.File.ReadAllText(Path.Combine(trainingDir, file + ".txt"));
                    _fileCache[file] = fileText;
                }
                catch (FileNotFoundException)
                {
                }
            }
            else fileText = _fileCache[file];
            return fileText;
        }

        [NonAction]
        List<string> GetFileNames()
            => Directory.EnumerateFiles(trainingDir)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();

        // temporary perf hack
        static Dictionary<string, Match> _matchesCache = new Dictionary<string, Match>();

        [NonAction]
        List<Match> GetMatches(int sid, string file)
        {
            string fileText = GetFileText(file);
            var rules = LoadRules(sid);
            List<Match> ret = new List<Match>();
            foreach (var ruleGroup in rules.value.GroupBy(r => r.field))
            {
                var field = ruleGroup.Key;
                var instances = ruleGroup.ToArray();

                //var unmatchedFiles = GetFileNames().Except(instances.Select(inst => inst.file)).Distinct().ToArray();
                if (instances.Any())
                {
                    string key = field + "@" + file + ":" + JsonConvert.SerializeObject(instances, Formatting.None);
                    Match output;
                    if (_matchesCache.TryGetValue(key, out output))
                    {
                        ret.Add(output);
                        continue;
                    }

                    var trainingExamples = instances
                        .Select(i => Tuple.Create(GetFileText(i.file), (uint)i.startPos, (uint)i.endPos))
                        .ToList();
                    string actualText = null;
                    try
                    {
                        var extractor = StructureExtractor.TrainExtractorAsync(
                            trainingExamples,
                            null).Result;
                        var match = extractor.Extract(new Document[] {
                        new Document { Id = file, Content = fileText.ToUpperInvariant() } }).FirstOrDefault();
                        actualText = match?.Content;
                        if (actualText != null && match.Start >= 0)
                        {
                            string expectedText = fileText.Substring(match.Start, match.End - match.Start);
                            output = new Match
                            {
                                file = file,
                                field = field,
                                failed = false,
                                startPos = match.Start,
                                endPos = match.End,
                                text = actualText
                            };
                            ret.Add(output);
                            _matchesCache[key] = output;
                        }
                    }
                    catch (Exception)
                    {
                    }

                    if (String.IsNullOrEmpty(actualText))
                    {
                        ret.Add(
                            new Match { file = file, field = field, failed = true }
                            );
                    }
                }
            }
            return ret;
        }

        // REMAME rule
        [ActionName("Rename")]
        [AcceptVerbs("POST")]
        public RuleText Rename(int sid, string file, string rule, [FromBody] RenameRule body)
        {
            var rules = LoadRules(sid);
            foreach (var ruleObj in rules.value) if (ruleObj.field == rule) ruleObj.field = body.name;
            SaveRules(sid, rules);
            return Get(sid, file);
        }

        // GET file
        [ActionName("Post")]
        [AcceptVerbs("POST")]
        public FileText Post(int sid, string file)
        {
            string fileText = GetFileText(file);

            int pos = 0;
            var lines = fileText.Split(new[] { '\n' });
            var ret = new FileText { value = new List<Word>() };
            for (int i=0; i < lines.Length; i++)
            {
                int pos2 = pos;
                var words = lines[i].Split(new[] { ' ' });
                for (int j=0; j < words.Length; j++)
                {
                    var word = words[j];
                    if (!string.IsNullOrEmpty(word))
                    {
                        ret.value.Add(new Word { startPos = pos2, endPos = pos2 + word.Length, text = word });
                    }
                    pos2 += word.Length + 1;
                }
                ret.value.Add(new Word { text = "\n" });
                pos += lines[i].Length + 1;
            }

            ret.matches = GetMatches(sid, file);

            return ret;
        }

        // GET rules [that are from this file]
        [ActionName("Get")]
        [AcceptVerbs("GET")]
        public RuleText Get(int sid, string file, string rule = null)
        {
            var rules = LoadRules(sid);
            var filteredRules = rules.value.Where(r => r.file == file).ToList();

            // generate dummy placeholder rules where none had actually been defined for this file yet
            var placeholderRules = rules.value.Select(r => r.field).Distinct()
                .Except(filteredRules.Select(r => r.field))
                .Select(field => new Rule { file = file, field = field } );

            rules.value = filteredRules.Concat(placeholderRules).ToList();
            return rules;
        }

        // PUT = add rule/example
        [ActionName("Put")]
        [AcceptVerbs("PUT")]
        public RuleText Put(int sid, string file, string rule, [FromBody]Rule newRule)
        {
            if (!String.Equals(file, newRule.file)) throw new Exception("bad request");
            if (!String.Equals(rule, newRule.field)) throw new Exception("bad request");

            var rules = LoadRules(sid);
            var index = rules.value.FindIndex((Rule r) => r.file == file && r.field == rule);
            if (index >= 0)
            {
                rules.value[index] = newRule;
            }
            else rules.value.Add(newRule);
            SaveRules(sid, rules);
            return Get(sid, file);
        }

        // DELETE rule
        [ActionName("Delete")]
        [AcceptVerbs("DELETE")]
        public RuleText Delete(int sid, string file, string rule)
        {
            var rules = LoadRules(sid);
            var index = rules.value.FindIndex((Rule r) => r.file == file && r.field == rule);
            if (index != 0)
            {
                rules.value.RemoveAt(index);
            }
            SaveRules(sid, rules);
            return Get(sid, file);
        }
    }
}