﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json;

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
        public int startPos;
        public int endPos;
        public string text;
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
        List<Match> GetMatches(int sid, string file)
        {
            string fileText = GetFileText(file);
            var rules = LoadRules(sid);
            List<Match> ret = new List<Match>();
            foreach (var ruleGroup in rules.value.GroupBy(r => r.field))
            {
                var field = ruleGroup.Key;
                var instances = ruleGroup.AsEnumerable();
                foreach(var instance in instances)
                {
                    //TODO!...
                }
                dynamic program = null; //TODO;
                try
                {
                    //var result = program.Match(fileText) //TODO:!!
                }
                catch (Exception e)
                {
                    ret.Add(new Match { file = file, field = field, failed = true });
                }
            }
            return ret;
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
            rules.value = rules.value.Where(r => r.file == file).ToList();
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
            return LoadRules(sid);
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
            return LoadRules(sid);
        }
    }
}