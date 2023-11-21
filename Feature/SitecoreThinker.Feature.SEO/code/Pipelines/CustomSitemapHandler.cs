using Microsoft.Extensions.DependencyInjection;
using Sitecore.Configuration;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Links.UrlBuilders;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.SecurityModel;
using Sitecore.Web;
using Sitecore.XA.Feature.SiteMetadata.Enums;
using Sitecore.XA.Feature.SiteMetadata.Services;
using Sitecore.XA.Feature.SiteMetadata.Sitemap;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.Abstractions.Configuration;
using Sitecore.XA.Foundation.Multisite;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;

namespace Sitecore.XA.Feature.SiteMetadata.Pipelines.HttpRequestBegin
{
    public class SitemapHandler : HttpRequestProcessor
    {
        protected SiteMetadataConfiguration Configuration = ServiceProviderServiceExtensions.GetService<IConfiguration<SiteMetadataConfiguration>>(ServiceLocator.ServiceProvider).GetConfiguration();

        protected SiteInfo CurrentSite => this.Context.Site.SiteInfo;

        protected string FilePath => Path.Combine(TempFolder.Folder, "sitemap-" + this.CurrentSite.Name + ".xml");

        protected string CacheKey => string.Format("{0}/{1}/{2}/{3}", (object)"XA-SITEMAP", (object)this.Context.Database?.Name, (object)this.CurrentSite.Name, (object)HttpContext.Current.Request.Url);

        public int CacheExpiration { set; get; }

        protected IContext Context { get; } = ServiceProviderServiceExtensions.GetService<IContext>(ServiceLocator.ServiceProvider);

        protected IUrlOptionsProvider UrlOptionsProvider { get; } = ServiceProviderServiceExtensions.GetService<IUrlOptionsProvider>(ServiceLocator.ServiceProvider);

        public override void Process(HttpRequestArgs args)
        {
            Uri url = HttpContext.Current.Request.Url;
            if (!url.PathAndQuery.EndsWith("/sitemap.xml", StringComparison.OrdinalIgnoreCase) && !url.PathAndQuery.EndsWith("/local-sitemap.xml", StringComparison.OrdinalIgnoreCase))
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
                            this.StoreSitemapInCache(sitemap, this.CacheKey);
                            break;
                        }
                        break;
                    case SitemapStatus.StoredInFile:
                        sitemap = this.GetSitemapFromFile();
                        if (string.IsNullOrEmpty(sitemap))
                        {
                            sitemap = this.GetSitemap(settingsItem);
                            string filePath = this.FilePath;
                            Task.Factory.StartNew((Action)(() => this.SaveSitemapToFile(filePath, sitemap)), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
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

        protected virtual bool IsUrlValidForSitemapFiles(Uri url) => UrlUtils.IsUrlValidForFile(url, this.CurrentSite, "/sitemap.xml") || UrlUtils.IsUrlValidForFile(url, this.CurrentSite, "/local-sitemap.xml");

        protected virtual Item GetHomeItem()
        {
            using (new SecurityDisabler())
                return Factory.GetDatabase(this.Context.Database.Name).GetItem(this.CurrentSite.RootPath + this.CurrentSite.StartItem);
        }

        protected virtual SitemapLinkOptions GetLinkBuilderOptions()
        {
            Uri url = HttpContext.Current.Request.Url;
            string targetHostname = this.CurrentSite.ResolveTargetHostName();
            ItemUrlBuilderOptions urlOptions = this.UrlOptionsProvider.GetUrlOptions();
            return this.CurrentSite.Scheme.IsNullOrWhiteSpace() ? new SitemapLinkOptions(url, targetHostname, urlOptions) : new SitemapLinkOptions(this.CurrentSite.Scheme, urlOptions, targetHostname);
        }

        protected virtual string GetSitemap(Item settings)
        {
            Uri url = HttpContext.Current.Request.Url;
            ISitemapGenerator service = ServiceProviderServiceExtensions.GetService<ISitemapGenerator>(ServiceLocator.ServiceProvider);
            if (url.PathAndQuery.EndsWith("/local-sitemap.xml", StringComparison.OrdinalIgnoreCase))
                return service.GenerateSitemap(this.GetHomeItem(), new NameValueCollection(), this.GetLinkBuilderOptions());
            NameValueCollection urlParameters = WebUtil.ParseUrlParameters(settings[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._SitemapSettings.Fields.ExternalSitemaps]);
            if (!((CheckboxField)settings.Fields[Sitecore.XA.Feature.SiteMetadata.Templates.Sitemap._SitemapSettings.Fields.SitemapIndex]).Checked)
                return service.GenerateSitemap(this.GetHomeItem(), urlParameters, this.GetLinkBuilderOptions());
            NameValueCollection nameValueCollection = new NameValueCollection();
            nameValueCollection.Add(Guid.NewGuid().ToString(), url.AbsoluteUri.Replace("/sitemap.xml", "/local-sitemap.xml"));
            nameValueCollection.Merge(urlParameters);
            return service.BuildSitemapIndex(nameValueCollection);
        }

        protected virtual Item GetSettingsItem()
        {
            using (new SecurityDisabler())
                return ServiceProviderServiceExtensions.GetService<IMultisiteContext>(ServiceLocator.ServiceProvider).GetSettingsItem(this.Context.Database.GetItem(this.Context.Site.StartPath));
        }

        protected virtual string GetSitemapFromCache()
        {
            string sitemapFromCache = (string)null;
            if (System.Web.HttpRuntime.Cache[this.CacheKey] != null)
                sitemapFromCache = System.Web.HttpRuntime.Cache.Get(this.CacheKey) as string;
            return sitemapFromCache;
        }

        protected virtual void StoreSitemapInCache(string sitemap, string cacheKey) => System.Web.HttpRuntime.Cache.Insert(cacheKey, (object)sitemap, (CacheDependency)null, DateTime.UtcNow.AddMinutes((double)this.CacheExpiration), Cache.NoSlidingExpiration);

        protected virtual string GetSitemapFromFile()
        {
            string sitemapFromFile = (string)null;
            if (FileUtil.Exists(this.FilePath))
            {
                using (StreamReader streamReader = new StreamReader((Stream)FileUtil.OpenRead(this.FilePath)))
                    sitemapFromFile = streamReader.ReadToEnd();
            }
            return sitemapFromFile;
        }

        protected virtual void SaveSitemapToFile(string filePath, string sitemap)
        {
            using (StreamWriter streamWriter = new StreamWriter((Stream)FileUtil.OpenCreate(filePath)))
                streamWriter.Write(sitemap);
        }

        protected virtual void SetResponse(HttpResponseBase response, object content)
        {
            response.ContentType = "application/xml";
            response.ContentEncoding = Encoding.UTF8;
            response.Headers.Set("cache-control", this.GetCacheControlHeader().ToString());
            response.Write(content);
            response.End();
        }

        protected virtual CacheControlHeaderValue GetCacheControlHeader()
        {
            SitemapCachingHeadersConfiguration cacheControlHeader = this.Configuration.SitemapCacheControlHeader;
            CacheControlHeaderValue controlHeaderValue = new CacheControlHeaderValue();
            int? nullable1;
            if (cacheControlHeader.MaxAge.HasValue && cacheControlHeader.MaxAge.Value >= 0)
            {
                CacheControlHeaderValue controlHeaderValue1 = controlHeaderValue;
                nullable1 = cacheControlHeader.MaxAge;
                TimeSpan? nullable2 = new TimeSpan?(TimeSpan.FromSeconds((double)nullable1.Value));
                controlHeaderValue1.MaxAge = nullable2;
            }
            controlHeaderValue.MustRevalidate = cacheControlHeader.MustRevalidate;
            controlHeaderValue.NoCache = cacheControlHeader.NoCache;
            controlHeaderValue.NoStore = cacheControlHeader.NoStore;
            controlHeaderValue.NoTransform = cacheControlHeader.NoTransform;
            controlHeaderValue.Private = cacheControlHeader.Private;
            controlHeaderValue.ProxyRevalidate = cacheControlHeader.ProxyRevalidate;
            controlHeaderValue.Public = cacheControlHeader.Public;
            nullable1 = cacheControlHeader.SharedMaxAge;
            if (nullable1.HasValue)
            {
                nullable1 = cacheControlHeader.SharedMaxAge;
                if (nullable1.Value >= 0)
                {
                    CacheControlHeaderValue controlHeaderValue2 = controlHeaderValue;
                    nullable1 = cacheControlHeader.SharedMaxAge;
                    TimeSpan? nullable3 = new TimeSpan?(TimeSpan.FromSeconds((double)nullable1.Value));
                    controlHeaderValue2.SharedMaxAge = nullable3;
                }
            }
            cacheControlHeader.NoCacheHeaders.ForEach((Action<string>)(noCacheHeader => controlHeaderValue.NoCacheHeaders.Add(noCacheHeader)));
            cacheControlHeader.PrivateHeaders.ForEach((Action<string>)(privateHeader => controlHeaderValue.PrivateHeaders.Add(privateHeader)));
            return controlHeaderValue;
        }
    }
}
