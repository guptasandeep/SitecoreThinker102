using Sitecore.Data.Items;
using Sitecore.Links.UrlBuilders;
using Sitecore.XA.Feature.SiteMetadata.Sitemap;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Sitecore.Data;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Security.Accounts;
using Sitecore.XA.Feature.SiteMetadata.Enums;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using System.IO;
using System.Xml.Linq;
using System.Collections;
using System.Xml;

namespace SitecoreThinker.Feature.SEO.Sitemap
{
    public class CustomSitemapGenerator : SitemapGenerator
    {
        public Hashtable GenerateSitemapIndex(Item homeItem, NameValueCollection externalSitemaps, SitemapLinkOptions sitemapLinkOptions)
        {
            Hashtable hashtable = this.BuildMultilanguageNestedSitemap(this.ChildrenSearch(homeItem).Where<Sitecore.Data.Items.Item>((Func<Sitecore.Data.Items.Item, bool>)(i => i.Security.CanRead((Account)this.Context.User))), sitemapLinkOptions);
            NameValueCollection siteMapsURLs = new NameValueCollection();
            siteMapsURLs.Merge(externalSitemaps);
            foreach (var key in hashtable.Keys.Cast<string>().OrderBy(key => key).ToList()) //Prepares Sitemap index using the URLs of sitemaps
            {
                var defaultUrlBuilderOptions = LinkManager.GetDefaultUrlBuilderOptions();
                defaultUrlBuilderOptions.AlwaysIncludeServerUrl = true;
                siteMapsURLs.Add((string)key, $"{LinkManager.GetItemUrl(Sitecore.Context.Database.GetItem(this.Context.Site.StartPath), defaultUrlBuilderOptions)}{Convert.ToString(key)}");
            }
            hashtable["sitemap.xml"] = BuildSitemapIndex(siteMapsURLs);
            return hashtable;
        }

        protected Hashtable BuildMultilanguageNestedSitemap(
            IEnumerable<Sitecore.Data.Items.Item> childrenTree,
            SitemapLinkOptions options)
        {
            Hashtable hashtable = new Hashtable();
            ItemUrlBuilderOptions urlOptions1 = this.GetUrlOptions();
            SitemapLinkOptions options1 = new SitemapLinkOptions(options.Scheme, urlOptions1, options.TargetHostname);
            ItemUrlBuilderOptions urlOptions2 = (ItemUrlBuilderOptions)urlOptions1.Clone();
            urlOptions2.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Always);
            SitemapLinkOptions options2 = new SitemapLinkOptions(options.Scheme, urlOptions2, options.TargetHostname);
            ItemUrlBuilderOptions urlOptions3 = (ItemUrlBuilderOptions)options2.UrlOptions.Clone();
            urlOptions3.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Never);
            SitemapLinkOptions options3 = new SitemapLinkOptions(options.Scheme, urlOptions3, options.TargetHostname);
            List<XElement> pages = new List<XElement>();
            LanguageEmbedding? languageEmbedding = options1.UrlOptions.LanguageEmbedding;
            HashSet<ID> idSet = new HashSet<ID>();
            foreach (Item obj1 in childrenTree)
            {
                if (IsItemNoIndexedMarked(obj1))
                    continue;

                SitemapChangeFrequency sitemapChangeFrequency = obj1.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.ChangeFrequency].ToEnum<SitemapChangeFrequency>();
                if (sitemapChangeFrequency != SitemapChangeFrequency.DoNotInclude)
                {
                    List<XElement> alternateUrls = new List<XElement>();
                    foreach (Language language in obj1.Languages)
                    {
                        Item obj2 = obj1.Database.GetItem(obj1.ID, language);
                        if (obj2 != null && obj2.Versions.Count > 0)
                        {
                            options2.UrlOptions.Language = language;
                            string fullLink = this.GetFullLink(obj2, options2);
                            if (!IsDisallowedInRobotstxt(fullLink))
                            {
                                XElement xelement = this.BuildAlternateLinkElement(fullLink, language.CultureInfo.Name);
                                alternateUrls.Add(xelement);
                            }
                        }
                    }
                    if (alternateUrls.Count == 1)
                    {
                        if (this.Context.Site.Language == obj1.Language.Name)
                            options1.UrlOptions.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Never);
                        else
                            options1.UrlOptions.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Always);
                        alternateUrls.Clear();
                    }
                    else if (alternateUrls.Count >= 2)
                    {
                        options1.UrlOptions.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Always);
                        string fullLink = this.GetFullLink(obj1, options3);
                        if (!IsDisallowedInRobotstxt(fullLink))
                        {
                            XElement xelement = this.BuildAlternateLinkElement(fullLink, "x-default");
                            alternateUrls.Insert(0, xelement);
                        }
                    }
                    options1.UrlOptions.Language = obj1.Language;
                    string fullLink1 = this.GetFullLink(obj1, options1);
                    string updatedDate = this.GetUpdatedDate(obj1);
                    string lowerInvariant = sitemapChangeFrequency.ToString().ToLowerInvariant();
                    string priority = this.GetPriority(obj1);
                    if (alternateUrls.Count >= 2 && !idSet.Contains(obj1.ID))
                    {
                        options1.UrlOptions.LanguageEmbedding = new LanguageEmbedding?(LanguageEmbedding.Never);
                        string fullLink2 = this.GetFullLink(obj1, options1);
                        if (!IsDisallowedInRobotstxt(fullLink2))
                            pages.Add(this.BuildPageElement(fullLink2, updatedDate, lowerInvariant, priority, (IEnumerable<XElement>)alternateUrls));
                        idSet.Add(obj1.ID);
                    }
                    if (!IsDisallowedInRobotstxt(fullLink1))
                    {
                        XElement xelement1 = this.BuildPageElement(fullLink1, updatedDate, lowerInvariant, priority, (IEnumerable<XElement>)alternateUrls);
                        pages.Add(xelement1);
                    }
                }
            }

            int sitemapCount = (int)Math.Ceiling((double)pages.Count / SiteMapValidator.MaxURLsPerSiteMap);

            for (int i = 0; i < sitemapCount; i++)
            {
                List<XElement> sitemapUrls = pages.Skip(i * SiteMapValidator.MaxURLsPerSiteMap).Take(SiteMapValidator.MaxURLsPerSiteMap).ToList();
                string sitemapPath = $"sitemap{i + 1}.xml";

                PrepareSiteMap(hashtable, sitemapUrls, sitemapPath);
            }
            return hashtable;
        }

        private void PrepareSiteMap(Hashtable hashtable, List<XElement> sitemapUrls, string sitemapPath)
        {
            XDocument xdocument = this.BuildXmlDocument((IEnumerable<XElement>)sitemapUrls);
            StringBuilder stringBuilder = new StringBuilder();
            using (TextWriter textWriter = (TextWriter)new StringWriter(stringBuilder))
                xdocument.Save(textWriter);
            this.FixDeclaration(stringBuilder);
            string nestedSiteMap = this.FixEncoding(stringBuilder);
            if (SiteMapValidator.IsSiteMapSizeValid(nestedSiteMap))
                hashtable[sitemapPath] = nestedSiteMap;
            else
            {
                string[] nestedSiteMaps = SplitSitemap(nestedSiteMap).ToArray();
                for (int i = 0; i < nestedSiteMaps.Length; i++)
                {
                    hashtable[$"{sitemapPath.Replace(".xml", $"_{i + 1}.xml")}"] = nestedSiteMaps[i];
                }
            }
        }

        public List<string> SplitSitemap(string originalSitemap)
        {
            //return the same original sitemap back if its size is within the given limit 
            List<string> sitemapSegments = new List<string>();
            if (Encoding.UTF8.GetBytes(originalSitemap).Length <= SiteMapValidator.MaxSiteMapSizeInBytes)
            {
                sitemapSegments.Add(originalSitemap);
                return sitemapSegments;
            }

            //If not within the size limit, split it.
            StringBuilder currentSegment = new StringBuilder();

            using (StringReader stringReader = new StringReader(originalSitemap))
            using (XmlReader xmlReader = XmlReader.Create(stringReader))
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "urlset")
                    {
                        if (currentSegment.Length > 0)
                        {
                            // Close the previous <urlset> tag
                            currentSegment.AppendLine("</urlset>");
                            sitemapSegments.Add(currentSegment.ToString());
                            currentSegment.Clear();
                        }
                        currentSegment.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
                        currentSegment.AppendLine("<urlset xmlns:xhtml=\"http://www.w3.org/1999/xhtml\" xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
                    }
                    else if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "url")
                    {
                        if (currentSegment.Length == 0)
                        {
                            throw new InvalidOperationException("Invalid sitemap structure");
                        }

                        string urlElement = xmlReader.ReadOuterXml();
                        // Calculate the size of the new URL element, including existing elements
                        StringBuilder tempCurrentSegment = new StringBuilder();
                        tempCurrentSegment.Append(currentSegment);
                        tempCurrentSegment.AppendLine(urlElement);
                        tempCurrentSegment.AppendLine("</urlset>");

                        if (Encoding.UTF8.GetBytes(tempCurrentSegment.ToString()).Length > SiteMapValidator.MaxSiteMapSizeInBytes)
                        {
                            // Close the previous <urlset> tag
                            currentSegment.AppendLine("</urlset>");
                            sitemapSegments.Add(currentSegment.ToString());
                            currentSegment.Clear();
                            tempCurrentSegment.Clear();

                            // Start a new <urlset> tag
                            currentSegment.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>");
                            currentSegment.AppendLine("<urlset xmlns:xhtml=\"http://www.w3.org/1999/xhtml\" xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
                        }

                        currentSegment.AppendLine(urlElement);
                    }
                }
            }

            if (currentSegment.Length > 0)
            {
                // Close the last <urlset> tag
                currentSegment.AppendLine("</urlset>");
                sitemapSegments.Add(currentSegment.ToString());
            }

            return sitemapSegments;
        }

        //override the existing method to filter the pages with no-index and if disallwed in the robots.txt
        protected override StringBuilder BuildMultilanguageSitemap(IEnumerable<Item> childrenTree, SitemapLinkOptions options)
        {
            ItemUrlBuilderOptions urlOptions = GetUrlOptions();
            SitemapLinkOptions sitemapLinkOptions = new SitemapLinkOptions(options.Scheme, urlOptions, options.TargetHostname);
            ItemUrlBuilderOptions itemUrlBuilderOptions = (ItemUrlBuilderOptions)urlOptions.Clone();
            itemUrlBuilderOptions.LanguageEmbedding = LanguageEmbedding.Always;
            SitemapLinkOptions sitemapLinkOptions2 = new SitemapLinkOptions(options.Scheme, itemUrlBuilderOptions, options.TargetHostname);
            ItemUrlBuilderOptions itemUrlBuilderOptions2 = (ItemUrlBuilderOptions)sitemapLinkOptions2.UrlOptions.Clone();
            itemUrlBuilderOptions2.LanguageEmbedding = LanguageEmbedding.Never;
            SitemapLinkOptions options2 = new SitemapLinkOptions(options.Scheme, itemUrlBuilderOptions2, options.TargetHostname);
            List<XElement> list = new List<XElement>();
            _ = sitemapLinkOptions.UrlOptions.LanguageEmbedding;
            HashSet<ID> hashSet = new HashSet<ID>();
            foreach (Item item5 in childrenTree)
            {
                if (IsItemNoIndexedMarked(item5))
                    continue;

                SitemapChangeFrequency sitemapChangeFrequency = item5.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.ChangeFrequency].ToEnum<SitemapChangeFrequency>();
                if (sitemapChangeFrequency == SitemapChangeFrequency.DoNotInclude)
                {
                    continue;
                }

                List<XElement> list2 = new List<XElement>();
                Language[] languages = item5.Languages;
                foreach (Language language in languages)
                {
                    Item item = item5.Database.GetItem(item5.ID, language);
                    if (item != null && item.Versions.Count > 0)
                    {
                        sitemapLinkOptions2.UrlOptions.Language = language;
                        string fullLink = GetFullLink(item, sitemapLinkOptions2);
                        string name = language.CultureInfo.Name;
                        if (!IsDisallowedInRobotstxt(fullLink))
                        {
                            XElement item2 = BuildAlternateLinkElement(fullLink, name);
                            list2.Add(item2);
                        }
                    }
                }

                if (list2.Count == 1)
                {
                    if (Context.Site.Language == item5.Language.Name)
                    {
                        sitemapLinkOptions.UrlOptions.LanguageEmbedding = LanguageEmbedding.Never;
                    }
                    else
                    {
                        sitemapLinkOptions.UrlOptions.LanguageEmbedding = LanguageEmbedding.Always;
                    }

                    list2.Clear();
                }
                else if (list2.Count >= 2)
                {
                    sitemapLinkOptions.UrlOptions.LanguageEmbedding = LanguageEmbedding.Always;
                    string fullLink2 = GetFullLink(item5, options2);
                    string hreflang = "x-default";
                    if (!IsDisallowedInRobotstxt(fullLink2))
                    {
                        XElement item3 = BuildAlternateLinkElement(fullLink2, hreflang);
                        list2.Insert(0, item3);
                    }
                }

                sitemapLinkOptions.UrlOptions.Language = item5.Language;
                string fullLink3 = GetFullLink(item5, sitemapLinkOptions);
                string updatedDate = GetUpdatedDate(item5);
                string changefreq = sitemapChangeFrequency.ToString().ToLowerInvariant();
                string priority = GetPriority(item5);
                if (list2.Count >= 2 && !hashSet.Contains(item5.ID))
                {
                    sitemapLinkOptions.UrlOptions.LanguageEmbedding = LanguageEmbedding.Never;
                    string fullLink4 = GetFullLink(item5, sitemapLinkOptions);
                    if (!IsDisallowedInRobotstxt(fullLink4))
                        list.Add(BuildPageElement(fullLink4, updatedDate, changefreq, priority, list2));
                    hashSet.Add(item5.ID);
                }

                if (!IsDisallowedInRobotstxt(fullLink3))
                {
                    XElement item4 = BuildPageElement(fullLink3, updatedDate, changefreq, priority, list2);
                    list.Add(item4);
                }
            }

            XDocument xDocument = BuildXmlDocument(list);
            StringBuilder stringBuilder = new StringBuilder();
            using (TextWriter textWriter = new StringWriter(stringBuilder))
            {
                xDocument.Save(textWriter);
            }

            FixDeclaration(stringBuilder);
            return stringBuilder;
        }
        public List<string> RobotstxtDisallowedPaths { get; set; }
        private bool IsDisallowedInRobotstxt(string fullURL)
        {
            if (string.IsNullOrEmpty(fullURL))
                return false;

            if (RobotstxtDisallowedPaths == null)
            {
                RobotstxtDisallowedPaths = new List<string>();
                Item siteSettingItem = Sitecore.Context.Database.GetItem(Sitecore.Context.Site.StartPath.Replace("/Home", "/Settings"));

                string robotstxtcontent = siteSettingItem["RobotsContent"];
                if (!string.IsNullOrWhiteSpace(robotstxtcontent))
                {
                    string[] robotstxtlines = robotstxtcontent.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var robotstxtline in robotstxtlines)
                    {
                        if (robotstxtline.Trim().StartsWith("Disallow: ") && robotstxtline.IndexOf("/") > -1)
                        {
                            string path = robotstxtline.Substring(robotstxtline.IndexOf("/")).Trim();
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                RobotstxtDisallowedPaths.Add(path);
                            }
                        }
                    }
                }
            }

            if (!fullURL.EndsWith("/"))
                fullURL = fullURL + "/";

            string relativePath = new Uri(fullURL).LocalPath;
            foreach (var robotstxtDisallowedPath in RobotstxtDisallowedPaths)
            {
                if (robotstxtDisallowedPath.EndsWith("/"))
                {
                    if (relativePath.Replace("/en/", "/").ToLower().Equals(robotstxtDisallowedPath.ToLower()))
                        return true;
                }
                else if (relativePath.Replace("/en/", "/").ToLower().StartsWith(robotstxtDisallowedPath.ToLower()))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsItemNoIndexedMarked(Item item)
        {
            return item["Meta Robots NOINDEX"] == "1";
        }
    }
}
