using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;

namespace CrestronDeploymentTool.Utilities
{
    /// <summary>
    /// a static helper class to format strings with bold or italics without requiring tons of effort
    /// </summary>
    internal static class TextHelpers
    {
        /// <summary>
        /// parses a string with simple markup (*bold*, **italic**, ***bolditalic***) into a TextBlock.
        /// </summary>
        public static void ParseFormattedText(string input, TextBlock target)
        {
            // Regex matches ***text***, **text**, *text*
            string pattern = @"(\*\*\*.*?\*\*\*|\*\*.*?\*\*|\*.*?\*)";

            int lastIndex = 0;

            foreach (Match match in Regex.Matches(input, pattern))
            {
                // Add normal text before the match
                if (match.Index > lastIndex)
                {
                    target.Inlines.Add(new Run(input.Substring(lastIndex, match.Index - lastIndex)));
                }

                string value = match.Value;
                Run run = new Run();

                if (value.StartsWith("***") && value.EndsWith("***"))
                {
                    // Bold + Italic
                    run.Text = value.Substring(3, value.Length - 6);
                    run.FontWeight = FontWeights.Bold;
                    run.FontStyle = FontStyles.Italic;
                }
                else if (value.StartsWith("**") && value.EndsWith("**"))
                {
                    // Italic
                    run.Text = value.Substring(2, value.Length - 4);
                    run.FontStyle = FontStyles.Italic;
                }
                else if (value.StartsWith("*") && value.EndsWith("*"))
                {
                    // Bold
                    run.Text = value.Substring(1, value.Length - 2);
                    run.FontWeight = FontWeights.Bold;
                }

                target.Inlines.Add(run);
                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after last match
            if (lastIndex < input.Length)
            {
                target.Inlines.Add(new Run(input.Substring(lastIndex)));
            }
        }
    }
}
