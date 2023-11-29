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
- Copy all the binaries from https://github.com/guptasandeep/MapExtension2.0/tree/master/setup/bin_binaries to the Sitecore instance bin folder.
- Copy Include folder from https://github.com/guptasandeep/MapExtension2.0/tree/master/setup/App_Config to the Sitecore instance App_Config folder.
- Restore the Sitecore Master database .bacpac file from https://github.com/guptasandeep/MapExtension2.0/blob/master/Database/sitecorethinker1031_Master_29_Nov_2023.bacpac
- Update the connection strings accordingly.
- Update the IIS Site bindings and host file for the local domain - sitecorethinker1031sc.dev.local.
     - 127.0.0.1 sitecorethinker1031sc.dev.local
- Login to Sitecore
     - Provide valid Google Map API key with Maps API, Places API, and Geocoding API services enabled.
     - Ensure domain - sitecorethinker1031sc.dev.local is updated in Target Hostname and Hostname in /sitecore/content/demotenant/sitecorethinker/Settings/Site Grouping/sitecorethinker-cm.

4. Publish all the items.

   Test URL: https://sitecorethinker102sc.dev.local/sitemap.xml
