using WuwaModModifier.Common;
using Xunit;

namespace UnitTests
{
    public class DefaultSystemVariableStrategyTests
    {
        private readonly ISystemVariableStrategy _strategy = new DefaultSystemVariableStrategy();

        [Theory]
        [InlineData("$required_something")]
        [InlineData("$required_hat")]
        [InlineData("$REQUIRED_BRA")]
        public void IsInternal_ShouldReturnTrue_ForRequiredPrefix(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$object_detected")]
        [InlineData("$OBJECT_SOMETHING")]
        public void IsInternal_ShouldReturnTrue_ForObjectPrefix(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$mesh_override")]
        [InlineData("$MESH_DATA")]
        public void IsInternal_ShouldReturnTrue_ForMeshPrefix(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$shapekey_something")]
        public void IsInternal_ShouldReturnTrue_ForShapekeyPrefix(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$merge_status")]
        [InlineData("$MERGE_STATUS")]
        public void IsInternal_ShouldReturnTrue_ForMergeStatus(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("something\\WWMIv1\\other")]
        [InlineData("prefix\\WWMIv1\\suffix")]
        public void IsInternal_ShouldReturnTrue_ForWwmiPath(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$mod_id")]
        [InlineData("$MOD_ID")]
        public void IsInternal_ShouldReturnTrue_ForModId(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$state_id")]
        [InlineData("$STATE_ID")]
        public void IsInternal_ShouldReturnTrue_ForStateId(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$mod_enabled")]
        [InlineData("$MOD_ENABLED")]
        public void IsInternal_ShouldReturnTrue_ForModEnabled(string variableName)
        {
            Assert.True(_strategy.IsInternalSystemVariable(variableName));
        }

        [Theory]
        [InlineData("$hat")]
        [InlineData("$bra")]
        [InlineData("$swapvar_clothing")]
        [InlineData("$draw_component_1")]
        [InlineData("$random_user_param")]
        public void IsInternal_ShouldReturnFalse_ForUserVariables(string variableName)
        {
            Assert.False(_strategy.IsInternalSystemVariable(variableName));
        }

        [Fact]
        public void IsInternal_ShouldReturnFalse_ForNullPrefixLookalike()
        {
            Assert.False(_strategy.IsInternalSystemVariable("required_something"));
        }
    }
}
