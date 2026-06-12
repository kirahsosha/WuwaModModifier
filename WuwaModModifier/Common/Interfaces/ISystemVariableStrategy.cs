namespace WuwaModModifier.Common
{
    /// <summary>
    /// Pluggable strategy for identifying WWMI-internal / system variables
    /// that should not be surfaced as user-facing parameters or toggles.
    /// </summary>
    public interface ISystemVariableStrategy
    {
        /// <summary>
        /// Returns <c>true</c> when <paramref name="variableName"/> is a
        /// reserved system variable.
        /// </summary>
        bool IsInternalSystemVariable(string variableName);
    }
}
