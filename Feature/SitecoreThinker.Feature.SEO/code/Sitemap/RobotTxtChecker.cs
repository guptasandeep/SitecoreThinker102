using System;
using System.Text.RegularExpressions;

namespace SitecoreThinker.Feature.SEO.Sitemap
{
    public class RobotTxtChecker
    {
        public static bool IsUrlDisallowed(string robotsTxtContent, string urlToCheck)
        {
            try
            {
                // Split the content into lines
                string[] lines = robotsTxtContent.Split('\n');

                // Parse the URL to get the absolute path
                Uri urlUri = new Uri(urlToCheck);
                string urlAbsolutePath = urlUri.AbsolutePath;

                // Check each line for disallow directives
                foreach (string line in lines)
                {
                    if (line.Trim().StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract the path after "Disallow:"
                        string disallowedPath = line.Substring("Disallow:".Length).Trim();

                        // Convert the disallowed path to a regex pattern
                        string regexPattern = WildcardToRegex(disallowedPath);

                        // Check if the URL matches the regex pattern
                        if (Regex.IsMatch(urlAbsolutePath, regexPattern, RegexOptions.IgnoreCase))
                        {
                            return true; // URL is disallowed
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Sitecore.Diagnostics.Log.Error($"Error occured in IsUrlDisallowed() of RobotTxtChecker: {ex.Message}", ex, typeof(RobotTxtChecker));
            }

            return false; // URL is not disallowed or an error occurred
        }

        private static string WildcardToRegex(string wildcard)
        {
            // Escape characters that have special meaning in regular expressions
            string escapedWildcard = Regex.Escape(wildcard);

            // Replace escaped asterisks with a pattern that matches any characters (non-greedy)
            string regexPattern = escapedWildcard.Replace("\\*", ".*?");

            // Handle trailing slash separately to allow for child pages
            if (regexPattern.EndsWith("/"))
            {
                // If the pattern ends with a slash, allow for any characters or none after the slash
                regexPattern = regexPattern.TrimEnd('/') + "(/.*)?";
            }

            return $"^{regexPattern}$";
        }
    }
}