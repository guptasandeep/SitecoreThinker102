# SitecoreThinker102

- SXA Sitemap enhancements in Sitecore version 10.2

## PreRequisites

- Sitecore 10.2 with SXA installed in the local
- Developer workstation to validate the solution
  
## Setup

1. Clone the below repositories 

https://github.com/guptasandeep/SitecoreThinker102.git

2. Take backup of webroot\App_Config and webroot\bin folders. 

3. Deploy the files.
   In cloned repositories,
- Copy all the binaries from https://github.com/guptasandeep/SitecoreThinker102/tree/master/Setup/dll_Binaries to the Sitecore instance bin folder.
- Copy the Include folder from https://github.com/guptasandeep/SitecoreThinker102/tree/master/Setup/App_Config to the Sitecore instance App_Config folder.
- Restore the Sitecore Master database .bacpac file from https://github.com/guptasandeep/SitecoreThinker102/blob/master/Setup/Database/sitecorethinker102_Master_29_Nov_2023.bacpac
- Update the connection strings accordingly.
- Update the IIS Site bindings and host file for the local domain - sitecorethinker102sc.dev.local.
     - 127.0.0.1 sitecorethinker102sc.dev.local
- Login to Sitecore
     - Publish all the items.
- Manage the Sitemap limits at /sitecore/content/SitecoreTenant/DemoSite/Settings/Sitemap Limits to test different cases.
   Test URL: https://sitecorethinker102sc.dev.local/sitemap.xml
