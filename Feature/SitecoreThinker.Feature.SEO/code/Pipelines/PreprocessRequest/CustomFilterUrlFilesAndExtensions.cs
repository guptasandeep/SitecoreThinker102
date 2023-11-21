using SitecoreThinker.Feature.SEO.Sitemap;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.PreprocessRequest;
using Sitecore.XA.Foundation.Abstractions.Configuration;
using Sitecore.XA.Foundation.SitecoreExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;


namespace SitecoreThinker.Feature.SEO.Pipelines.PreprocessRequest
{
    public class FilterUrlFilesAndExtensions : FilterUrlExtensions
    {
        public FilterUrlFilesAndExtensions(
          string allowed,
          string blocked,
          string streamFiles,
          string doNotStreamFiles)
          : base(allowed, blocked, streamFiles, doNotStreamFiles)
        {
        }

        public override void Process(PreprocessRequestArgs args)
        {
            string requestFilePath = this.GetRequestFilePath();
            IEnumerable<string> AllowedFileNames = ServiceLocator.ServiceProvider.GetService<IConfiguration<SitecoreExtensionsConfiguration>>().GetConfiguration().AllowedFileNames;
            if (AllowedFileNames.Contains<string>(requestFilePath))
                return;
            if (SiteMapValidator.IsNestedSiteMap(HttpContext.Current.Request.Url.PathAndQuery)) //check for the nested sitemap files
                return;
            base.Process(args);
        }

        protected virtual string GetRequestFilePath()
        {
            try
            {
                return Path.GetFileName(HttpContext.Current.Request.FilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, (object)this);
                return string.Empty;
            }
        }
    }
}
