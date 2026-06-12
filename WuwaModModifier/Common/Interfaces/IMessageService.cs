namespace WuwaModModifier.Common
{
    public interface IMessageService
    {
        void ShowInfo(string message, string? caption = null);
        void ShowError(string message, string? caption = null);
        bool Confirm(string message, string? caption = null);
    }
}


