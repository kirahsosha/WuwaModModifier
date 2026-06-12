namespace WuwaModModifier.Model
{
    /// <summary>
    /// Toggle creation mode — determines whether to bind a new toggle to an existing
    /// parameter or create a new parameter alongside it.
    /// Extracted from MainViewModel private enum.
    /// </summary>
    public enum ToggleCreationMode
    {
        /// <summary>Use an existing parameter for the new toggle binding.</summary>
        ExistingParameter,

        /// <summary>Create a new parameter for the toggle.</summary>
        NewParameter
    }
}
