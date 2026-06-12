namespace WuwaModModifier.Common.Constants
{
    /// <summary>
    /// Centralised INI-config-related constants that replace hard-coded magic strings
    /// scattered across the codebase.
    /// </summary>
    public static class ConfigConstants
    {
        public const string FileExtension = ".ini";
        public const string MultiConfigSuffixSeparator = "_p";
        public const string ValueSeparator = " | ";
        public const string BinaryValueFalse = "0";
        public const string BinaryValueTrue = "1";

        public static class SystemVariablePrefixes
        {
            public const string Required = "$required_";
            public const string Object = "$object_";
            public const string Mesh = "$mesh_";
            public const string Draw = "$draw_";
            public const string SwapVar = "$swapvar_";
            public const string Component = "$component_";
            public const string Model = "$model_";
            public const string Texture = "$texture_";
        }

        public static class ResourceDirectoryNames
        {
            public const string Data = "Data";
            public const string Textures = "Textures";
            public const string Meshes = "Meshes";
            public const string Bin = "Bin";
        }

        public static class ToggleTypeMarkers
        {
            public const string Cycle = "cycle";
        }
    }
}
