using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public interface IModConfigParser
    {
        ModConfigDocument Parse(string content);
        ModConfigDocument ParseFile(string filePath);
    }
}