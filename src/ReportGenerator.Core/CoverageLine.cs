using System.Collections.Generic;
using System.Linq;

namespace Palmmedia.ReportGenerator.Core
{
    /// <summary>
    /// CoverageLine.
    /// </summary>
    public static class CoverageLine
    {
        /// <summary>
        /// Gets or sets changed-line scopes keyed by relative or raw coverage file path.
        /// </summary>
        public static IDictionary<string, List<int>> ChangeLines { get; set; } = new Dictionary<string, List<int>>();

        /// <summary>
        /// Tries to resolve changed lines for the given coverage file path.
        /// </summary>
        /// <param name="filePath">The raw file path from the coverage report.</param>
        /// <param name="changedLines">The resolved changed lines.</param>
        /// <returns><c>true</c> if changed lines were resolved; otherwise, <c>false</c>.</returns>
        public static bool TryGetChangedLines(string filePath, out List<int> changedLines)
        {
            if (ChangeLines.TryGetValue(filePath, out changedLines))
            {
                return true;
            }

            var normalizedFilePath = NormalizePath(filePath);
            if (ChangeLines.TryGetValue(normalizedFilePath, out changedLines))
            {
                return true;
            }

            var match = ChangeLines
                .Where(entry => PathEndsWith(normalizedFilePath, entry.Key))
                .OrderByDescending(entry => NormalizePath(entry.Key).Length)
                .FirstOrDefault();

            if (match.Value != null)
            {
                changedLines = match.Value;
                return true;
            }

            changedLines = null;
            return false;
        }

        private static bool PathEndsWith(string filePath, string candidate)
        {
            var normalizedCandidate = NormalizePath(candidate).TrimStart('/');
            return NormalizedFilePathEquals(filePath, normalizedCandidate)
                || filePath.EndsWith("/" + normalizedCandidate, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool NormalizedFilePathEquals(string filePath, string candidate)
        {
            return filePath.Equals(candidate, System.StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}