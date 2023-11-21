using Microsoft.Extensions.DependencyInjection;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.Links.UrlBuilders;
using Sitecore.Security.Accounts;
using Sitecore.XA.Feature.SiteMetadata.Enums;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.Multisite.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace Sitecore.XA.Feature.SiteMetadata.Sitemap
{
    public class SitemapGenerator : ISitemapGenerator
    {
        protected static XNamespace Xhtml { get; } = (XNamespace)"http://www.w3.org/1999/xhtml";

        protected static XNamespace ns { get; } = (XNamespace)"http://www.sitemaps.org/schemas/sitemap/0.9";

        public XmlWriterSettings XmlWriterSettings { set; get; }

        protected IContext Context { get; } = ServiceProviderServiceExtensions.GetService<IContext>(ServiceLocator.ServiceProvider);

        protected ILinkProviderService LinkProviderService { get; } = ServiceProviderServiceExtensions.GetService<ILinkProviderService>(ServiceLocator.ServiceProvider);

        public SitemapGenerator() => this.XmlWriterSettings = new XmlWriterSettings()
        {
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        public SitemapGenerator(XmlWriterSettings xmlWriterSettings) => this.XmlWriterSettings = xmlWriterSettings;

        public virtual string GenerateSitemap(Sitecore.Data.Items.Item homeItem, SitemapLinkOptions sitemapLinkOptions) => this.GenerateSitemap(homeItem, (NameValueCollection)null, sitemapLinkOptions);

        public virtual string GenerateSitemap(
          Sitecore.Data.Items.Item homeItem,
          NameValueCollection externalSitemaps,
          SitemapLinkOptions sitemapLinkOptions)
        {
            List<string> externalXmls = (List<string>)null;
            Task task = (Task)null;
            if (externalSitemaps != null && externalSitemaps.Count > 0)
            {
                task = new Task((Action)(() => this.DownloadExternalSitemaps(externalSitemaps, out externalXmls)));
                task.Start();
            }
            StringBuilder xml = this.BuildMultilanguageSitemap(this.ChildrenSearch(homeItem).Where<Sitecore.Data.Items.Item>((Func<Sitecore.Data.Items.Item, bool>)(i => i.Security.CanRead((Account)this.Context.User))), sitemapLinkOptions);
            task?.Wait();
            if (externalXmls != null && externalXmls.Count > 0)
            {
                string str = this.JoinXmls(xml.ToString(), (IEnumerable<string>)externalXmls);
                xml.Clear();
                xml.Append(str);
            }
            return this.FixEncoding(xml);
        }

        public virtual string BuildSitemapIndex(NameValueCollection externalSitemaps)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = true;
            using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, this.XmlWriterSettings))
            {
                xmlWriter.WriteStartDocument(true);
                xmlWriter.WriteStartElement("sitemapindex", "http://www.sitemaps.org/schemas/sitemap/0.9");
                foreach (string key in externalSitemaps.Keys)
                {
                    string url = HttpUtility.UrlDecode(externalSitemaps[key]);
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(url) && UrlUtils.IsValidUrl(url))
                    {
                        xmlWriter.WriteStartElement("sitemap");
                        xmlWriter.WriteElementString("loc", url);
                        xmlWriter.WriteEndElement();
                        xmlWriter.Flush();
                        if (flag)
                        {
                            flag = false;
                            this.FixDeclaration(stringBuilder);
                        }
                    }
                }
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
            }
            return this.FixEncoding(stringBuilder);
        }

        protected virtual ItemUrlBuilderOptions GetUrlOptions() => (ItemUrlBuilderOptions)this.LinkProviderService.GetLinkProvider(this.Context.Site).GetDefaultUrlBuilderOptions();

        protected virtual StringBuilder BuildMultilanguageSitemap(
          IEnumerable<Sitecore.Data.Items.Item> childrenTree,
          SitemapLinkOptions options)
        {
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
            foreach (Sitecore.Data.Items.Item obj1 in childrenTree)
            {
                SitemapChangeFrequency sitemapChangeFrequency = obj1.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.ChangeFrequency].ToEnum<SitemapChangeFrequency>();
                if (sitemapChangeFrequency != SitemapChangeFrequency.DoNotInclude)
                {
                    List<XElement> alternateUrls = new List<XElement>();
                    foreach (Language language in obj1.Languages)
                    {
                        Sitecore.Data.Items.Item obj2 = obj1.Database.GetItem(obj1.ID, language);
                        if (obj2 != null && obj2.Versions.Count > 0)
                        {
                            options2.UrlOptions.Language = language;
                            XElement xelement = this.BuildAlternateLinkElement(this.GetFullLink(obj2, options2), language.CultureInfo.Name);
                            alternateUrls.Add(xelement);
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
                        XElement xelement = this.BuildAlternateLinkElement(this.GetFullLink(obj1, options3), "x-default");
                        alternateUrls.Insert(0, xelement);
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
                        pages.Add(this.BuildPageElement(fullLink2, updatedDate, lowerInvariant, priority, (IEnumerable<XElement>)alternateUrls));
                        idSet.Add(obj1.ID);
                    }
                    XElement xelement1 = this.BuildPageElement(fullLink1, updatedDate, lowerInvariant, priority, (IEnumerable<XElement>)alternateUrls);
                    pages.Add(xelement1);
                }
            }
            XDocument xdocument = this.BuildXmlDocument((IEnumerable<XElement>)pages);
            StringBuilder stringBuilder = new StringBuilder();
            using (TextWriter textWriter = (TextWriter)new StringWriter(stringBuilder))
                xdocument.Save(textWriter);
            this.FixDeclaration(stringBuilder);
            return stringBuilder;
        }

        protected virtual XDocument BuildXmlDocument(IEnumerable<XElement> pages) => new XDocument(new XDeclaration("1.0", "UTF-8", "no"), new object[1]
        {
      (object) new XElement(SitemapGenerator.ns + "urlset", new object[2]
      {
        (object) new XAttribute(XNamespace.Xmlns + "xhtml", (object) (XNamespace) "http://www.w3.org/1999/xhtml"),
        (object) pages
      })
        });

        protected virtual XElement BuildAlternateLinkElement(
          string href,
          string hreflang,
          string rel = "alternate")
        {
            return new XElement(SitemapGenerator.Xhtml + "link", new object[3]
            {
        (object) new XAttribute((XName) nameof (rel), (object) rel),
        (object) new XAttribute((XName) nameof (hreflang), (object) hreflang),
        (object) new XAttribute((XName) nameof (href), (object) href)
            });
        }

        protected virtual XElement BuildPageElement(
          string loc,
          string lastmod,
          string changefreq,
          string priority,
          IEnumerable<XElement> alternateUrls)
        {
            return new XElement(SitemapGenerator.ns + "url", new object[5]
            {
        (object) new XElement(SitemapGenerator.ns + nameof (loc), (object) loc),
        (object) new XElement(SitemapGenerator.ns + nameof (lastmod), (object) lastmod),
        (object) new XElement(SitemapGenerator.ns + nameof (changefreq), (object) changefreq),
        (object) new XElement(SitemapGenerator.ns + nameof (priority), (object) priority),
        (object) alternateUrls
            });
        }

        protected virtual StringBuilder BuildSitemap(
          IEnumerable<Sitecore.Data.Items.Item> childrenTree,
          SitemapLinkOptions options)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = true;
            using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, this.XmlWriterSettings))
            {
                xmlWriter.WriteStartDocument(true);
                xmlWriter.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");
                foreach (Sitecore.Data.Items.Item obj in childrenTree)
                {
                    SitemapChangeFrequency sitemapChangeFrequency = obj.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.ChangeFrequency].ToEnum<SitemapChangeFrequency>();
                    if (sitemapChangeFrequency != SitemapChangeFrequency.DoNotInclude)
                    {
                        xmlWriter.WriteStartElement("url");
                        xmlWriter.WriteElementString("loc", this.GetFullLink(obj, options));
                        xmlWriter.WriteElementString("lastmod", this.GetUpdatedDate(obj));
                        xmlWriter.WriteElementString("changefreq", sitemapChangeFrequency.ToString());
                        xmlWriter.WriteElementString("priority", this.GetPriority(obj));
                        xmlWriter.WriteEndElement();
                        xmlWriter.Flush();
                        if (flag)
                        {
                            flag = false;
                            this.FixDeclaration(stringBuilder);
                        }
                    }
                }
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
            }
            return stringBuilder;
        }

        protected virtual string GetFullLink(Sitecore.Data.Items.Item item, SitemapLinkOptions options)
        {
            string uriString = LinkManager.GetItemUrl(item, options.UrlOptions);
            if (!uriString.StartsWith("/", StringComparison.Ordinal))
                uriString = new Uri(uriString).LocalPath;
            return options.Scheme + Uri.SchemeDelimiter + options.TargetHostname + uriString;
        }

        protected virtual string GetUpdatedDate(Sitecore.Data.Items.Item item) => string.Format("{0:yyyy-MM-dd}", (object)item.Statistics.Updated);

        protected virtual string GetPriority(Sitecore.Data.Items.Item item) => this.Context.Database.GetItem(((ReferenceField)item.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.Priority]).TargetID).Fields[Sitecore.XA.Foundation.Common.Templates.Enum.Fields.Value].Value;

        protected virtual void DownloadExternalSitemaps(
          NameValueCollection externalSitemaps,
          out List<string> externalXml)
        {
            externalXml = new List<string>();
            using (WebClient webClient = new WebClient())
            {
                foreach (string key in externalSitemaps.Keys)
                {
                    string externalSitemap = externalSitemaps[key];
                    if (!string.IsNullOrEmpty(externalSitemap))
                    {
                        if (UrlUtils.IsValidUrl(externalSitemap))
                        {
                            try
                            {
                                string str = webClient.DownloadString(externalSitemap);
                                externalXml.Add(str);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Could not download sxternal sitemap for key=" + key, ex, (object)this);
                            }
                        }
                    }
                }
            }
        }

        protected virtual IList<Sitecore.Data.Items.Item> ChildrenSearch(Sitecore.Data.Items.Item homeItem)
        {
            List<Sitecore.Data.Items.Item> objList = new List<Sitecore.Data.Items.Item>();
            Queue<Sitecore.Data.Items.Item> objQueue = new Queue<Sitecore.Data.Items.Item>();
            if (homeItem.HasChildren)
            {
                objQueue.Enqueue(homeItem);
                if (homeItem.Versions.Count > 0)
                    objList.Add(homeItem);
                objList.AddRange(this.GetItemsForOtherLanguages(homeItem));
                while (objQueue.Count != 0)
                {
                    foreach (Sitecore.Data.Items.Item child in objQueue.Dequeue().Children)
                    {
                        if (!objList.Contains(child))
                        {
                            if (!this.ShouldBeSkipped(child))
                            {
                                if (child.Versions.Count > 0)
                                    objList.Add(child);
                                objList.AddRange(this.GetItemsForOtherLanguages(child));
                            }
                            if (!child.InheritsFrom(Sitecore.XA.Foundation.LocalDatasources.Templates.PageData.ID) && child.HasChildren)
                                objQueue.Enqueue(child);
                        }
                    }
                }
            }
            return (IList<Sitecore.Data.Items.Item>)objList;
        }

        protected virtual IEnumerable<Sitecore.Data.Items.Item> GetItemsForOtherLanguages(
          Sitecore.Data.Items.Item item)
        {
            foreach (Language language in ((IEnumerable<Language>)item.Languages).Where<Language>((Func<Language, bool>)(language => language != item.Language)))
            {
                Sitecore.Data.Items.Item obj = item.Database.GetItem(item.ID, language);
                if (obj != null && obj.Versions.Count > 0)
                    yield return obj;
            }
        }

        protected virtual void FixDeclaration(StringBuilder xml) => xml.Replace("utf-16", "utf-8").Replace("UTF-16", "utf-8");

        protected virtual string FixEncoding(StringBuilder xml) => Encoding.UTF8.GetString(Encoding.Convert(Encoding.Unicode, Encoding.UTF8, Encoding.Unicode.GetBytes(xml.ToString())));

        protected virtual bool ShouldBeSkipped(Sitecore.Data.Items.Item item) => !item.DoesItemInheritFrom(Sitecore.XA.Foundation.Multisite.Templates.Page.ID) || string.IsNullOrEmpty(item.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._Sitemap.Fields.Priority]?.Value);

        protected virtual string JoinXmls(string baseXml, IEnumerable<string> xmlsToAdd)
        {
            XDocument xdocument1 = XDocument.Parse(baseXml);
            xdocument1.Declaration = new XDeclaration("1.0", "UTF-8", "yes");
            foreach (XDocument xdocument2 in xmlsToAdd.Select<string, XDocument>(new Func<string, XDocument>(XDocument.Parse)))
            {
                if (xdocument1.Root != null && xdocument2.Root != null)
                    xdocument1.Root.Add((object)xdocument2.Root.Elements());
            }
            MemoryStream memoryStream = new MemoryStream();
            using (StreamWriter streamWriter = new StreamWriter((Stream)memoryStream, Encoding.UTF8, 1024, true))
                xdocument1.Save((TextWriter)streamWriter);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            using (StreamReader streamReader = new StreamReader((Stream)memoryStream))
                return streamReader.ReadToEnd();
        }
    }
}
