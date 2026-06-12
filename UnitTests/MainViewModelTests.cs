using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Xunit;
using WuwaModModifier.Common;
using WuwaModModifier.Data.ViewModels;
using WuwaModModifier.Model;
using WuwaModModifier.ViewModels;

namespace UnitTests
{
    public class MainViewModelTests
    {
        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return (T)field!.GetValue(obj)!;
        }

        private static void SetPrivateField<T>(object obj, string fieldName, T value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field!.SetValue(obj, value);
        }

        private static void InvokePrivateMethod(object obj, string methodName, params object?[]? parameters)
        {
            var paramTypes = parameters?.Select(p => p?.GetType() ?? typeof(object)).ToArray()
                ?? Array.Empty<Type>();
            var method = obj.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, paramTypes, null);
            Assert.NotNull(method);
            method!.Invoke(obj, parameters);
        }

        [Fact]
        public void LoadDirectoryTree_ShouldCreateTreeFromAllMods()
        {
            // Arrange
            var vm = TestHelper.CreateMainViewModel();

            var allMods = new List<WuwaMods>
            {
                new WuwaMods
                {
                    CharacterName = "CharA",
                    Folder = @"C:\Mods\CharA",
                    Mods = new List<WuwaMod>
                    {
                        new WuwaMod { CharacterName = "CharA", Id = "1", ModName = "[1]ModA1", FullPath = @"C:\Mods\CharA\[1]ModA1" },
                        new WuwaMod { CharacterName = "CharA", Id = "2", ModName = "[2]ModA2", FullPath = @"C:\Mods\CharA\[2]ModA2" }
                    }
                },
                new WuwaMods
                {
                    CharacterName = "CharB",
                    Folder = @"C:\Mods\CharB",
                    Mods = new List<WuwaMod>
                    {
                        new WuwaMod { CharacterName = "CharB", Id = "3", ModName = "[3]ModB1", FullPath = @"C:\Mods\CharB\[3]ModB1" }
                    }
                }
            };

            SetPrivateField(vm, "_allMods", allMods);

            // Act
            InvokePrivateMethod(vm, "LoadDirectoryTree");

            // Assert
            Assert.Equal(2, vm.DirectoryItems.Count);

            var charA = vm.DirectoryItems.First(d => d.Name == "CharA");
            Assert.Equal(2, charA.Children.Count);
            Assert.Contains(charA.Children, c => c.Name == "[1]ModA1" && c.Id == "1");
            Assert.Contains(charA.Children, c => c.Name == "[2]ModA2" && c.Id == "2");

            var charB = vm.DirectoryItems.First(d => d.Name == "CharB");
            Assert.Single(charB.Children);
            Assert.Equal("[3]ModB1", charB.Children[0].Name);
        }

        [Fact]
        public void LoadDirectoryTree_ShouldIgnoreWeaponWhenFlagIsTrue()
        {
            // Arrange
            var vm = TestHelper.CreateMainViewModel();
            vm.IgnoreWeapon = true;

            var allMods = new List<WuwaMods>
            {
                new WuwaMods
                {
                    CharacterName = "weapon_sword",
                    Folder = @"C:\Mods\weapon_sword",
                    Mods = new List<WuwaMod>
                    {
                        new WuwaMod { CharacterName = "weapon_sword", Id = "10", ModName = "[10]Sword", FullPath = @"C:\Mods\weapon_sword\[10]Sword" }
                    }
                },
                new WuwaMods
                {
                    CharacterName = "CharC",
                    Folder = @"C:\Mods\CharC",
                    Mods = new List<WuwaMod>
                    {
                        new WuwaMod { CharacterName = "CharC", Id = "11", ModName = "[11]ModC1", FullPath = @"C:\Mods\CharC\[11]ModC1" }
                    }
                }
            };

            SetPrivateField(vm, "_allMods", allMods);

            // Act
            InvokePrivateMethod(vm, "LoadDirectoryTree");

            // Assert
            Assert.Single(vm.DirectoryItems);
            Assert.Equal("CharC", vm.DirectoryItems[0].Name);
        }

        [Fact]
        public void SelectLoadedMods_ShouldSelectAndCheckMatchingItems()
        {
            // Arrange
            var vm = TestHelper.CreateMainViewModel();

            // 构造目录树
            vm.DirectoryItems = new ObservableCollection<DirectoryItemViewModel>
            {
                new DirectoryItemViewModel
                {
                    Name = "CharA",
                    FullPath = @"C:\Mods\CharA",
                    IsDirectory = true,
                    Children =
                    {
                        new DirectoryItemViewModel
                        {
                            Name = "[1]ModA1",
                            Id = "1",
                            FullPath = @"C:\Mods\CharA\[1]ModA1",
                            IsDirectory = false
                        },
                        new DirectoryItemViewModel
                        {
                            Name = "[2]ModA2",
                            Id = "2",
                            FullPath = @"C:\Mods\CharA\[2]ModA2",
                            IsDirectory = false
                        }
                    }
                }
            };
            // 维护 Parent 引用
            foreach (var child in vm.DirectoryItems[0].Children)
            {
                child.Parent = vm.DirectoryItems[0];
            }

            // 构造已安装的 WWMI MOD 列表
            var wwmiMods = new List<WuwaMod>
            {
                new WuwaMod
                {
                    CharacterName = "CharA",
                    Id = "2",
                    ModName = "[2]ModA2",
                    FullPath = @"E:\WWMI\[CharA][2]ModA2"
                }
            };
            SetPrivateField(vm, "_wwmiMods", wwmiMods);

            // Act
            InvokePrivateMethod(vm, "SelectLoadedMods");

            // Assert
            var charA = vm.DirectoryItems[0];
            Assert.True(charA.IsSelected);

            var mod1 = charA.Children[0];
            var mod2 = charA.Children[1];

            Assert.False(mod1.IsSelected);
            Assert.False(mod1.IsChecked);

            Assert.True(mod2.IsSelected);
            Assert.True(mod2.IsChecked);
        }

        [Fact]
        public void ClearSelection_ShouldClearAllIsSelectedFlags()
        {
            // Arrange
            var vm = TestHelper.CreateMainViewModel();

            var root = new DirectoryItemViewModel
            {
                Name = "Root",
                IsDirectory = true,
                IsSelected = true,
                Children =
                {
                    new DirectoryItemViewModel
                    {
                        Name = "Child1",
                        IsDirectory = false,
                        IsSelected = true
                    }
                }
            };
            root.Children[0].Parent = root;

            vm.DirectoryItems = new ObservableCollection<DirectoryItemViewModel> { root };

            // Act
            var method = typeof(MainViewModel).GetMethod("ClearSelection", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(vm, new object[] { vm.DirectoryItems });

            // Assert
            Assert.False(root.IsSelected);
            Assert.False(root.Children[0].IsSelected);
        }

        [Fact]
        public void CollectSelectedItems_ShouldReturnAllCheckedLeafItems()
        {
            // Arrange
            var vm = TestHelper.CreateMainViewModel();

            var root = new DirectoryItemViewModel
            {
                Name = "Root",
                IsDirectory = true,
                Children =
                {
                    new DirectoryItemViewModel { Name = "Child1", IsDirectory = false, IsChecked = true },
                    new DirectoryItemViewModel { Name = "Child2", IsDirectory = false, IsChecked = false }
                }
            };
            root.Children[0].Parent = root;
            root.Children[1].Parent = root;

            vm.DirectoryItems = new ObservableCollection<DirectoryItemViewModel> { root };

            var selected = new List<DirectoryItemViewModel>();

            // Act
            var method = typeof(MainViewModel).GetMethod("CollectSelectedItems", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(vm, new object[] { vm.DirectoryItems, selected });

            // Assert
            Assert.Single(selected);
            Assert.Equal("Child1", selected[0].Name);
        }

        [Fact]
        public void SelectedDirectoryItem_ShouldPopulateConfigAnalysis_ForLeafModItem()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "hash = 12345678\n" +
                    "match_first_index = 0\n" +
                    "match_index_count = 12\n" +
                    "handling = skip\n" +
                    "drawindexed = 12, 0, 0\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.EndsWith("mod.ini", vm.SelectedConfigPath);
                Assert.Single(vm.SelectedToggleItems);
                Assert.Equal(2, vm.SelectedParameterItems.Count);
                Assert.Single(vm.SelectedVisibilityItems);
                Assert.Contains("global persist $curHat = 1", vm.RawConfigEditorText);
                Assert.False(vm.IsRawConfigDirty);
                Assert.Contains(vm.RawConfigHighlights, item => item.Kind == ConfigTextHighlightKind.Toggle);
                Assert.Contains(vm.RawConfigHighlights, item => item.Kind == ConfigTextHighlightKind.Parameter);
                Assert.Contains(vm.RawConfigHighlights, item => item.Kind == ConfigTextHighlightKind.Visibility);
                Assert.Empty(vm.ModificationHistoryItems);
                Assert.Contains("已分析 1 个按键项，2 个参数，1 个模型显示项。", vm.SelectedConfigAnalysisStatus);
                Assert.Equal("Key curHat", vm.SelectedToggleItems[0].SectionName);
                Assert.Equal(4, vm.SelectedToggleItems[0].NavigateLine);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$curHat");
                Assert.Equal(2, Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$curHat")).NavigateLine);
                var internalParameter = Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$object_detected"));
                Assert.Equal(string.Empty, internalParameter.DefaultValue);
                Assert.Equal(string.Empty, internalParameter.BoundKeySectionsText);
                Assert.Equal(string.Empty, internalParameter.KeyBindingsText);
                Assert.Equal("TextureOverrideComponent0", vm.SelectedVisibilityItems[0].SectionName);
                Assert.Equal(15, vm.SelectedVisibilityItems[0].NavigateLine);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SelectedDirectoryItem_ShouldExposeMultipleConfigCandidates_ForMultiConfigMod()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[201]MultiFormMod");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global $form = 1\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "form_a.ini"),
                    "[Constants]\n" +
                    "global $form = 2\n");
                File.WriteAllText(
                    Path.Combine(modDirectory, "form_b.ini"),
                    "[Constants]\n" +
                    "global $form = 3\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[201]MultiFormMod",
                    FullPath = modDirectory,
                    Id = "201",
                    IsDirectory = false
                };

                Assert.EndsWith("mod.ini", vm.SelectedConfigPath);
                Assert.Contains(Path.Combine(modDirectory, "mod.ini"), vm.SelectedConfigCandidatesText);
                Assert.Contains(Path.Combine(modDirectory, "form_a.ini"), vm.SelectedConfigCandidatesText);
                Assert.Contains(Path.Combine(modDirectory, "form_b.ini"), vm.SelectedConfigCandidatesText);
                Assert.Contains("已发现 3 个配置候选", vm.SelectedConfigEditStatus);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SelectedConfigCandidatePath_ShouldSwitchAnalysisToSelectedCandidate()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[202]CandidateSwitch");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var primaryConfigPath = Path.Combine(modDirectory, "mod.ini");
                var alternateConfigPath = Path.Combine(modDirectory, "form_a.ini");
                File.WriteAllText(
                    primaryConfigPath,
                    "[Constants]\n" +
                    "global $form = 0\n");
                File.WriteAllText(
                    alternateConfigPath,
                    "[Constants]\n" +
                    "global $form = 1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[202]CandidateSwitch",
                    FullPath = modDirectory,
                    Id = "202",
                    IsDirectory = false
                };

                Assert.Equal(primaryConfigPath, vm.SelectedConfigPath);
                Assert.Equal(primaryConfigPath, vm.SelectedConfigCandidatePath);

                vm.SelectedConfigCandidatePath = alternateConfigPath;

                Assert.Equal(alternateConfigPath, vm.SelectedConfigPath);
                Assert.Equal(alternateConfigPath, vm.SelectedConfigCandidatePath);
                Assert.Contains("已切换到候选配置", vm.SelectedConfigEditStatus);
                Assert.Contains("global $form = 1", vm.RawConfigEditorText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SelectedDirectoryItem_ShouldResetConfigAnalysis_WhenSelectingCharacterDirectory()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $curHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedToggleItem = Assert.Single(vm.SelectedToggleItems);
                Assert.True(vm.RawConfigNavigateRequestVersion > 0);

                InvokePrivateMethod(vm, "AppendModificationHistory", "按键修改", "Key curHat", "快捷键更新为 CTRL ALT NUMPAD1。");
                Assert.Single(vm.ModificationHistoryItems);

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "Encore",
                    FullPath = tempRoot,
                    IsDirectory = true
                };

                Assert.Equal(string.Empty, vm.SelectedConfigPath);
                Assert.Empty(vm.SelectedToggleItems);
                Assert.Empty(vm.SelectedParameterItems);
                Assert.Empty(vm.SelectedVisibilityItems);
                Assert.Empty(vm.ModificationHistoryItems);
                Assert.Equal(string.Empty, vm.RawConfigEditorText);
                Assert.Empty(vm.RawConfigHighlights);
                Assert.False(vm.IsRawConfigDirty);
                Assert.Equal(0, vm.RawConfigNavigateLine);
                Assert.Equal(0, vm.RawConfigNavigateRequestVersion);
                Assert.Equal("当前选择的是角色目录，请选择具体 MOD。", vm.SelectedConfigAnalysisStatus);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SelectedDirectoryItem_ShouldOnlyEnableVersionSyncForCharacterDirectory()
        {
            var vm = TestHelper.CreateMainViewModel();
            var characterDirectory = new DirectoryItemViewModel
            {
                Name = "Encore",
                FullPath = @"C:\Mods\Encore",
                IsDirectory = true
            };

            vm.SelectedDirectoryItem = new DirectoryItemViewModel
            {
                Name = "[200]FancyDress",
                FullPath = @"C:\Mods\Encore\[200]FancyDress",
                IsDirectory = false,
                Parent = characterDirectory
            };

            Assert.False(vm.CanOpenVersionSync);
            Assert.Equal(string.Empty, vm.SelectedVersionSyncDirectoryPath);

            vm.SelectedDirectoryItem = characterDirectory;

            Assert.True(vm.CanOpenVersionSync);
            Assert.Equal(@"C:\Mods\Encore", vm.SelectedVersionSyncDirectoryPath);
        }

        [Fact]
        public void SelectingAnalysisItems_ShouldUpdateRawConfigNavigateState()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "hash = 12345678\n" +
                    "match_first_index = 0\n" +
                    "match_index_count = 12\n" +
                    "handling = skip\n" +
                    "drawindexed = 12, 0, 0\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.Equal(0, vm.RawConfigNavigateLine);
                Assert.Equal(0, vm.RawConfigNavigateRequestVersion);

                var toggleItem = Assert.Single(vm.SelectedToggleItems);
                vm.SelectedToggleItem = toggleItem;
                var toggleRequestVersion = vm.RawConfigNavigateRequestVersion;
                Assert.Equal(4, vm.RawConfigNavigateLine);
                Assert.True(toggleRequestVersion > 0);

                var parameterItem = Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$curHat"));
                vm.SelectedParameterItem = parameterItem;
                var parameterRequestVersion = vm.RawConfigNavigateRequestVersion;
                Assert.Equal(2, vm.RawConfigNavigateLine);
                Assert.True(parameterRequestVersion > toggleRequestVersion);

                var visibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                vm.SelectedVisibilityItem = visibilityItem;
                Assert.Equal(15, vm.RawConfigNavigateLine);
                Assert.True(vm.RawConfigNavigateRequestVersion > parameterRequestVersion);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void RawConfigEditor_ShouldDisableSemanticCommandsUntilSaved()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $curHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedToggleItem = Assert.Single(vm.SelectedToggleItems);
                vm.ToggleKeyBindingEditorText = "CTRL ALT NUMPAD1";

                Assert.True(vm.ApplyToggleKeyBindingsCommand.CanExecute(null));
                vm.ApplyToggleKeyBindingsCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.True(vm.SaveConfigToModCommand.CanExecute(null));

                vm.RawConfigEditorText = vm.RawConfigEditorText + "\n; raw editor note";

                Assert.True(vm.IsRawConfigDirty);
                Assert.True(vm.SaveRawConfigCommand.CanExecute(null));
                Assert.False(vm.ApplyToggleKeyBindingsCommand.CanExecute(null));
                Assert.True(vm.SaveConfigToModCommand.CanExecute(null));
                Assert.False(vm.SyncModToWwmiCommand.CanExecute(null));
                Assert.Contains("未保存修改", vm.RawConfigEditorStatusText);

                vm.SaveConfigToModCommand.Execute(null);

                Assert.False(vm.IsRawConfigDirty);
                Assert.False(vm.HasPendingConfigChanges);
                Assert.Contains("CTRL ALT NUMPAD1", File.ReadAllText(configPath));
                Assert.Contains("; raw editor note", File.ReadAllText(configPath));
                Assert.Contains(configPath, messages.LastInfoMessage ?? string.Empty);
                Assert.Contains("已保存到", messages.LastInfoMessage ?? string.Empty);
                Assert.Contains("; raw editor note", vm.RawConfigEditorText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SaveConfigToMod_ShouldTreatNestedConfigPathAsSourceTarget()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Camellya", "[596119] camellya_succubus_nsfw");
            var configDirectory = Path.Combine(modDirectory, "Succubus Camellya");
            Directory.CreateDirectory(configDirectory);

            try
            {
                var configPath = Path.Combine(configDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $curHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[596119] camellya_succubus_nsfw",
                    FullPath = modDirectory,
                    Id = "596119",
                    IsDirectory = false
                };

                Assert.Equal(configPath, vm.SelectedConfigPath);
                vm.SelectedToggleItem = Assert.Single(vm.SelectedToggleItems);
                vm.ToggleKeyBindingEditorText = "CTRL ALT NUMPAD1";

                vm.ApplyToggleKeyBindingsCommand.Execute(null);
                vm.SaveConfigToModCommand.Execute(null);

                Assert.False(vm.HasPendingConfigChanges);
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(configPath));
                Assert.Contains(configPath, messages.LastInfoMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyToggleKeyBindingsCommand_ShouldUpdateBuffer_AndSaveToWwmi()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $curHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedToggleItem = Assert.Single(vm.SelectedToggleItems);
                vm.ToggleKeyBindingEditorText = "CTRL ALT NUMPAD1\nSHIFT NUMPAD2";
                var changedProperties = new List<string>();
                vm.PropertyChanged += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.PropertyName))
                    {
                        changedProperties.Add(args.PropertyName);
                    }
                };

                Assert.True(vm.ApplyToggleKeyBindingsCommand.CanExecute(null));

                vm.ApplyToggleKeyBindingsCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Equal("CTRL ALT NUMPAD1 | SHIFT NUMPAD2", Assert.Single(vm.SelectedToggleItems).KeyBindingsText);
                Assert.Contains("已在缓冲区更新", vm.SelectedConfigEditStatus);
                var toggleHistory = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("按键修改", toggleHistory.OperationTypeText);
                Assert.Equal("Key curHat", toggleHistory.TargetText);
                Assert.Contains("CTRL ALT NUMPAD1 | SHIFT NUMPAD2", toggleHistory.SummaryText);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", File.ReadAllText(configPath));

                var wwmiConfigPath = Path.Combine(wwmiRoot, "[Encore][200]FancyDress", "mod.ini");
                Assert.Contains(wwmiConfigPath, vm.SaveToWwmiPreviewPath);
                Assert.Contains("将创建新文件", vm.SaveToWwmiPreviewPath);

                vm.SaveConfigToWwmiCommand.Execute(null);

                Assert.True(File.Exists(wwmiConfigPath));
                Assert.Contains(wwmiConfigPath, vm.SaveToWwmiPreviewPath);
                Assert.Contains("将覆盖已有文件", vm.SaveToWwmiPreviewPath);
                Assert.Contains("key = CTRL ALT NUMPAD1", File.ReadAllText(wwmiConfigPath));
                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains("已导出当前工作内容到", messages.LastInfoMessage ?? string.Empty);
                Assert.Contains(wwmiConfigPath, messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains("将创建新文件", messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains(nameof(MainViewModel.SaveToWwmiPreviewPath), changedProperties);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SaveConfigToWwmiCommand_ShouldExportRawEditorChangesWithoutClearingCurrentSourceState()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedToggleItem = Assert.Single(vm.SelectedToggleItems);
                vm.ToggleKeyBindingEditorText = "CTRL ALT NUMPAD1";
                vm.ApplyToggleKeyBindingsCommand.Execute(null);
                vm.RawConfigEditorText += "\n; raw editor note";

                Assert.True(vm.HasPendingConfigChanges);
                Assert.True(vm.IsRawConfigDirty);
                Assert.True(vm.SaveConfigToWwmiCommand.CanExecute(null));

                vm.SaveConfigToWwmiCommand.Execute(null);

                var wwmiConfigPath = Path.Combine(wwmiRoot, "[Encore][200]FancyDress", "mod.ini");
                Assert.True(File.Exists(wwmiConfigPath));
                Assert.Contains("CTRL ALT NUMPAD1", File.ReadAllText(wwmiConfigPath));
                Assert.Contains("; raw editor note", File.ReadAllText(wwmiConfigPath));
                Assert.True(vm.HasPendingConfigChanges);
                Assert.True(vm.IsRawConfigDirty);
                Assert.Contains("仍有未保存改动", messages.LastInfoMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ToggleConfigSourceCommand_ShouldSwitchBetweenModAndWwmiConfigs()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][200]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            try
            {
                var modConfigPath = Path.Combine(modDirectory, "mod.ini");
                var wwmiConfigPath = Path.Combine(wwmiDirectory, "mod.ini");
                File.WriteAllText(modConfigPath, "[Constants]\nglobal persist $value = 1\n");
                File.WriteAllText(wwmiConfigPath, "[Constants]\nglobal persist $value = 9\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.Equal(modConfigPath, vm.SelectedConfigPath);
                Assert.Equal("Mod配置", vm.CurrentConfigSourceText);
                Assert.True(vm.ToggleConfigSourceCommand.CanExecute(null));

                vm.ToggleConfigSourceCommand.Execute(null);

                Assert.Equal(wwmiConfigPath, vm.SelectedConfigPath);
                Assert.Equal("WWMI配置", vm.CurrentConfigSourceText);
                Assert.Contains("$value = 9", vm.RawConfigEditorText);

                vm.ToggleConfigSourceCommand.Execute(null);

                Assert.Equal(modConfigPath, vm.SelectedConfigPath);
                Assert.Equal("Mod配置", vm.CurrentConfigSourceText);
                Assert.Contains("$value = 1", vm.RawConfigEditorText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ToggleConfigSourceCommand_ShouldKeepMatchingCandidateAcrossSources()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[203]MultiForm");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][203]MultiForm");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            try
            {
                var modMainConfigPath = Path.Combine(modDirectory, "mod.ini");
                var modFormConfigPath = Path.Combine(modDirectory, "form_a.ini");
                var wwmiMainConfigPath = Path.Combine(wwmiDirectory, "mod.ini");
                var wwmiFormConfigPath = Path.Combine(wwmiDirectory, "form_a.ini");

                File.WriteAllText(modMainConfigPath, "[Constants]\nglobal persist $value = 1\n");
                File.WriteAllText(modFormConfigPath, "[Constants]\nglobal persist $value = 2\n");
                File.WriteAllText(wwmiMainConfigPath, "[Constants]\nglobal persist $value = 9\n");
                File.WriteAllText(wwmiFormConfigPath, "[Constants]\nglobal persist $value = 8\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[203]MultiForm",
                    FullPath = modDirectory,
                    Id = "203",
                    IsDirectory = false
                };

                vm.SelectedConfigCandidatePath = modFormConfigPath;

                Assert.Equal(modFormConfigPath, vm.SelectedConfigPath);
                Assert.Equal(modFormConfigPath, vm.SelectedConfigCandidatePath);

                vm.ToggleConfigSourceCommand.Execute(null);

                Assert.Equal("WWMI配置", vm.CurrentConfigSourceText);
                Assert.Equal(wwmiFormConfigPath, vm.SelectedConfigPath);
                Assert.Equal(wwmiFormConfigPath, vm.SelectedConfigCandidatePath);
                Assert.Contains("$value = 8", vm.RawConfigEditorText);

                vm.ToggleConfigSourceCommand.Execute(null);

                Assert.Equal("Mod配置", vm.CurrentConfigSourceText);
                Assert.Equal(modFormConfigPath, vm.SelectedConfigPath);
                Assert.Equal(modFormConfigPath, vm.SelectedConfigCandidatePath);
                Assert.Contains("$value = 2", vm.RawConfigEditorText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ToggleConfigSourceCommand_ShouldBeDisabledWhenOnlyModConfigExists()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var modConfigPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(modConfigPath, "[Constants]\nglobal persist $value = 1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.Equal(modConfigPath, vm.SelectedConfigPath);
                Assert.False(vm.ToggleConfigSourceCommand.CanExecute(null));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ToggleConfigSourceCommand_ShouldBlockWhenCurrentSourceHasUnsavedChanges()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][200]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            try
            {
                var modConfigPath = Path.Combine(modDirectory, "mod.ini");
                var wwmiConfigPath = Path.Combine(wwmiDirectory, "mod.ini");
                File.WriteAllText(modConfigPath, "[Constants]\nglobal persist $value = 1\n");
                File.WriteAllText(wwmiConfigPath, "[Constants]\nglobal persist $value = 9\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.RawConfigEditorText += "\n; unsaved";

                Assert.True(vm.ToggleConfigSourceCommand.CanExecute(null));

                vm.ToggleConfigSourceCommand.Execute(null);

                Assert.Equal(modConfigPath, vm.SelectedConfigPath);
                Assert.Equal("Mod配置", vm.CurrentConfigSourceText);
                Assert.Contains("未保存改动", messages.LastInfoMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SyncModToWwmiCommand_ShouldCommitCurrentSourceRawChangesBeforeSync()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            var wwmiDirectory = Path.Combine(wwmiRoot, "[Encore][200]FancyDress");
            Directory.CreateDirectory(modDirectory);
            Directory.CreateDirectory(wwmiDirectory);

            try
            {
                var modConfigPath = Path.Combine(modDirectory, "mod.ini");
                var wwmiConfigPath = Path.Combine(wwmiDirectory, "mod.ini");
                File.WriteAllText(modConfigPath, "[Constants]\nglobal persist $value = 1\n");
                File.WriteAllText(wwmiConfigPath, "[Constants]\nglobal persist $value = 1\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.RawConfigEditorText += "\n; sync raw note";

                Assert.True(vm.IsRawConfigDirty);
                Assert.True(vm.SyncModToWwmiCommand.CanExecute(null));

                vm.SyncModToWwmiCommand.Execute(null);

                Assert.False(vm.IsRawConfigDirty);
                Assert.False(vm.HasPendingConfigChanges);
                Assert.Contains("; sync raw note", File.ReadAllText(modConfigPath));
                Assert.Contains("; sync raw note", File.ReadAllText(wwmiConfigPath));
                Assert.Contains("已同步", messages.LastInfoMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void RenameParameterCommand_ShouldUpdateAnalysis_AndSaveToMod()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $curHat = 1\n\n" +
                    "[Key curHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$curHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $curHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedParameterItem = Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$curHat"));
                vm.ParameterRenameText = "$curCap";

                Assert.True(vm.RenameParameterCommand.CanExecute(null));

                vm.RenameParameterCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$curCap");
                Assert.DoesNotContain(vm.SelectedParameterItems, item => item.Name == "$curHat");
                Assert.Contains("$curCap", Assert.Single(vm.SelectedToggleItems).TargetsText);
                var parameterHistory = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("参数重命名", parameterHistory.OperationTypeText);
                Assert.Equal("$curHat", parameterHistory.TargetText);
                Assert.Contains("$curCap", parameterHistory.SummaryText);

                vm.SaveConfigToModCommand.Execute(null);

                Assert.False(vm.HasPendingConfigChanges);
                Assert.Contains(configPath, vm.SaveToModPreviewPath);
                Assert.Contains("将覆盖已有文件", vm.SaveToModPreviewPath);
                Assert.Contains("global persist $curCap = 1", File.ReadAllText(configPath));
                Assert.Contains("已保存到", messages.LastInfoMessage ?? string.Empty);
                Assert.Contains(configPath, messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains("将覆盖已有文件", messages.LastConfirmationMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityBindingCommand_ShouldBindToExistingParameter()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[KeyHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NUMPAD1\n" +
                    "type = cycle\n" +
                    "$showHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);

                Assert.True(vm.CanBindSelectedVisibilityItem);
                Assert.True(vm.UseExistingVisibilityParameterBinding);
                Assert.Single(vm.VisibilityBindingParameterCandidates);
                Assert.Single(vm.VisibilityBindingToggleCandidates);
                Assert.Equal("$showHat", Assert.Single(vm.VisibilityBindingParameterCandidates).Name);
                Assert.True(vm.ApplyVisibilityBindingCommand.CanExecute(null));

                vm.ApplyVisibilityBindingCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                var updatedVisibility = Assert.Single(vm.SelectedVisibilityItems);
                Assert.Equal("$showHat", updatedVisibility.ControllingParametersText);
                Assert.Equal("NUMPAD1", updatedVisibility.ControllingKeyBindingsText);
                Assert.True(updatedVisibility.CanToggleSafely);
                Assert.False(updatedVisibility.CanBindSafely);
                Assert.Contains("if $showHat == 1", vm.RawConfigEditorText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型绑定", history.OperationTypeText);
                Assert.Contains("$showHat", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void SelectingUnboundVisibilityItem_ShouldPopulateNewBindingDefaults_FromFirstUnusedTemplateKey()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            var templatePath = Path.Combine(tempRoot, "toggle.ini");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[KeyHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$showHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n" +
                    "global persist $key_2 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n\n" +
                    "[Key key_2]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                    "type = cycle\n" +
                    "$key_2 = 0,1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.StandardToggleTemplatePath = templatePath;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                vm.UseNewVisibilityBinding = true;

                Assert.True(vm.UseNewVisibilityBinding);
                Assert.Equal("$HAT", vm.VisibilityBindingNewParameterName);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD2", vm.VisibilityBindingNewKeyBindingsText);
                Assert.Equal(new[] { "NO_CTRL NO_ALT NO_SHIFT NUMPAD2" }, vm.VisibilityBindingAvailableKeyOptions);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityBindingCommand_ShouldReuseSelectedToggleParameter()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[KeyHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NUMPAD1\n" +
                    "type = cycle\n" +
                    "$showHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                vm.UseExistingVisibilityToggleBinding = true;

                Assert.True(vm.UseExistingVisibilityToggleBinding);
                Assert.True(vm.ApplyVisibilityBindingCommand.CanExecute(null));

                vm.ApplyVisibilityBindingCommand.Execute(null);

                var updatedVisibility = Assert.Single(vm.SelectedVisibilityItems);
                Assert.Equal("$showHat", updatedVisibility.ControllingParametersText);
                Assert.Equal("NUMPAD1", updatedVisibility.ControllingKeyBindingsText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型绑定", history.OperationTypeText);
                Assert.Contains("KeyHat", history.SummaryText);
                Assert.Contains("$showHat", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityBindingCommand_ShouldCreateNewParameterAndKey()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            var templatePath = Path.Combine(tempRoot, "toggle.ini");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n" +
                    "global persist $key_2 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n\n" +
                    "[Key key_2]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                    "type = cycle\n" +
                    "$key_2 = 0,1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.StandardToggleTemplatePath = templatePath;
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);

                Assert.True(vm.CanBindSelectedVisibilityItem);
                Assert.Empty(vm.VisibilityBindingParameterCandidates);
                Assert.Empty(vm.VisibilityBindingToggleCandidates);
                Assert.True(vm.UseNewVisibilityBinding);
                Assert.Equal("$HAT", vm.VisibilityBindingNewParameterName);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.VisibilityBindingNewKeyBindingsText);
                Assert.Equal(2, vm.VisibilityBindingAvailableKeyOptions.Count);

                Assert.True(vm.ApplyVisibilityBindingCommand.CanExecute(null));

                vm.ApplyVisibilityBindingCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$HAT");
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key HAT");
                var updatedVisibility = Assert.Single(vm.SelectedVisibilityItems);
                Assert.Equal("$HAT", updatedVisibility.ControllingParametersText);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", updatedVisibility.ControllingKeyBindingsText);
                Assert.True(updatedVisibility.CanToggleSafely);
                Assert.Contains("global persist $HAT = 1", vm.RawConfigEditorText);
                Assert.Contains("[Key HAT]", vm.RawConfigEditorText);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.RawConfigEditorText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型绑定", history.OperationTypeText);
                Assert.Contains("$HAT", history.SummaryText);
                Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityBindingCommand_ShouldCreateNewParameterAndKey_ForAlreadyBoundItem()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            var templatePath = Path.Combine(tempRoot, "toggle.ini");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[KeyHat]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$showHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $showHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n" +
                    "global persist $key_2 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n\n" +
                    "[Key key_2]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                    "type = cycle\n" +
                    "$key_2 = 0,1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.StandardToggleTemplatePath = templatePath;
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);

                Assert.False(vm.CanBindSelectedVisibilityItem);
                Assert.True(vm.CanCreateNewVisibilityBinding);
                Assert.Equal("$HAT", vm.VisibilityBindingNewParameterName);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD2", vm.VisibilityBindingNewKeyBindingsText);

                vm.UseNewVisibilityBinding = true;

                Assert.True(vm.ApplyVisibilityBindingCommand.CanExecute(null));

                vm.ApplyVisibilityBindingCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$showHat");
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$HAT");
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key HAT");
                var updatedVisibility = Assert.Single(vm.SelectedVisibilityItems);
                Assert.Equal("$HAT", updatedVisibility.ControllingParametersText);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD2", updatedVisibility.ControllingKeyBindingsText);
                Assert.Contains("if $HAT == 1", vm.RawConfigEditorText);
                Assert.Contains("[Key HAT]", vm.RawConfigEditorText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型绑定", history.OperationTypeText);
                Assert.Contains("$HAT", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void CreateToggleCommand_ShouldBindToExistingParameter()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            var templatePath = Path.Combine(tempRoot, "toggle.ini");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.StandardToggleTemplatePath = templatePath;
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.True(vm.UseExistingToggleCreationParameter);
                Assert.Equal("$showHat", Assert.Single(vm.ToggleCreationParameterCandidates).Name);
                Assert.Equal("$showHat", vm.SelectedToggleCreationParameter?.Name);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.ToggleCreationKeyBindingsText);
                Assert.True(vm.CreateToggleCommand.CanExecute(null));

                vm.CreateToggleCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key showHat");
                Assert.Contains("[Key showHat]", vm.RawConfigEditorText);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.RawConfigEditorText);
                var updatedParameter = Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$showHat"));
                Assert.Contains("Key showHat", updatedParameter.BoundKeySectionsText);
                Assert.Contains("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", updatedParameter.KeyBindingsText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("新增按键", history.OperationTypeText);
                Assert.Equal("$showHat", history.TargetText);
                Assert.Contains("绑定到现有参数", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void CreateToggleCommand_ShouldCreateNewParameterAndBindIt()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            var templatePath = Path.Combine(tempRoot, "toggle.ini");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.StandardToggleTemplatePath = templatePath;
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.True(vm.UseNewToggleCreationParameter);
                Assert.Empty(vm.ToggleCreationParameterCandidates);
                Assert.Equal("NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.ToggleCreationKeyBindingsText);

                vm.ToggleCreationNewParameterName = "showHat";

                Assert.True(vm.CreateToggleCommand.CanExecute(null));

                vm.CreateToggleCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$showHat");
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key showHat");
                Assert.Contains("global persist $showHat = 1", vm.RawConfigEditorText);
                Assert.Contains("[Key showHat]", vm.RawConfigEditorText);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", vm.RawConfigEditorText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("新增按键", history.OperationTypeText);
                Assert.Contains("新建参数 $showHat", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void CreateParameterCommand_ShouldAddParameterAndExposeItForToggleCreation()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modDirectory = Path.Combine(tempRoot, "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "mod.ini"),
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.HAT\n" +
                    "drawindexed = 12, 0, 0\n");

                var vm = TestHelper.CreateMainViewModel(new TestMessageService());
                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.ParameterCreationName = "showHat";

                Assert.True(vm.CreateParameterCommand.CanExecute(null));

                vm.CreateParameterCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$showHat");
                Assert.Contains(vm.ToggleCreationParameterCandidates, item => item.Name == "$showHat");
                Assert.Equal("$showHat", vm.SelectedParameterItem?.Name);
                Assert.Contains("global persist $showHat = 1", vm.RawConfigEditorText);
                var history = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("新增参数", history.OperationTypeText);
                Assert.Equal("$showHat", history.TargetText);
                Assert.Contains("默认值为 1", history.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityChangeCommand_ShouldSaveToMod_AndSyncToWwmi()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $showHat = 1\n\n" +
                    "[KeyHat]\n" +
                    "key = NUMPAD1\n" +
                    "type = cycle\n" +
                    "$showHat = 0,1\n\n" +
                    "[TextureOverrideComponent0]\n" +
                    "if $showHat == 1\n" +
                    "    ; Draw Component 0.HAT\n" +
                    "    drawindexed = 12, 0, 0\n" +
                    "endif\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                vm.VisibilityTargetIsVisible = false;
                var changedProperties = new List<string>();
                vm.PropertyChanged += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.PropertyName))
                    {
                        changedProperties.Add(args.PropertyName);
                    }
                };

                Assert.True(vm.ApplyVisibilityChangeCommand.CanExecute(null));

                var visibilityLabel = vm.SelectedVisibilityItem.DrawLabelsText;
                vm.ApplyVisibilityChangeCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Equal("0", Assert.Single(vm.SelectedParameterItems.Where(item => item.Name == "$showHat")).DefaultValue);
                var visibilityHistory = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型显示修改", visibilityHistory.OperationTypeText);
                Assert.Equal(visibilityLabel, visibilityHistory.TargetText);
                Assert.Contains("隐藏", visibilityHistory.SummaryText);
                Assert.False(vm.SyncModToWwmiCommand.CanExecute(null));

                vm.SaveConfigToModCommand.Execute(null);

                Assert.False(vm.HasPendingConfigChanges);
                Assert.Contains("global persist $showHat = 0", File.ReadAllText(configPath));
                Assert.False(vm.SyncModToWwmiCommand.CanExecute(null));
                Assert.False(vm.SyncWwmiToModCommand.CanExecute(null));
                var wwmiConfigPath = Path.Combine(wwmiRoot, "[Encore][200]FancyDress", "mod.ini");
                Assert.Contains(wwmiConfigPath, vm.SaveToWwmiPreviewPath);
                Assert.Contains("将创建新文件", vm.SaveToWwmiPreviewPath);
                Assert.Contains(wwmiConfigPath, vm.SyncModToWwmiPreviewText);
                Assert.Contains("将创建新文件", vm.SyncModToWwmiPreviewText);

                Directory.CreateDirectory(Path.GetDirectoryName(wwmiConfigPath)!);
                File.WriteAllText(wwmiConfigPath, "global persist $showHat = 1\n");

                Assert.True(vm.SyncModToWwmiCommand.CanExecute(null));
                Assert.True(vm.SyncWwmiToModCommand.CanExecute(null));

                vm.SyncModToWwmiCommand.Execute(null);

                Assert.True(File.Exists(wwmiConfigPath));
                Assert.Contains("global persist $showHat = 0", File.ReadAllText(wwmiConfigPath));
                Assert.Contains("已同步", messages.LastInfoMessage ?? string.Empty);
                Assert.Contains(wwmiConfigPath, messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains("将覆盖已有文件", messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains(wwmiConfigPath, vm.SyncModToWwmiPreviewText);
                Assert.Contains("将覆盖已有文件", vm.SyncModToWwmiPreviewText);
                Assert.Contains(nameof(MainViewModel.SyncModToWwmiPreviewText), changedProperties);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityChangeCommand_ShouldAllowDirectHideForUnboundVisibilityItem()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var modDirectory = Path.Combine(modRoot, "Rover Female", "[636196]rover female gold 32lodfix");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.GOLD\n" +
                    "drawindexed = 12, 0, 0\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[636196]rover female gold 32lodfix",
                    FullPath = modDirectory,
                    Id = "636196",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                Assert.False(vm.SelectedVisibilityItem.CanToggleSafely);
                Assert.True(vm.SelectedVisibilityItem.CanBindSafely);
                Assert.False(vm.ApplyVisibilityChangeCommand.CanExecute(null));

                vm.VisibilityTargetIsVisible = false;

                Assert.True(vm.ApplyVisibilityChangeCommand.CanExecute(null));

                vm.ApplyVisibilityChangeCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains("if 0", vm.RawConfigEditorText);
                Assert.Contains("drawindexed = 12, 0, 0", vm.RawConfigEditorText);
                var visibilityHistory = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("模型显示修改", visibilityHistory.OperationTypeText);
                Assert.Contains("隐藏", visibilityHistory.SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyVisibilityChangeCommand_ShouldAllowRestoringDirectlyHiddenUnboundVisibilityItem()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var modDirectory = Path.Combine(modRoot, "Rover Female", "[636196]rover female gold 32lodfix");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                File.WriteAllText(
                    configPath,
                    "[TextureOverrideComponent0]\n" +
                    "; Draw Component 0.GOLD\n" +
                    "drawindexed = 12, 0, 0\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[636196]rover female gold 32lodfix",
                    FullPath = modDirectory,
                    Id = "636196",
                    IsDirectory = false
                };

                vm.SelectedVisibilityItem = Assert.Single(vm.SelectedVisibilityItems);
                vm.VisibilityTargetIsVisible = false;
                vm.ApplyVisibilityChangeCommand.Execute(null);

                vm.VisibilityTargetIsVisible = true;

                Assert.True(vm.ApplyVisibilityChangeCommand.CanExecute(null));

                vm.ApplyVisibilityChangeCommand.Execute(null);

                Assert.DoesNotContain("if 0", vm.RawConfigEditorText);
                Assert.Contains("drawindexed = 12, 0, 0", vm.RawConfigEditorText);
                Assert.Contains("设置为显示", vm.ModificationHistoryItems.Last().SummaryText);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public void ApplyStandardizationCommand_ShouldUpdateBuffer_AndSaveToMod()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"WuwaModModifierTests_{Path.GetRandomFileName()}");
            var modRoot = Path.Combine(tempRoot, "Mods");
            var wwmiRoot = Path.Combine(tempRoot, "WWMI");
            var modDirectory = Path.Combine(modRoot, "Encore", "[200]FancyDress");
            Directory.CreateDirectory(modDirectory);

            try
            {
                var configPath = Path.Combine(modDirectory, "mod.ini");
                var templatePath = Path.Combine(tempRoot, "toggle.ini");
                File.WriteAllText(
                    configPath,
                    "[Constants]\n" +
                    "global persist $hat = 1\n" +
                    "global persist $bra = 1\n\n" +
                    "[Key Hat]\n" +
                    "condition = $object_detected\n" +
                    "key = F1\n" +
                    "type = cycle\n" +
                    "$hat = 0,1\n\n" +
                    "[Key Bra]\n" +
                    "condition = $object_detected\n" +
                    "key = F2\n" +
                    "type = cycle\n" +
                    "$bra = 0,1\n");
                File.WriteAllText(
                    templatePath,
                    "[Constants]\n" +
                    "global persist $key_1 = 1\n" +
                    "global persist $key_2 = 1\n\n" +
                    "[Key key_1]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1\n" +
                    "type = cycle\n" +
                    "$key_1 = 0,1\n\n" +
                    "[Key key_2]\n" +
                    "condition = $object_detected\n" +
                    "key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2\n" +
                    "type = cycle\n" +
                    "$key_2 = 0,1\n");

                var messages = new TestMessageService();
                var vm = TestHelper.CreateMainViewModel(messages);
                vm.ModFolderPath = modRoot;
                vm.WwmiFolderPath = wwmiRoot;
                vm.StandardToggleTemplatePath = templatePath;

                vm.SelectedDirectoryItem = new DirectoryItemViewModel
                {
                    Name = "[200]FancyDress",
                    FullPath = modDirectory,
                    Id = "200",
                    IsDirectory = false
                };

                Assert.True(vm.ApplyStandardizationCommand.CanExecute(null));

                vm.ApplyStandardizationCommand.Execute(null);

                Assert.True(vm.HasPendingConfigChanges);
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key Hat");
                Assert.Contains(vm.SelectedToggleItems, item => item.SectionName == "Key Bra");
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$hat");
                Assert.Contains(vm.SelectedParameterItems, item => item.Name == "$bra");
                Assert.Contains("部分 2", vm.SelectedConfigEditStatus);
                Assert.Equal(2, vm.LatestStandardizationItems.Count);
                Assert.Contains(vm.LatestStandardizationItems, item => item.StatusText == "部分标准化");
                var standardizationHistory = Assert.Single(vm.ModificationHistoryItems);
                Assert.Equal("批量标准化", standardizationHistory.OperationTypeText);
                Assert.Equal("toggle.ini", standardizationHistory.TargetText);
                Assert.Contains("部分 2", standardizationHistory.SummaryText);
                Assert.Contains(configPath, vm.SaveToModPreviewPath);
                Assert.Contains("将覆盖已有文件", vm.SaveToModPreviewPath);

                vm.SaveConfigToModCommand.Execute(null);

                Assert.False(vm.HasPendingConfigChanges);
                var savedContent = File.ReadAllText(configPath);
                Assert.Contains("[Key Hat]", savedContent);
                Assert.Contains("[Key Bra]", savedContent);
                Assert.Contains("$hat = 0,1", savedContent);
                Assert.Contains("$bra = 0,1", savedContent);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD1", savedContent);
                Assert.Contains("key = NO_CTRL NO_ALT NO_SHIFT NUMPAD2", savedContent);
                Assert.Contains("已保存到", messages.LastInfoMessage ?? string.Empty);
                Assert.Contains(configPath, messages.LastConfirmationMessage ?? string.Empty);
                Assert.Contains("将覆盖已有文件", messages.LastConfirmationMessage ?? string.Empty);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private sealed class TestMessageService : IMessageService
        {
            public string? LastInfoMessage { get; private set; }
            public string? LastErrorMessage { get; private set; }
            public string? LastConfirmationMessage { get; private set; }
            public bool ConfirmationResult { get; set; } = true;

            public void ShowInfo(string message, string? caption = null)
            {
                LastInfoMessage = message;
            }

            public void ShowError(string message, string? caption = null)
            {
                LastErrorMessage = message;
            }

            public bool Confirm(string message, string? caption = null)
            {
                LastConfirmationMessage = message;
                return ConfirmationResult;
            }
        }
    }
}


