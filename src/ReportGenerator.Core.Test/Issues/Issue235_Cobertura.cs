using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NSubstitute;
using Palmmedia.ReportGenerator.Core;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using Xunit;

namespace Palmmedia.ReportGenerator.Core.Test.Issues
{
    [Collection("FileManager")]
    public class Issue235_Cobertura
    {
        private const string Report = @"<?xml version=""1.0"" encoding=""utf-8""?>
<coverage line-rate=""0.752"" branch-rate=""0.739"" version=""1.9"" timestamp=""1556998206"" lines-covered=""185"" lines-valid=""246"" branches-covered=""17"" branches-valid=""23"">
  <sources>
    <source></source>
  </sources>
  <packages>
    <package name=""OutwardPaymentDocumentProcessing"" line-rate=""0.542"" branch-rate=""0.714"" complexity=""42"">
      <classes>
        <class name=""OutwardPaymentDocumentProcessing.HttpStart/&lt;Run&gt;d__0"" filename=""/Users/likasem/Projects/Partners/StandardBank/CIBOperationsIA/OutwardPaymentDocumentProcessing/HttpStart.cs"" line-rate=""1"" branch-rate=""1"" complexity=""1"">
          <lines>
            <line number=""20"" hits=""1"" branch=""False"" />
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>";

        [Fact]
        public void NestedClassWithoutParentIsPresent()
        {
            var filter = Substitute.For<IFilter>();
            filter.IsElementIncludedInReport(Arg.Any<string>()).Returns(true);

            var report = XDocument.Parse(Report);

            var previousChanges = CoverageLine.ChangeLines;
            try
            {
                CoverageLine.ChangeLines = new Dictionary<string, List<int>>
                {
                    [report.Descendants("class").Single().Attribute("filename").Value] = new List<int> { 20 }
                };
                var parserResult = new CoberturaParser(filter, filter, filter).Parse(report.Root);

                Assert.Equal("OutwardPaymentDocumentProcessing.HttpStart", parserResult.Assemblies.First().Classes.First().Name);
            }
            finally
            {
                CoverageLine.ChangeLines = previousChanges;
            }
        }
    }
}
