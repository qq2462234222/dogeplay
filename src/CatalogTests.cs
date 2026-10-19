using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LumaKeys {
    public static class CatalogTests {
        public static int Run(){return RunAsync().GetAwaiter().GetResult();}
        static async Task<int> RunAsync() {
            var log=new List<string>();int result=0;
            try {
                var index=await Catalog.Refresh();if(index.Entries.Count<1400)throw new Exception("Incomplete live catalog");log.Add("PASS: live catalog refresh, "+index.Entries.Count+" entries");
                var entries=new[]{Catalog.Search(index,"Think of Today").First(),Catalog.Search(index,"Schubert Nacht").First()};
                foreach(var entry in entries) {
                    var song=await Catalog.Download(entry,true);Importers.Validate(song);
                    if(song.Notes.Select(n=>n.Part).Distinct().Count()<2||!song.Notes.GroupBy(n=>n.Beat).Any(g=>g.Select(n=>n.Midi).Distinct().Count()>1))throw new Exception("Missing accompaniment: "+song.Title);
                    log.Add("PASS: search/download/import "+song.Title+", "+song.Notes.Count+" notes, "+song.PartNames.Count+" parts");
                    var cached=await Catalog.Download(entry,false);if(cached.Notes.Count>=song.Notes.Count)throw new Exception("Melody filter failed");log.Add("PASS: cached download and melody selection");
                }
            }catch(Exception ex){result=1;log.Add("FAIL: "+ex);}
            string path=Path.GetFullPath(Path.Combine(Store.Root,"..","test-output","catalog-tests.txt"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path,log);return result;
        }
    }
}
