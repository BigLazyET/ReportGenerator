using System.Collections.Generic;

namespace Palmmedia.ReportGenerator.Core
{
    /// <summary>
    /// CoverageLine.
    /// </summary>
    public static class CoverageLine
    {
        public static IDictionary<string, List<int>> ChangeLines { get; set; } = new Dictionary<string, List<int>>();
    }
}