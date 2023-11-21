using Sitecore;
using Sitecore.Data.Items;
using System.Text;
using System.Text.RegularExpressions;

namespace SitecoreThinker.Feature.SEO.Sitemap
{
    public static class SiteMapValidator
    {
        public static int MaxURLsPerSiteMap
        {
            get
            {
                string sitemapLimitPath = Sitecore.Context.Site?.StartPath?.Replace("/Home", "/Settings/Sitemap Limits");
                if (!string.IsNullOrWhiteSpace(sitemapLimitPath))
                {
                    Item siteMapLimitItem = Sitecore.Context.Database.GetItem(sitemapLimitPath);
                    if (siteMapLimitItem != null)
                    {
                        string maxURLsValue = siteMapLimitItem["Sitemap max URLs count"];
                        int maxURLs;
                        if (int.TryParse(maxURLsValue, out maxURLs))
                            return maxURLs;
                    }
                }
                return 50000;
            }
        }

        public static long MaxSiteMapSizeInBytes
        {
            get
            {
                long defaultMaxSize = StringUtil.ParseSizeString("50MB");
                string sitemapLimitPath = Sitecore.Context.Site?.StartPath?.Replace("/Home", "/Settings/Sitemap Limits");
                if (!string.IsNullOrWhiteSpace(sitemapLimitPath))
                {
                    Item siteMapLimitItem = Sitecore.Context.Database.GetItem(sitemapLimitPath);
                    if (siteMapLimitItem != null)
                    {
                        string maxSizeInMBValue = siteMapLimitItem["Sitemap max size"];
                        long setSize = StringUtil.ParseSizeString(maxSizeInMBValue);
                        return setSize <= defaultMaxSize ? setSize : defaultMaxSize;
                    }
                }
                return defaultMaxSize;
            }
        }

        public static bool IsSiteMapURLsLimitValid(string siteMap)
        {
            return Regex.Matches(siteMap, "<loc>").Count <= MaxURLsPerSiteMap;
        }

        public static bool IsSiteMapSizeValid(string siteMap)
        {
            double size = Encoding.UTF8.GetByteCount(siteMap);
            if (size > MaxSiteMapSizeInBytes)
                return false;
            return true;
        }

        public static bool IsSiteMapValid(string siteMap)
        {
            return IsSiteMapSizeValid(siteMap) && IsSiteMapURLsLimitValid(siteMap);
        }

        public static bool IsNestedSiteMap(string url)
        {
            Regex rg = new Regex(@"(?i)(sitemap)(\d+)((_)(\d+))*(.xml)");
            string sitemapFileName = url.Substring(url.LastIndexOf("/") + 1);
            Match match = rg.Match(url);
            return match.Success;
        }
    }
}
