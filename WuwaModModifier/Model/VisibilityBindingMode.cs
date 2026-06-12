namespace WuwaModModifier.Model
{
    /// <summary>
    /// Visibility binding mode — determines how a visibility item is bound to a parameter.
    /// Extracted from MainViewModel private enum.
    /// </summary>
    public enum VisibilityBindingMode
    {
        /// <summary>Bind to an existing parameter.</summary>
        ExistingParameter,

        /// <summary>Reuse an existing toggle's keybinding for visibility control.</summary>
        ExistingToggle,

        /// <summary>Create a new parameter with toggle keybinding.</summary>
        NewParameterAndToggle,

        /// <summary>Remove an existing visibility binding.</summary>
        RemoveExistingBinding
    }
}
