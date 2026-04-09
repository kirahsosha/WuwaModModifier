using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WuwaModModifier.Common;
using WuwaModModifier.Model;
using WuwaModModifier.Data.ViewModels;

namespace WuwaModModifier.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IFileSystemService _fileSystem;
        private readonly IMessageService _messages;

        private string _modFolderPath;
        private string _wwmiFolderPath;
        private string _otherFolderPath;
        private bool _ignoreWeapon;
        private bool _ignoreOther;
        private List<WuwaMods> _allMods;
        private List<WuwaMod> _wwmiMods;
        private ObservableCollection<DirectoryItemViewModel> _directoryItems;
        private DirectoryItemViewModel? _selectedDirectoryItem;

        public MainViewModel()
            : this(new FileSystemService(), new MessageService())
        {
        }

        public MainViewModel(IFileSystemService fileSystem, IMessageService messages)
        {
            _fileSystem = fileSystem;
            _messages = messages;

            _modFolderPath = AppConfig.DefaultModPath;
            _wwmiFolderPath = AppConfig.DefaultWwmiPath;
            _otherFolderPath = AppConfig.OtherFolderPath;
            _ignoreWeapon = true;
            _ignoreOther = true;
            _allMods = new List<WuwaMods>();
            _wwmiMods = new List<WuwaMod>();
            _directoryItems = new ObservableCollection<DirectoryItemViewModel>();

            BtnModPathCommand = new RelayCommand(ExecuteBtnModPath);
            BtnWwmiPathCommand = new RelayCommand(ExecuteBtnWwmiPath);
            BtnClearAllModsCommand = new RelayCommand(ExecuteBtnClearAllMods);
            BtnRandomLoadAllModsCommand = new RelayCommand(ExecuteBtnRandomLoadAllMods);
            BtnLoadSelectedModsCommand = new RelayCommand(ExecuteBtnLoadSelectedMods);
            OpenModFolderCommand = new RelayCommand(ExecuteOpenModFolderBySelected, CanOpenModFolderBySelected);
        }

        public DirectoryItemViewModel? SelectedDirectoryItem
        {
            get => _selectedDirectoryItem;
            set => SetProperty(ref _selectedDirectoryItem, value);
        }

        public string ModFolderPath
        {
            get => _modFolderPath;
            set => SetProperty(ref _modFolderPath, value);
        }

        public string WwmiFolderPath
        {
            get => _wwmiFolderPath;
            set => SetProperty(ref _wwmiFolderPath, value);
        }

        public bool IgnoreWeapon
        {
            get => _ignoreWeapon;
            set => SetProperty(ref _ignoreWeapon, value);
        }

        public bool IgnoreOther
        {
            get => _ignoreOther;
            set => SetProperty(ref _ignoreOther, value);
        }

        public ObservableCollection<DirectoryItemViewModel> DirectoryItems
        {
            get => _directoryItems;
            set => SetProperty(ref _directoryItems, value);
        }

        public ICommand BtnModPathCommand { get; }
        public ICommand BtnWwmiPathCommand { get; }
        public ICommand BtnClearAllModsCommand { get; }
        public ICommand BtnRandomLoadAllModsCommand { get; }
        public ICommand BtnLoadSelectedModsCommand { get; }
        public ICommand OpenModFolderCommand { get; }

        /// <summary>
        /// BtnModPath Click Event
        /// </summary>
        private void ExecuteBtnModPath()
        {
            if (GetMod(out var characterCount, out var modCount))
            {
                // 加载目录树
                LoadDirectoryTree();
                _messages.ShowInfo($"共找到 {characterCount} 名角色共 {modCount} 个MOD。");
            }
            else
            {
                _messages.ShowInfo("未找到MOD。");
            }
        }

        /// <summary>
        /// BtnWwmiPath Click Event
        /// </summary>
        private void ExecuteBtnWwmiPath()
        {
            if (GetWwmi(out var modCount))
            {
                // 选中已加载的MOD
                SelectLoadedMods();
                _messages.ShowInfo($"共找到 {modCount} 个MOD。");
            }
            else
            {
                _messages.ShowInfo("未找到MOD。");
            }
        }

        /// <summary>
        /// BtnClearAllMods Click Event
        /// </summary>
        private void ExecuteBtnClearAllMods()
        {
            if (ClearWwmi(out var modCount))
            {
                _messages.ShowInfo($"共删除 {modCount} 个MOD。");
            }
            else
            {
                _messages.ShowInfo("未找到MOD。");
            }
        }

        /// <summary>
        /// BtnRandomLoadAllMods Click Event
        /// </summary>
        private void ExecuteBtnRandomLoadAllMods()
        {
            if (ClearWwmi(out var oldModCount))
            {
                if (RandomLoadMods(out var newModCount, out var loadedMods))
                {
                    _messages.ShowInfo($"共删除 {oldModCount} 个MOD,加载 {newModCount} 个MOD。");
                    _wwmiMods = loadedMods;
                    // 选中加载的MOD
                    SelectRandomLoadedMods(loadedMods);
                }
                else
                {
                    _messages.ShowError("加载新MOD失败。");
                }
            }
            else
            {
                _messages.ShowError("删除已安装MOD失败。");
            }
        }

        /// <summary>
        /// BtnLoadSelectedMods
        /// </summary>
        private void ExecuteBtnLoadSelectedMods()
        {
            LoadSelectedMods();
        }

        private void ExecuteOpenModFolderBySelected()
        {
            ExecuteOpenModFolder(SelectedDirectoryItem);
        }

        private bool CanOpenModFolderBySelected()
        {
            return CanOpenModFolder(SelectedDirectoryItem);
        }

        private bool GetMod(out int characterCount, out int modCount)
        {
            characterCount = 0;
            modCount = 0;

            string path = ModFolderPath;
            _allMods = new List<WuwaMods>();
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var folders = _fileSystem.GetDirectories(path);
                    foreach (var folder in folders)
                    {
                        if (IgnoreOther && folder.Equals(_otherFolderPath))
                        {
                            continue;
                        }

                        var characterName = ModPathHelper.GetCharacterNameFromFolder(folder);
                        if (string.IsNullOrWhiteSpace(characterName))
                        {
                            continue;
                        }

                        var mods = new WuwaMods
                        {
                            CharacterName = characterName,
                            Folder = folder,
                            Mods = new List<WuwaMod>()
                        };

                        var modFolders = _fileSystem.GetDirectories(folder);
                        foreach (var modFolder in modFolders)
                        {
                            var folderName = ModPathHelper.GetCharacterNameFromFolder(modFolder);
                            var (id, modName) = ModPathHelper.ParseModFolderName(folderName);

                            var mod = new WuwaMod
                            {
                                CharacterName = characterName,
                                FullPath = modFolder,
                                Id = id,
                                ModName = modName
                            };
                            mods.Mods.Add(mod);
                        }
                        modCount += modFolders.Count();
                        _allMods.Add(mods);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("GetMod: ", ex);
                    return false;
                }

                if (modCount == 0)
                {
                    return true;
                }
                else
                {
                    characterCount = _allMods.Count();
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        private bool GetWwmi(out int modCount)
        {
            modCount = 0;
            string path = WwmiFolderPath;
            _wwmiMods = new List<WuwaMod>();
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    var folders = _fileSystem.GetDirectories(path);
                    foreach (var folder in folders)
                    {
                        var (characterName, id, modName) = ModPathHelper.ParseWwmiFolderPath(folder);
                        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(modName))
                        {
                            continue;
                        }

                        if (characterName.ToLower().Contains("weapon"))
                        {
                            continue;
                        }

                        var mod = new WuwaMod
                        {
                            CharacterName = characterName,
                            FullPath = folder,
                            Id = id,
                            ModName = modName
                        };

                        _wwmiMods.Add(mod);
                        modCount++;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.Error("GetWwmi: ", ex);
                    return false;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool ClearWwmi(out int modCount)
        {
            modCount = 0;
            if (_wwmiMods.Count == 0)
            {
                if (!GetWwmi(out modCount))
                {
                    return false;
                }
            }
            try
            {
                foreach (var mod in _wwmiMods)
                {
                    _fileSystem.DeleteDirectory(mod.FullPath, true);
                }
                modCount = _wwmiMods.Count;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error("ClearWwmi: ", ex);
                return false;
            }
        }

        private bool RandomLoadMods(out int modCount, out List<WuwaMod> loadedMods)
        {
            modCount = 0;
            loadedMods = new List<WuwaMod>();
            
            if (_allMods.Count == 0)
            {
                if (!GetMod(out _, out _))
                {
                    return false;
                }
            }
            try
            {
                var ran = new Random();
                foreach (var mods in _allMods)
                {
                    if (IgnoreWeapon && mods.CharacterName.Contains("weapon")) continue;
                    var count = mods.Mods.Count;
                    if (count == 0) continue;
                    var index = ran.Next(count);
                    var mod = mods.Mods[index];
                    _fileSystem.CopyDirectory(mod.FullPath, $"{_wwmiFolderPath}\\[{mod.CharacterName}][{mod.Id}]{mod.ModName}");
                    loadedMods.Add(mod);
                    modCount++;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error("RandomLoadMods: ", ex);
                return false;
            }
        }

        private void LoadDirectoryTree()
        {
            DirectoryItems.Clear();
            if (_allMods == null || _allMods.Count == 0)
                return;

            foreach (var mods in _allMods)
            {
                if (IgnoreWeapon && mods.CharacterName.Contains("weapon"))
                    continue;

                var characterItem = new DirectoryItemViewModel
                {
                    Name = mods.CharacterName,
                    FullPath = mods.Folder,
                    IsDirectory = true,
                    IsChecked = false
                };

                foreach (var mod in mods.Mods)
                {
                    var modItem = new DirectoryItemViewModel
                    {
                        Name = mod.ModName,
                        Id = mod.Id,
                        FullPath = mod.FullPath,
                        IsDirectory = false,
                        IsChecked = false,
                        Parent = characterItem
                    };
                    characterItem.Children.Add(modItem);
                }

                DirectoryItems.Add(characterItem);
            }
        }

        private void LoadSelectedMods()
        {
            var selectedMods = new List<DirectoryItemViewModel>();
            CollectSelectedItems(DirectoryItems, selectedMods);

            if (selectedMods.Count == 0)
            {
                _messages.ShowInfo("请至少选择一个MOD。");
                return;
            }

            if (selectedMods.GroupBy(s => s.Parent).Where(s => s.Count() > 1).Count() > 0)
            {
                _messages.ShowInfo("每个角色只能选择一个MOD。");
                return;
            }

            if (_wwmiMods.Count == 0)
            {
                if (!GetWwmi(out _))
                {
                    _messages.ShowError("加载已安装MOD失败。");
                    return;
                }
            }

            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;

            try
            {
                foreach (var item in selectedMods)
                {
                    if (!item.IsDirectory && !string.IsNullOrEmpty(item.FullPath))
                    {
                        try
                        {
                            var modName = item.Name;
                            var characterName = item.Parent?.Name ?? "";
                            var id = item.Id;
                            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(id))
                            {
                                failCount++;
                                continue;
                            }

                            if (!Directory.Exists(item.FullPath))
                            {
                                failCount++;
                                continue;
                            }

                            var destinationPath = $"{_wwmiFolderPath}\\[{characterName}][{id}]{modName}";
                            var exist = _wwmiMods.Where(s => s.CharacterName == characterName).FirstOrDefault();
                            // 如果目标目录不存在，复制
                            if (exist == null)
                            {
                                _fileSystem.CopyDirectory(item.FullPath, destinationPath);
                            }
                            // 如果目标目录已存在，跳过
                            else if (exist.Id == id)
                            {
                                skipCount++;
                                continue;
                            }
                            // 存在相同characterName的其他MOD，先删除再复制
                            else
                            {
                                if (_fileSystem.DirectoryExists(exist.FullPath))
                                {
                                    _fileSystem.DeleteDirectory(exist.FullPath, true);
                                }
                                _fileSystem.CopyDirectory(item.FullPath, destinationPath);
                            }
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            LogManager.Error($"LoadSelectedMods - Failed to load MOD {item.Name}: ", ex);
                            failCount++;
                        }
                    }
                }

                //更新已安装MOD
                if (successCount > 0 && !GetWwmi(out _))
                {
                    _messages.ShowError("加载已安装MOD失败。");
                    return;
                }

                _messages.ShowInfo($"成功加载 {successCount} 个MOD{(skipCount > 0 ? $"，跳过 {skipCount} 个" : "")}{(failCount > 0 ? $"，失败 {failCount} 个" : "")}。");
            }
            catch (Exception ex)
            {
                LogManager.Error("LoadSelectedMods: ", ex);
                _messages.ShowError($"加载MOD时发生错误：{ex.Message}");
            }
        }

        private void CollectSelectedItems(ObservableCollection<DirectoryItemViewModel> items, List<DirectoryItemViewModel> selectedItems)
        {
            foreach (var item in items)
            {
                if (item.IsChecked && !item.IsDirectory)
                {
                    selectedItems.Add(item);
                }
                if (item.Children.Count > 0)
                {
                    CollectSelectedItems(item.Children, selectedItems);
                }
            }
        }

        private void SelectLoadedMods()
        {
            // 首先清除所有选中状态
            ClearSelection(DirectoryItems);

            if (_wwmiMods == null || _wwmiMods.Count == 0)
                return;

            // 遍历目录树，选中已加载的MOD
            foreach (var wwmiMod in _wwmiMods)
            {
                var characterItem = DirectoryItems.Where(d => d.Name == wwmiMod.CharacterName).FirstOrDefault();
                if(characterItem != null)
                {
                    // 展开角色节点
                    characterItem.IsSelected = true;

                    // 在子节点中查找匹配的MOD
                    // WWMI中的MOD名称格式：[ID]ModName（去掉[CharacterName]后的部分）
                    // 目录树中的MOD名称格式：[ID]ModName
                    // 直接比较MOD名称（都是[ID]ModName格式）
                    var modItem = characterItem.Children.Where(c => c.Name == wwmiMod.ModName && c.Id == wwmiMod.Id).FirstOrDefault();
                    if(modItem != null)
                    {
                        modItem.IsSelected = true;
                        modItem.IsChecked = true;
                    }
                }
            }
        }

        private void ClearSelection(ObservableCollection<DirectoryItemViewModel> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;
                item.IsChecked = false;
                if (item.Children.Count > 0)
                {
                    ClearSelection(item.Children);
                }
            }
        }

        private void SelectRandomLoadedMods(List<WuwaMod> loadedMods)
        {
            // 首先清除所有选中状态
            ClearSelection(DirectoryItems);

            if (loadedMods == null || loadedMods.Count == 0)
                return;

            // 如果目录树未加载，先加载
            if (DirectoryItems.Count == 0)
            {
                LoadDirectoryTree();
            }

            // 遍历目录树，选中随机加载的MOD
            foreach (var loadedMod in loadedMods)
            {
                foreach (var characterItem in DirectoryItems)
                {
                    if (characterItem.Name == loadedMod.CharacterName)
                    {
                        // 展开角色节点
                        characterItem.IsSelected = true;

                        // 在子节点中查找匹配的MOD
                        foreach (var modItem in characterItem.Children)
                        {
                            // 比较MOD名称（都是[ID]ModName格式）
                            if (modItem.Name == loadedMod.ModName)
                            {
                                modItem.IsSelected = true;
                                modItem.IsChecked = true;
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void ExecuteOpenModFolder(DirectoryItemViewModel? item)
        {
            if (item == null || string.IsNullOrEmpty(item.FullPath))
                return;

            try
            {
                if (_fileSystem.DirectoryExists(item.FullPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = item.FullPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    _messages.ShowError("文件夹不存在。");
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("OpenModFolder: ", ex);
                _messages.ShowError($"打开文件夹时发生错误：{ex.Message}");
            }
        }

        private bool CanOpenModFolder(DirectoryItemViewModel? item)
        {
            return item != null && !string.IsNullOrEmpty(item.FullPath);
        }
    }
}

