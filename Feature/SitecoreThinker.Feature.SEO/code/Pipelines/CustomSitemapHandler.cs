using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.XA.Feature.SiteMetadata.Enums;
using Sitecore.XA.Feature.SiteMetadata.Pipelines.HttpRequestBegin;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Sitecore.DependencyInjection;
using System.Collections.Specialized;
using Sitecore.Web;
using System.Collections;
using Sitecore.IO;
using Sitecore.XA.Feature.SiteMetadata.Sitemap;
using SitecoreThinker.Feature.SEO.Sitemap;

namespace SitecoreThinker.Feature.SEO.Pipelines
{
    public class CustomSitemapHandler : SitemapHandler
    {
        public override void Process(HttpRequestArgs args)
        {
            Uri url = HttpContext.Current.Request.Url;
            if (!IsSiteMapRequest(url))
                return;
            if (this.CurrentSite == null || !this.IsUrlValidForSitemapFiles(url))
            {
                Log.Info("SitemapHandler (sitemap.xml) : " + string.Format("cannot resolve site or url ({0})", (object)url), (object)this);
            }
            else
            {
                Item settingsItem = this.GetSettingsItem();
                SitemapStatus sitemapStatus = settingsItem != null ? settingsItem.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._SitemapSettings.Fields.SitemapMode].ToEnum<SitemapStatus>() : SitemapStatus.Inactive;
                string sitemap;
                switch (sitemapStatus)
                {
                    case SitemapStatus.Inactive:
                        Log.Info("SitemapHandler (sitemap.xml) : " + string.Format("sitemap is off (status : {0})", (object)sitemapStatus), (object)this);
                        return;
                    case SitemapStatus.StoredInCache:
                        sitemap = this.GetSitemapFromCache();
                        if (string.IsNullOrEmpty(sitemap))
                        {
                            sitemap = this.GetSitemap(settingsItem);
                            if (!SiteMapValidator.IsSiteMapValid(sitemap))
                            {
                                Hashtable siteMapIndexAndSiteMaps = this.GetSitemapIndexAndSiteMaps(settingsItem);
                                sitemap = (string)siteMapIndexAndSiteMaps["sitemap.xml"];
                                foreach (var key in siteMapIndexAndSiteMaps.Keys)
                                {
                                    if (string.Equals((string)key, GetSiteMapFileName(), StringComparison.OrdinalIgnoreCase))
                                        sitemap = Convert.ToString(siteMapIndexAndSiteMaps[key]);
                                    this.StoreSitemapInCache(Convert.ToString(siteMapIndexAndSiteMaps[key]), this.CacheKey.Replace("sitemap.xml", Convert.ToString(key)));
                                }
                            }
                            else
                            {
                                this.StoreSitemapInCache(sitemap, this.CacheKey);
                            }

                            break;
                        }
                        break;
                    case SitemapStatus.StoredInFile:
                        sitemap = this.GetSitemapFromFile();
                        if (string.IsNullOrEmpty(sitemap))
                        {
                            sitemap = this.GetNestedSitemapFromFile($"{this.CurrentSite.Name}{this.GetSiteMapFileName()}"); //serve from the file if exists
                            if (string.IsNullOrEmpty(sitemap))
                            {
                                sitemap = this.GetSitemap(settingsItem); //Default
                                if (!SiteMapValidator.IsSiteMapValid(sitemap))
                                {
                                    Hashtable siteMapIndexAndSiteMaps = this.GetSitemapIndexAndSiteMaps(settingsItem);
                                    sitemap = (string)siteMapIndexAndSiteMaps["sitemap.xml"];
                                    foreach (var key in siteMapIndexAndSiteMaps.Keys)
                                    {
                                        if (string.Equals((string)key, GetSiteMapFileName(), StringComparison.OrdinalIgnoreCase))
                                            sitemap = Convert.ToString(siteMapIndexAndSiteMaps[key]);
                                        string filePath = Path.Combine(TempFolder.Folder, this.CurrentSite.Name + key);
                                        Task.Factory.StartNew((Action)(() => this.SaveSitemapToFile(filePath, Convert.ToString(siteMapIndexAndSiteMaps[key]))), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                                    }
                                }
                            }
                            break;
                        }
                        break;
                    default:
                        Log.Info("SitemapHandler (sitemap.xml) : unknown error", (object)this);
                        return;
                }
                this.SetResponse(args.HttpContext.Response, (object)sitemap);
                args.AbortPipeline();

            }
        }

        protected virtual bool IsSiteMapRequest(Uri url)
        {
            if (!url.PathAndQuery.EndsWith("/sitemap.xml", StringComparison.OrdinalIgnoreCase) && !url.PathAndQuery.EndsWith("/local-sitemap.xml", StringComparison.OrdinalIgnoreCase) && !SiteMapValidator.IsNestedSiteMap(HttpContext.Current.Request.Url.PathAndQuery))
                return false;
            return true;
        }

        protected override bool IsUrlValidForSitemapFiles(Uri url)
        {
            if (base.IsUrlValidForSitemapFiles(url))
                return true;
            string vurl = HttpContext.Current.Request.Url.PathAndQuery;
            int lastIndex = vurl.LastIndexOf("/");
            if (lastIndex < 0)
                return false;
            if (vurl.Length > vurl.LastIndexOf("/") + 1)
            {
                string sitemapFileName = vurl.Substring(vurl.LastIndexOf("/") + 1);
                return UrlUtils.IsUrlValidForFile(url, this.CurrentSite, $"/{sitemapFileName}");
            }
            return false;
        }

        protected Hashtable GetSitemapIndexAndSiteMaps(Item settings)
        {
            Hashtable siteMapIndexAndSiteMaps = new Hashtable();
            Uri url = HttpContext.Current.Request.Url;
            CustomSitemapGenerator service = (CustomSitemapGenerator)ServiceProviderServiceExtensions.GetService<ISitemapGenerator>(ServiceLocator.ServiceProvider);
            //Build SiteMap having the local sitemap and external sitemap urls merged
            NameValueCollection urlParameters = WebUtil.ParseUrlParameters(settings[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._SitemapSettings.Fields.ExternalSitemaps]);
            siteMapIndexAndSiteMaps = service.GenerateSitemapIndex(this.GetHomeItem(), urlParameters, this.GetLinkBuilderOptions());
            return siteMapIndexAndSiteMaps;
        }

        protected string GetSiteMapFileName()
        {
            string url = HttpContext.Current.Request.Url.PathAndQuery;
            int lastIndex = url.LastIndexOf("/");
            if (lastIndex < 0)
                return "";
            return url.Substring(url.LastIndexOf("/") + 1);
        }

        protected string GetNestedSitemapFromFile(string fileName)
        {
            string sitemapFromFile = (string)null;
            if (FileUtil.Exists(Path.Combine(TempFolder.Folder, fileName)))
            {
                using (StreamReader streamReader = new StreamReader((Stream)FileUtil.OpenRead(Path.Combine(TempFolder.Folder, fileName))))
                    sitemapFromFile = streamReader.ReadToEnd();
            }
            return sitemapFromFile;
        }
    }
}
