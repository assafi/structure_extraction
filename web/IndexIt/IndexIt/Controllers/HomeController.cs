using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;

namespace IndexIt.Controllers
{
    public class HomeController : Controller
    {
        static string trainingDir = @"C:\Users\tilovell\Desktop\hackathon\PARSED_TRAINING_RECORDS";
        static string testDir = @"C:\Users\tilovell\Desktop\hackathon\PARSED_TEST_RECORDS";
        
        public ActionResult Index(string doc, int? p1, int? p2)
        {
            ViewBag.Documents = Directory.EnumerateFiles(trainingDir).Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
            if (doc != null)
            {
                ViewBag.Doc = doc;
                ViewBag.DocText = System.IO.File.ReadAllText(Path.Combine(trainingDir, doc + ".txt"));
                var outputsSpecPath = Path.Combine(trainingDir, doc + ".json");
                if (p1 != null && p2 != null)
                {
                    // update file
                    string targetText = ViewBag.DocText.Substring((int)p1, (int)(p2 - p1));

                    var dict = new Dictionary<string, string>();
                    try
                    {
                        var json = System.IO.File.ReadAllText(outputsSpecPath);
                        dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    }
                    catch (FileNotFoundException)
                    {
                    }
                    dict.Add("_newOutput" + (dict.Keys.Count()), targetText + "@" + p1 + "," + p2);
                    string json2 = JsonConvert.SerializeObject(dict);
                    System.IO.File.WriteAllText(outputsSpecPath, json2);
                    p1 = null;
                    p2 = null;
                }
                ViewBag.P1 = p1;
                ViewBag.P2 = p2;
                try
                {
                    var json = System.IO.File.ReadAllText(outputsSpecPath);
                    ViewBag.Outputs = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                }
                catch (FileNotFoundException)
                {
                    ViewBag.Outputs = new Dictionary<string, string>();
                }
            }
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}