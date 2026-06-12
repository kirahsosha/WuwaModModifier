namespace WuwaModModifier.Model
{
    /// <summary>
    /// Common contract for types that can be selected as a visibility-binding
    /// target, replacing the previous untyped <c>object?</c> field.
    /// </summary>
    public interface IVisibilityBindingTarget
    {
        string ParameterName { get; }
        string DisplayName { get; }
    }
}
