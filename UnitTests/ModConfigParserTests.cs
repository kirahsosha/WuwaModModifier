using System.IO;
using System.Linq;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace UnitTests
{
    public class ModConfigParserTests
    {
        [Fact]
        public void Parse_ShouldCaptureRootStatementsAndSections()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var content =
                "namespace = cx_Mod030\n" +
                "; comment\n" +
                "\n" +
                "[Constants]\n" +
                "global persist $key_1 = 1\n" +
                "\n" +
                "[Key key_1]\n" +
                "condition = $object_detected\n" +
                "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                "$key_1 = 0,1\n";

            var document = parser.Parse(content);

            Assert.Equal(3, document.RootStatements.Count);
            Assert.Equal(ModConfigStatementKind.Assignment, document.RootStatements[0].Kind);
            Assert.Equal("namespace", document.RootStatements[0].Name);
            Assert.Equal(2, document.Sections.Count);
            Assert.Equal("Constants", document.Sections[0].Name);
            Assert.Equal("Key key_1", document.Sections[1].Name);
            Assert.Equal(ModConfigStatementKind.GlobalDeclaration, document.Sections[0].Statements[0].Kind);
            Assert.Equal("$key_1", document.Sections[0].Statements[0].VariableName);
            var keyAssignment = document.Sections[1].Statements.Last(s => s.Kind != ModConfigStatementKind.BlankLine);
            Assert.Equal(ModConfigStatementKind.VariableAssignment, keyAssignment.Kind);
            Assert.Equal("$key_1", keyAssignment.VariableName);
        }

        [Fact]
        public void Parse_ShouldClassifyControlLinesCommentsAndUnknownLines()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var content =
                "[Section]\n" +
                "; comment\n" +
                "if $object_detected\n" +
                "post $object_detected = 0\n" +
                "endif\n" +
                "some odd line\n";

            var document = parser.Parse(content);
            var statements = document.Sections.Single().Statements;

            Assert.Equal(ModConfigStatementKind.Comment, statements[0].Kind);
            Assert.Equal(ModConfigStatementKind.ControlLine, statements[1].Kind);
            Assert.Equal("if", statements[1].Name);
            Assert.Equal(ModConfigStatementKind.ControlLine, statements[2].Kind);
            Assert.Equal("post", statements[2].Name);
            Assert.Equal(ModConfigStatementKind.ControlLine, statements[3].Kind);
            Assert.Equal(ModConfigStatementKind.UnknownLine, statements[4].Kind);
        }

        [Fact]
        public void Parse_ShouldPreserveMultipleKeyAssignmentsInSameSection()
        {
            var parser = new ModConfigParser(new FileSystemService());
            var content =
                "[KeyHold]\n" +
                "key = no_ctrl no_shift VK_LBUTTON\n" +
                "key = no_ctrl no_shift VK_RBUTTON\n" +
                "type = hold\n" +
                "$hold = 1\n";

            var document = parser.Parse(content);
            var statements = document.Sections.Single().Statements;
            var nonBlankStatements = statements.Where(s => s.Kind != ModConfigStatementKind.BlankLine).ToList();

            Assert.Equal(4, nonBlankStatements.Count);
            Assert.Equal(2, nonBlankStatements.Count(s => s.Kind == ModConfigStatementKind.Assignment && s.Name == "key"));
            Assert.Equal(ModConfigStatementKind.VariableAssignment, nonBlankStatements.Last().Kind);
        }

        [Fact]
        public void ParseFile_ShouldReadFromFileSystemService()
        {
            var tempFile = Path.GetTempFileName();

            try
            {
                File.WriteAllText(tempFile, "[Constants]\nglobal $a = 1\n");
                var parser = new ModConfigParser(new FileSystemService());

                var document = parser.ParseFile(tempFile);
                var nonBlankStatements = document.Sections[0].Statements.Where(s => s.Kind != ModConfigStatementKind.BlankLine).ToList();

                Assert.Single(document.Sections);
                Assert.Equal("Constants", document.Sections[0].Name);
                Assert.Single(nonBlankStatements);
                Assert.Equal(ModConfigStatementKind.GlobalDeclaration, nonBlankStatements[0].Kind);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}