using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.PreprocessRequest;
using Sitecore.XA.Foundation.Abstractions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Web;

namespace Sitecore.XA.Foundation.SitecoreExtensions.Pipelines.PreprocessRequest
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
            if (ServiceLocator.ServiceProvider.GetService<IConfiguration<SitecoreExtensionsConfiguration>>().GetConfiguration().AllowedFileNames.Contains<string>(requestFilePath))
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
