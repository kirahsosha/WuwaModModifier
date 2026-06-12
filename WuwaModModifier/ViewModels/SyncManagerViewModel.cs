using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Manages config sync and save operations between Mod and WWMI directories.
    /// Extracted from MainViewModel as part of R-02 refactoring.
    /// </summary>
    public class SyncManagerViewModel : ViewModelBase
    {
        private readonly IMainViewModelSession _session;
        private readonly IFileSystemService _fileSystem;
        private readonly IMessageService _messages;
        private readonly IAppConfigService _appConfig;
        private readonly IModConfigUpdateService _configUpdateService;

        public SyncManagerViewModel(
            IMainViewModelSession session,
            IFileSystemService fileSystem,
            IMessageService messages,
            IAppConfigService appConfig,
            IModConfigUpdateService configUpdateService)
        {
            _session = session;
            _fileSystem = fileSystem;
            _messages = messages;
            _appConfig = appConfig;
            _configUpdateService = configUpdateService;

            SaveRawConfigCommand = new RelayCommand(ExecuteSaveRawConfig, CanSaveRawConfig);
            SaveConfigToModCommand = new RelayCommand(ExecuteSaveConfigToMod, CanSaveConfigToMod);
            SaveConfigToWwmiCommand = new RelayCommand(ExecuteSaveConfigToWwmi, CanSaveConfigToWwmi);
            SyncModToWwmiCommand = new RelayCommand(ExecuteSyncModToWwmi, CanSyncModToWwmi);
            SyncWwmiToModCommand = new RelayCommand(ExecuteSyncWwmiToMod, CanSyncWwmiToMod);
        }

        // ── Commands ──

        public ICommand SaveRawConfigCommand { get; }
        public ICommand SaveConfigToModCommand { get; }
        public ICommand SaveConfigToWwmiCommand { get; }
        public ICommand SyncModToWwmiCommand { get; }
        public ICommand SyncWwmiToModCommand { get; }

        // ── Public API for MainViewModel delegate ──

        internal bool CanSaveConfig(ModConfigSaveTarget saveTarget)
        {
            return _session.CurrentBuffer != null &&
                !string.IsNullOrWhiteSpace(_session.CurrentBuffer.SourcePath) &&
                (_session.HasPendingConfigChanges || _session.IsRawConfigDirty) &&
                (saveTarget != ModConfigSaveTarget.ModDirectory || !string.IsNullOrWhiteSpace(_session.ModFolderPath)) &&
                (saveTarget != ModConfigSaveTarget.WwmiDirectory || !string.IsNullOrWhiteSpace(_session.WwmiFolderPath));
        }

        internal bool CanSyncConfig(ModConfigSyncDirection direction)
        {
            return CanSyncConfigBase();
        }

        internal bool CanSyncConfigBase()
        {
            return !string.IsNullOrWhiteSpace(_session.ModFolderPath) &&
                !string.IsNullOrWhiteSpace(_session.WwmiFolderPath) &&
                !string.IsNullOrWhiteSpace(_session.SelectedConfigPath);
        }

        internal void OnPathPreviewChanged() { /* Handled by MainViewModel */ }

        // ── Private methods ──

        private void ExecuteSaveRawConfig()
        {
            ExecuteSaveConfig(_session.CurrentConfigSource);
        }

        private bool CanSaveRawConfig()
        {
            return CanSaveConfig(_session.CurrentConfigSource);
        }

        private void ExecuteSaveConfigToMod()
        {
            ExecuteSaveConfig(ModConfigSaveTarget.ModDirectory);
        }

        private bool CanSaveConfigToMod()
        {
            return CanSaveConfig(ModConfigSaveTarget.ModDirectory);
        }

        private void ExecuteSaveConfigToWwmi()
        {
            ExecuteSaveConfig(ModConfigSaveTarget.WwmiDirectory);
        }

        private bool CanSaveConfigToWwmi()
        {
            return CanSaveConfig(ModConfigSaveTarget.WwmiDirectory);
        }

        private void ExecuteSaveConfig(ModConfigSaveTarget saveTarget)
        {
            // Full implementation delegates to MainViewModel session
            if (_session.CurrentBuffer == null) return;

            try
            {
                var preview = _configUpdateService.PreviewSaveTarget(
                    _session.SelectedConfigPath, saveTarget,
                    _session.ModFolderPath, _session.WwmiFolderPath);

                var isCurrentSourceTarget = saveTarget == _session.CurrentConfigSource;
                var promptActionText = isCurrentSourceTarget
                    ? "即将把当前工作内容保存回当前显示来源文件"
                    : "即将把当前工作内容导出到目标文件";

                if (!_messages.Confirm(
                    $"{promptActionText}：\n{preview.TargetPath}\n\n该操作{(preview.TargetExists ? "会覆盖已有文件" : "将创建新文件")}，是否继续？",
                    "确认保存"))
                {
                    return;
                }

                ModConfigSaveResult saveResult;
                if (isCurrentSourceTarget)
                {
                    saveResult = _configUpdateService.SaveBuffer(
                        _session.CurrentBuffer, preview.TargetPath);
                    _session.ApplyBufferAnalysis(
                        _session.CurrentBuffer,
                        $"已保存到 {preview.TargetPath}。");
                }
                else
                {
                    saveResult = _configUpdateService.SaveBufferToTarget(
                        _session.CurrentBuffer,
                        saveTarget,
                        _session.ModFolderPath,
                        _session.WwmiFolderPath);
                }

                _messages.ShowInfo($"已保存到 {saveResult.TargetPath}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("SaveConfig: ", ex);
                _messages.ShowError($"保存配置失败：{ex.Message}");
            }
        }

        private void ExecuteSyncModToWwmi()
        {
            ExecuteSyncConfig(ModConfigSyncDirection.ModToWwmi);
        }

        private void ExecuteSyncWwmiToMod()
        {
            ExecuteSyncConfig(ModConfigSyncDirection.WwmiToMod);
        }

        private void ExecuteSyncConfig(ModConfigSyncDirection direction)
        {
            if (string.IsNullOrWhiteSpace(_session.SelectedConfigPath)) return;

            try
            {
                var preview = _configUpdateService.PreviewSync(
                    _session.SelectedConfigPath, direction,
                    _session.ModFolderPath, _session.WwmiFolderPath);

                if (!_messages.Confirm(
                    $"即将同步配置文件：\n源：{preview.SourcePath}\n目标：{preview.TargetPath}\n\n该操作{(preview.TargetExists ? "会覆盖已有文件" : "将创建新文件")}，是否继续？",
                    "确认同步"))
                {
                    return;
                }

                var syncResult = _configUpdateService.SyncConfig(
                    _session.SelectedConfigPath, direction,
                    _session.ModFolderPath, _session.WwmiFolderPath);

                _session.RefreshSelectedConfigAnalysis();
                _messages.ShowInfo($"已同步 {syncResult.SourcePath} -> {syncResult.TargetPath}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("SyncConfig: ", ex);
                _messages.ShowError($"同步配置失败：{ex.Message}");
            }
        }

        private bool CanSyncModToWwmi()
        {
            return CanSyncConfig(ModConfigSyncDirection.ModToWwmi);
        }

        private bool CanSyncWwmiToMod()
        {
            return CanSyncConfig(ModConfigSyncDirection.WwmiToMod);
        }
    }
}
