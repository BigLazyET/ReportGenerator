using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Palmmedia.ReportGenerator.Core.Parser.Filtering
{
    public class MethodFilter : DefaultFilter
    {
        public MethodFilter(IEnumerable<string> filters) : base(filters)
        {
        }

        protected override Regex CreateFilterRegex(string filter)
        {
            filter = filter.Substring(1);
            filter = filter.Replace("*", "$$$*");
            filter = Regex.Escape(filter);
            filter = filter.Replace(@"\$\$\$\*", ".*");

            var regex = new Regex($"{filter}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return regex;
        }
    }
}
