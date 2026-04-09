using System.Collections.ObjectModel;
using System.Reflection;
using Xunit;
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
            var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(obj, parameters);
        }

        [Fact]
        public void LoadDirectoryTree_ShouldCreateTreeFromAllMods()
        {
            // Arrange
            var vm = new MainViewModel();

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
            var vm = new MainViewModel
            {
                IgnoreWeapon = true
            };

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
            var vm = new MainViewModel();

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
            var vm = new MainViewModel();

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
            var vm = new MainViewModel();

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
    }
}


