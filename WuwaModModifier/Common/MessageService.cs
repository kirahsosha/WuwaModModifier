using System.Windows;

namespace WuwaModModifier.Common
{
    /// <summary>
    /// 使用 WPF MessageBox 的消息服务实现
    /// </summary>
    public class MessageService : IMessageService
    {
        public void ShowInfo(string message, string? caption = null)
        {
            MessageBox.Show(message, caption ?? "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string? caption = null)
        {
            MessageBox.Show(message, caption ?? "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}


