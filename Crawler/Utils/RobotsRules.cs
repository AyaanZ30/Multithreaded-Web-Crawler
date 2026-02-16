using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/*
_userAgentRules : stores rules for each user agent, for example:

User-agent: *
Disallow: /admin/
Disallow: /private/
Allow: /private/public-docs/
Crawl-delay: 2

User-agent: Googlebot
Disallow: /temp/
Allow: /

User-agent: BadBot
Disallow: /

Sitemap: https://www.example.com/sitemap.xml
Sitemap: https://www.example.com/sitemap2.xml

User agents : [*, Googlebot, Badbot] (rules for each stored in _userAgentRules[agent, rules])
view : Googlebot : UserAgentRules("Allow : /", "Disallow : /temp/", CrawlDelay : None)

*/
namespace Crawler.Utils
{
    public class RobotsRules
    {
        
        private readonly Dictionary<string, UserAgentRules> _userAgentRules = new Dictionary<string, UserAgentRules>();
        public List<string> Sitemaps {get; set;} = new List<string>();

        public void AddRulesToUserAgent(string userAgent, UserAgentRules rules)
        {
            // _userAgentRules[googlebot] = {[/], [/temp/], None} : (Allow[], Disallow[], CrawlDelay?)
            _userAgentRules[userAgent.ToLowerInvariant()] = rules;
        }

        // returns Allow[], Disallow[], CrawlDelay? values for a particular Agent (bot)
        public UserAgentRules GetRulesForUserAgent(string userAgent)
        {
            if(_userAgentRules.TryGetValue(userAgent.ToLowerInvariant(), out var rules))
            {
                return rules;
            }
            if(_userAgentRules.TryGetValue("*", out var wildcardRules))
            {
                return wildcardRules;
            }
            return new UserAgentRules();
        }
    }
}