using WuwaModModifier.Model;

namespace WuwaModModifier.Common
{
    public interface IModConfigAnalysisService
    {
        ModConfigAnalysisResult Analyze(ModConfigDocument document);
        ModConfigAnalysisResult AnalyzeFile(string filePath);
    }
}