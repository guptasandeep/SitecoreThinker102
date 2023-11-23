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
                if (string.IsNullOrEmpty(robotsTxtContent))  // If the robots.txt content is null or empty, assume crawling is allowed
                {
                    return false;
                }
                Uri urlUri = new Uri(urlToCheck); // Parse the URL to get the absolute path
                string urlAbsolutePath = urlUri.AbsolutePath;

                string[] lines = robotsTxtContent.Split('\n');

                foreach (string line in lines)
                {
                    if (line.Trim().StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        string disallowedPath = line.Substring("Disallow:".Length).Trim();
                        string regexPattern = WildcardToRegex(disallowedPath);  // Convert the disallowed path to a regex pattern

                        if (Regex.IsMatch(urlAbsolutePath, regexPattern, RegexOptions.IgnoreCase)) // Check if the URL matches the regex pattern
                        {
                            return true; // Crawling is disallowed
                        }
                    }
                }
                return false; // If no Disallow rule matches, assume crawling is allowed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred in IsUrlDisallowed(): {ex.Message}");
                return false;
            }
        }

        private static string WildcardToRegex(string wildcard)
        {            
            string escapedWildcard = Regex.Escape(wildcard); // Escape characters that have special meaning in regular expressions
            string regexPattern = escapedWildcard.Replace("\\*", ".*?"); // Replace escaped asterisks with a pattern that matches any characters (non-greedy)
            if (regexPattern.EndsWith("/")) // Handle trailing slash separately to allow for child pages
            {
                // If the pattern ends with a slash, allow for no characters or any characters after the slash
                regexPattern += "(.*)?";
            }
            return $"^{regexPattern}$";
        }
    }
}