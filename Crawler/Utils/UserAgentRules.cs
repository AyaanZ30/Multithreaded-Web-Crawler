using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawler.Utils
{
    public class UserAgentRules
    {
        public List<string> Allow {get; set;} = new List<string>();
        public List<string> Disallow {get; set;} = new List<string>();
        public int? CrawlDelay {get; set;}
    }
}