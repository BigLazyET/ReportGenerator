using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Palmmedia.ReportGenerator.Core.Parser.Filtering
{
    public class ClassFilter : DefaultFilter
    {
        public ClassFilter(IEnumerable<string> filters) : base(filters)
        {
        }

        protected override Regex CreateFilterRegex(string filter)
        {
            filter = filter.Substring(1);
            filter = filter.Replace("*", "$$$*");
            filter = Regex.Escape(filter);
            filter = filter.Replace(@"\$\$\$\*", ".*");

            var regex = new Regex($"{filter}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return regex;
        }
    }
}
