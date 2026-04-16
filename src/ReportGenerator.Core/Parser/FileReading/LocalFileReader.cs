using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Palmmedia.ReportGenerator.Core.Common;
using Palmmedia.ReportGenerator.Core.Properties;

namespace Palmmedia.ReportGenerator.Core.Parser.FileReading
{
    /// <summary>
    /// File reader for reading files from local disk.
    /// </summary>
    internal class LocalFileReader : IFileReader
    {
        /// <summary>
        /// Regex to analyze if a path is a deterministic path.
        /// </summary>
        private static readonly Regex DeterministicPathRegex = new Regex("\\/_\\d?\\/", RegexOptions.Compiled);

        /// <summary>
        /// The source directories for typical environments like Azure DevOps or Github Actions.
        /// </summary>
        private static readonly IReadOnlyList<string> DeterministicSourceDirectories;

        /// <summary>
        /// The source directories.
        /// </summary>
        private readonly IReadOnlyList<string> sourceDirectories;

        /// <summary>
        /// Path segment anchors used for fast source path remapping before generic fallback matching.
        /// </summary>
        private readonly IReadOnlyList<string> sourcePathMappingAnchors;

        /// <summary>
        /// Indicates whether empty trailing line in source files should be preserved.
        /// </summary>
        private readonly bool preserveTrailingEmptyLine;

        static LocalFileReader()
        {
            var directories = new List<string>();

            // Azure DevOps
            if ("true".Equals(Environment.GetEnvironmentVariable("TF_BUILD"), StringComparison.OrdinalIgnoreCase)
                && Environment.GetEnvironmentVariable("Build.SourcesDirectory") != null)
            {
                directories.Add(Environment.GetEnvironmentVariable("Build.SourcesDirectory"));
            }

            // Github Actions
            if ("true".Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), StringComparison.OrdinalIgnoreCase)
                && Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") != null)
            {
                directories.Add(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE"));
            }

            DeterministicSourceDirectories = directories;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileReader" /> class.
        /// </summary>
        public LocalFileReader()
            : this(Enumerable.Empty<string>(), Enumerable.Empty<string>(), false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileReader" /> class.
        /// </summary>
        /// <param name="sourceDirectories">The source directories.</param>
        /// <param name="sourcePathMappingAnchors">Path segment anchors used for fast source path remapping.</param>
        /// <param name="preserveTrailingEmptyLine">Indicates whether empty trailing line in source files should be preserved.</param>
        public LocalFileReader(IEnumerable<string> sourceDirectories, IEnumerable<string> sourcePathMappingAnchors, bool preserveTrailingEmptyLine)
        {
            if (sourceDirectories == null)
            {
                throw new ArgumentNullException(nameof(sourceDirectories));
            }

            if (sourcePathMappingAnchors == null)
            {
                throw new ArgumentNullException(nameof(sourcePathMappingAnchors));
            }

            this.sourceDirectories = sourceDirectories.ToList();
            this.sourcePathMappingAnchors = sourcePathMappingAnchors
                .Where(anchor => !string.IsNullOrWhiteSpace(anchor))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            this.preserveTrailingEmptyLine = preserveTrailingEmptyLine;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileReader" /> class.
        /// </summary>
        /// <param name="sourceDirectories">The source directories.</param>
        /// <param name="preserveTrailingEmptyLine">Indicates whether empty trailing line in source files should be preserved.</param>
        public LocalFileReader(IEnumerable<string> sourceDirectories, bool preserveTrailingEmptyLine)
            : this(sourceDirectories, Enumerable.Empty<string>(), preserveTrailingEmptyLine)
        {
        }

        /// <summary>
        /// Loads the file with the given path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="error">Error message if file reading failed, otherwise <code>null</code>.</param>
        /// <returns><code>null</code> if an error occurs, otherwise the lines of the file.</returns>
        public string[] LoadFile(string path, out string error)
        {
            string mappedPath = this.MapPath(path);

            try
            {
                if (!File.Exists(mappedPath))
                {
                    error = string.Format(CultureInfo.InvariantCulture, Resources.FileDoesNotExist, path);
                    return null;
                }

                string[] lines = this.ReadAllLinesPreserveTrailingEmpty(mappedPath);

                error = null;
                return lines;
            }
            catch (Exception ex)
            {
                error = string.Format(CultureInfo.InvariantCulture, Resources.ErrorDuringReadingFile, path, ex.GetExceptionMessageForDisplay());
                return null;
            }
        }

        private string MapPath(string path)
        {
            if (File.Exists(path))
            {
                return path;
            }

            if (path.StartsWith("/_") && DeterministicPathRegex.IsMatch(path))
            {
                path = path.Substring(path.IndexOf("/", 2) + 1);

                if (File.Exists(path))
                {
                    return path;
                }

                if (this.sourceDirectories.Count == 0)
                {
                    return this.MapPath(path, DeterministicSourceDirectories, this.sourcePathMappingAnchors);
                }
            }

            if (this.sourceDirectories.Count > 0)
            {
                return this.MapPath(path, this.sourceDirectories, this.sourcePathMappingAnchors);
            }

            return path;
        }

        private string MapPath(string path, IEnumerable<string> directories, IReadOnlyCollection<string> sourcePathMappingAnchors)
        {
            var fastMappedPath = this.TryMapPathWithAnchors(path, directories, sourcePathMappingAnchors);
            if (fastMappedPath != null)
            {
                return fastMappedPath;
            }

            /*
             * Search in source dirctories
             *
             * E.g. with source directory 'C:\agent\1\work\s' the following locations will be searched:
             * C:\agent\1\work\s\_\some\directory\file.cs
             * C:\agent\1\work\s\some\directory\file.cs
             * C:\agent\1\work\s\directory\file.cs
             * C:\agent\1\work\s\file.cs
             */
            string[] parts = path.Split('/', '\\');

            foreach (var sourceDirectory in directories)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    string combinedPath = sourceDirectory;

                    for (int j = i; j < parts.Length; j++)
                    {
                        combinedPath = Path.Combine(combinedPath, parts[j]);
                    }

                    if (File.Exists(combinedPath))
                    {
                        return combinedPath;
                    }
                }
            }

            return path;
        }

        private string TryMapPathWithAnchors(string path, IEnumerable<string> directories, IReadOnlyCollection<string> sourcePathMappingAnchors)
        {
            if (sourcePathMappingAnchors == null || sourcePathMappingAnchors.Count == 0)
            {
                return null;
            }

            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            foreach (var anchor in sourcePathMappingAnchors)
            {
                int anchorIndex = Array.FindLastIndex(parts, part => string.Equals(part, anchor, StringComparison.OrdinalIgnoreCase));
                if (anchorIndex < 0 || anchorIndex >= parts.Length - 1)
                {
                    continue;
                }

                foreach (var sourceDirectory in directories)
                {
                    string combinedPath = sourceDirectory;
                    for (int i = anchorIndex + 1; i < parts.Length; i++)
                    {
                        combinedPath = Path.Combine(combinedPath, parts[i]);
                    }

                    if (File.Exists(combinedPath))
                    {
                        return combinedPath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Reads all lines of a file, preserving a trailing empty line if present.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The lines of the file.</returns>
        private string[] ReadAllLinesPreserveTrailingEmpty(string path)
        {
            var lines = new List<string>();

            var encoding = FileHelper.GetEncoding(path);
            using (var reader = new StreamReader(path, encoding))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                // If the file ends with a line break, ReadLine() returns null after the last empty line.
                // We need to check if the file ends with a line break and add an empty string if so.
                // Use preserveTrailingEmptyLine option here to stick to the default behavior
                if (this.preserveTrailingEmptyLine
                    && lines.Count > 0
                    && this.FileEndsWithNewline(path))
                {
                    lines.Add(string.Empty);
                }
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Checks if the file ends with a newline character.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>True if the file ends with a newline character, otherwise false.</returns>
        private bool FileEndsWithNewline(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length == 0)
                {
                    return false;
                }

                fs.Seek(-1, SeekOrigin.End);
                int lastByte = fs.ReadByte();
                return lastByte == '\n' || lastByte == '\r';
            }
        }
    }
}
