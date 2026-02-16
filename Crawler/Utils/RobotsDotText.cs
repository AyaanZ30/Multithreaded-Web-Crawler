using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawler.Utils
{
    public interface IRobotsDotText
    {   
        Task<bool> IsCrawlAllowedAsync(string url, string userAgent = "*");
        Task LoadRobotsFileAsync(string domain);          // load robots.txt for a domain and parse it
        int? GetCrawlDelay(string userAgent = "*");       // wait time between requests        
    }

    public class RobotsDotText : IRobotsDotText
    {
        private readonly HttpClient _httpClient;         // to load robots.txt by making requests
        private readonly Dictionary<string, RobotsRules> _rulesCache;

        public RobotsDotText(HttpClient httpClient = null)
        {
            _httpClient = httpClient;
            _rulesCache = new Dictionary<string, RobotsRules>();
        }

        public async Task<bool> IsCrawlAllowedAsync(string url, string userAgent = "*")
        {
            try
            {
                var uri = new Uri(url);
                string domain = $"{uri.Scheme}://{uri.Host}";

                if (!_rulesCache.ContainsKey(domain))
                {
                    await LoadRobotsFileAsync(domain);
                }
                if (!_rulesCache.ContainsKey(domain))
                {
                    return true;
                }

                var rules = _rulesCache[domain];
                string path = uri.PathAndQuery;

                var applicableRules = rules.GetRulesForUserAgent(userAgent);
                return IsPathAllowed(path, applicableRules);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error checking robots.txt for {url}: {ex.Message}");
                return true;
            }
        }

        public async Task LoadRobotsFileAsync(string domain)
        {
            try
            {
                string robotsFileUrl = $"{domain}/robots.txt";
                var response = await _httpClient.GetAsync(robotsFileUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _rulesCache[domain] = new RobotsRules();
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var obtainedRules = ParseRobotsFile(content);
 
                _rulesCache[domain] = obtainedRules;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error loading robots.txt for {domain}: {ex.Message}");
                _rulesCache[domain] = new RobotsRules();
            }
        }

        public int? GetCrawlDelay(string userAgent = "*")
        {
            return null;  
        } 

        private RobotsRules ParseRobotsFile(string content)
        {
            // find allowed, disallowed, sitemap urls, etc on the obtained raw robots.txt content to catch the rules for different user agents
            var rules = new RobotsRules();
            string currentUserAgent = null;
            var currentRules = new UserAgentRules();

            var lines = content.Split("\n");

            foreach(var line in lines)
            {
                string trimmedLine = line.Split('#')[0].Trim();
                
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                var parts = trimmedLine.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                    continue;

                string directive = parts[0].Trim().ToLowerInvariant();
                string value = parts[1].Trim();

                switch (directive)
                {
                    case "user-agent":
                        if(currentUserAgent != null)
                        {
                            rules.AddRulesToUserAgent(currentUserAgent, currentRules);
                        }
                        currentUserAgent = value;
                        currentRules = new UserAgentRules();
                        break;
                    
                    case "allow":
                        if(currentUserAgent != null)
                        {
                            currentRules.Allow.Add(value);
                        }
                        break;
                    
                    case "disallow":
                        if(currentUserAgent != null)
                        {
                            currentRules.Disallow.Add(value);
                        }
                        break;
                    
                    case "crawl-delay":
                        if(currentUserAgent != null && int.TryParse(value, out var delay))
                        {
                            currentRules.CrawlDelay = delay;
                        }
                        break;
                    
                    case "sitemap":
                        rules.Sitemaps.Add(value);
                        break;
                }
            }
            if(currentUserAgent != null)
            {
                rules.AddRulesToUserAgent(currentUserAgent, currentRules);
            }
            return rules;
        }
        private bool IsPathAllowed(string path, UserAgentRules rules)
        {
            foreach(var allowPattern in rules.Allow.OrderByDescending(p => p.Length))
            {
                if(PathMatches(path, allowPattern)) return true;
            }

            foreach(var disallowPattern in rules.Disallow.OrderByDescending(p => p.Length))
            {
                if(PathMatches(path, disallowPattern)) return true;
            }   

            return true;
        }

        private bool PathMatches(string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            /*
            EXAMPLE:
            path = "/admin/users/list.html"
            pattern = "/admin/*" (every url whose parent is admin)

            prefix = "/admin/" (remove *)

            "/admin/users/list.html".StartsWith(prefix) => True
            */

            if (pattern.EndsWith("*"))
            {
                string prefix = pattern.TrimEnd('*');
                return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            /*
            EXAMPLE:
            path = "/admin/posts/456/comments/789/delete"
            pattern = "\admin\*\delete\" (every url whose parent is admin)

            pattern.Split(*) => ["/admin/", "/delete"]

            index = 0 (where "/admin/ "is found in path)
            part("/admin/").Length = 7
            currentIndex = index + part("/admin/").Length = 0 + 7 = 7

            index = 35 (where "/delete" is found in path)
            part("/delete").Length = 7
            currentIndex = index + part("/delete").Length = 35 + 7 = 42
            */
            if (pattern.Contains("*"))
            {
                var parts = pattern.Split('*');
                int currentIndex = 0;
                
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part))
                        continue;
                        
                    int index = path.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                        return false;
                    currentIndex = index + part.Length;
                }
                return true;
            }

            return path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}