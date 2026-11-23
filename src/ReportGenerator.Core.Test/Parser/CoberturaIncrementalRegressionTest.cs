using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Palmmedia.ReportGenerator.Core.Parser;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using Palmmedia.ReportGenerator.Core.Parser.Filtering;
using Xunit;

namespace Palmmedia.ReportGenerator.Core.Test.Parser
{
    [Collection("FileManager")]
    public sealed class CoberturaIncrementalRegressionTest : IDisposable
    {
        private readonly IDictionary<string, List<int>> previous = CoverageLine.ChangeLines;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "coveragex-parser-" + Guid.NewGuid().ToString("N"));
        private readonly DefaultFilter filter = new DefaultFilter(Array.Empty<string>());

        public CoberturaIncrementalRegressionTest()
        {
            Directory.CreateDirectory(this.directory);
            CoverageLine.ChangeLines = new Dictionary<string, List<int>>();
        }

        [Fact]
        public void RootAndWrapper_PreserveExactIncrementalHitsBranchesAndMethods()
        {
            var root = this.CreateReport();
            var rootResult = this.Parse(root.ToString());
            var wrappedResult = this.Parse(new XElement("root", new XElement(root)).ToString());
            Assert.Equal("Cobertura", rootResult.ParserName);
            var direct = rootResult.Assemblies.Single().Classes.Single().Files.Single();
            var wrapped = wrappedResult.Assemblies.Single().Classes.Single().Files.Single();
            Assert.Equal(direct.LineCoverage, wrapped.LineCoverage);
            Assert.Equal(direct.LineVisitStatus, wrapped.LineVisitStatus);
            Assert.Equal(10, direct.LineCoverage.Count);
            Assert.Equal(5, direct.LineCoverage[2]);
            Assert.Equal(0, direct.LineCoverage[5]);
            Assert.Equal(-1, direct.LineCoverage[9]);
            Assert.Equal(LineVisitStatus.Covered, direct.LineVisitStatus[2]);
            Assert.Equal(LineVisitStatus.NotCovered, direct.LineVisitStatus[5]);
            Assert.Equal(LineVisitStatus.NotCoverable, direct.LineVisitStatus[9]);
            Assert.Single(direct.BranchesByLine);
            Assert.Equal(2, direct.BranchesByLine[2].Count);
            Assert.All(direct.BranchesByLine[2], branch => Assert.True(branch.BranchVisits > 0));
            Assert.Equal(direct.BranchesByLine[2].Select(b => b.BranchVisits), wrapped.BranchesByLine[2].Select(b => b.BranchVisits));
            var metrics = direct.MethodMetrics.Single().Metrics.Select(m => m.Value).ToArray();
            Assert.Contains(75m, metrics);
            Assert.Contains(50m, metrics);
            Assert.Contains(3m, metrics);
            Assert.Equal(metrics, wrapped.MethodMetrics.Single().Metrics.Select(m => m.Value));
        }

        [Theory]
        [InlineData("")]
        [InlineData("<!-- after -->")]
        public void BomAndDtd_KeepValidRootSemantics(string tail)
        {
            var report = this.CreateReport().ToString().Replace("name=\"App\"", "name=\"&assembly;\"");
            var result = this.Parse("<!DOCTYPE coverage [<!ENTITY assembly 'App'>]>" + report + tail, true);
            Assert.Equal("Cobertura", result.ParserName);
            Assert.Equal("App", result.Assemblies.Single().Name);
            Assert.Single(result.Assemblies.Single().Classes.Single().Files);
        }

        [Theory]
        [InlineData("<broken>")]
        [InlineData("<second/>")]
        [InlineData("garbage")]
        public void MalformedTail_DoesNotProducePartialCoverage(string tail)
        {
            var result = this.Parse(this.CreateReport().ToString() + tail);
            Assert.Empty(result.Assemblies);
        }

        [Theory]
        [InlineData("<PartCoverReport/>", true)]
        [InlineData("<CoverageSession/>", false)]
        [InlineData("<Root ReportType='dotCover'/>", false)]
        [InlineData("<report name='JaCoCo'/>", false)]
        public void EarlierDescendantMarker_PreservesLadderPriority(string marker, bool unsupported)
        {
            var root = this.CreateReport();
            root.Add(XElement.Parse(marker));
            if (unsupported)
            {
                Assert.Throws<UnsupportedParserException>(() => this.Parse(root.ToString()));
            }
            else
            {
                Assert.NotEqual("Cobertura", this.Parse(root.ToString()).ParserName);
            }
        }

        [Fact]
        public void UnsupportedMarkerBeforeBrokenTail_PreservesEarlyUnsupportedError()
        {
            Assert.Throws<UnsupportedParserException>(() => this.Parse("<coverage a='1' b='2'><PartCoverReport/><broken>"));
        }

        [Fact]
        public void MultipleReports_PreserveBothResultsAndDuplicateHits()
        {
            var root = this.CreateReport();
            var result = this.Parse(new XElement("root", root, new XElement(root)).ToString());
            Assert.Equal("MultiReport (2x Cobertura)", result.ParserName);
            Assert.Equal(10, result.Assemblies.Single().Classes.Single().Files.Single().LineCoverage[2]);
        }

        [Fact]
        public void MissingOrNonOverlappingChanges_DoNotExpandScope()
        {
            var root = this.CreateReport();
            CoverageLine.ChangeLines.Clear();
            Assert.Empty(this.Parse(root.ToString()).Assemblies.Single().Classes);
            CoverageLine.ChangeLines[Path.Combine(this.directory, "Sample.cs")] = new List<int> { -1, 0, 100 };
            Assert.Empty(this.Parse(root.ToString()).Assemblies.Single().Classes);
        }

        [Fact]
        public void MultipleAndNestedClasses_KeepPerClassIntersection()
        {
            var root = this.CreateReport();
            var classes = root.Descendants("classes").Single();
            var original = classes.Element("class");
            classes.Add(new XElement("class", new XAttribute("name", "Other"), new XAttribute("filename", Path.Combine(this.directory, "Sample.cs")),
                new XElement("methods"), new XElement("lines", new XElement("line", new XAttribute("number", 8), new XAttribute("hits", 4)))));
            var nested = new XElement(original);
            nested.SetAttributeValue("name", "A/Nested");
            classes.Add(nested);
            var result = this.Parse(root.ToString());
            var selectedClass = Assert.Single(result.Assemblies.Single().Classes);
            Assert.Equal("A", selectedClass.Name);
            Assert.Equal(10, selectedClass.Files.Single().LineCoverage[2]);
        }

        [Fact]
        public void UncoveredChangedClass_RemainsInReport()
        {
            var root = this.CreateReport();
            CoverageLine.ChangeLines[Path.Combine(this.directory, "Sample.cs")] = new List<int> { 5 };

            var selectedClass = Assert.Single(this.Parse(root.ToString()).Assemblies.Single().Classes);

            Assert.Equal(0, selectedClass.CoveredLines);
            Assert.Equal(1, selectedClass.CoverableLines);
            Assert.Equal(0, selectedClass.Files.Single().LineCoverage[5]);
        }

        private XElement CreateReport()
        {
            var file = Path.Combine(this.directory, "Sample.cs");
            CoverageLine.ChangeLines[file] = new List<int> { -1, 0, 2, 2, 5, 100 };
            var report = XElement.Parse("<coverage version='1' timestamp='1'><sources/><packages><package name='App'><classes><class name='A' filename='PLACEHOLDER'>"
                + "<methods><method name='M' signature='()' line-rate='0.75' branch-rate='0.5' complexity='3'><lines><line number='2' hits='1'/><line number='5' hits='0'/></lines></method></methods>"
                + "<lines><line number='2' hits='-1' branch='true' condition-coverage='0% (0/2)'/><line number='2' hits='3' branch='true' condition-coverage='100% (2/2)'/>"
                + "<line number='2' hits='2' branch='true' condition-coverage='50% (1/2)'/><line number='5' hits='0'/><line number='9' hits='-1' branch='true' condition-coverage='0% (0/2)'/>"
                + "</lines></class></classes></package></packages></coverage>");
            report.Descendants("class").Single().SetAttributeValue("filename", file);
            return report;
        }

        private ParserResult Parse(string content, bool bom = false)
        {
            var path = Path.Combine(this.directory, Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(path, content, new UTF8Encoding(bom));
            return new CoverageReportParser(1, 1, Array.Empty<string>(), this.filter, this.filter, this.filter).ParseFiles(new[] { path });
        }

        public void Dispose()
        {
            CoverageLine.ChangeLines = this.previous;
            Directory.Delete(this.directory, true);
        }
    }
}
