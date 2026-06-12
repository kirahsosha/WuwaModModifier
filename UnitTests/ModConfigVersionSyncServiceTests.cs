using System;
using System.IO;
using System.Linq;
using WuwaModModifier.Common;
using WuwaModModifier.Model;
using Xunit;

namespace UnitTests
{
    public class ModConfigVersionSyncServiceTests
    {
        [Fact]
        public void DiscoverModCandidates_ShouldResolvePrimaryConfigAndNormalizedKey()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var modDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();

                var candidates = service.DiscoverModCandidates(modRoot);

                var candidate = Assert.Single(candidates);
                Assert.Equal("13 Rover", candidate.CharacterName);
                Assert.Equal("589362", candidate.Id);
                Assert.Equal("rover_bikini_warrior_a9e13", candidate.ModName);
                Assert.Equal("rover_bikini_warrior", candidate.NormalizedNameKey);
                Assert.EndsWith("mod.ini", candidate.ConfigPath, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void DiscoverModCandidates_ShouldSupportCharacterDirectoryInput()
        {
            var tempRoot = CreateTempRoot();
            var characterDirectory = Path.Combine(tempRoot, "Mods", "13 Rover");
            var modDirectory = Path.Combine(characterDirectory, "[589362] rover_bikini_warrior_a9e13");
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();

                var candidates = service.DiscoverModCandidates(characterDirectory);

                var candidate = Assert.Single(candidates);
                Assert.Equal(modDirectory, candidate.FullPath);
                Assert.Equal("13 Rover", candidate.CharacterName);
                Assert.Equal("589362", candidate.Id);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void DiscoverModCandidates_ShouldSupportSingleModDirectoryInput()
        {
            var tempRoot = CreateTempRoot();
            var modDirectory = Path.Combine(tempRoot, "Mods", "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();

                var candidates = service.DiscoverModCandidates(modDirectory);

                var candidate = Assert.Single(candidates);
                Assert.Equal(modDirectory, candidate.FullPath);
                Assert.Equal("13 Rover", candidate.CharacterName);
                Assert.Equal("589362", candidate.Id);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void DiscoverModCandidates_ShouldReturnMultipleConfigCandidatesForSingleModDirectory()
        {
            var tempRoot = CreateTempRoot();
            var modDirectory = Path.Combine(tempRoot, "Mods", "13 Rover", "[589362] rover_multiform_a9e13");
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, "mod.ini"), "[Constants]\n");
            File.WriteAllText(Path.Combine(modDirectory, "form_a.ini"), "[Constants]\n");
            File.WriteAllText(Path.Combine(modDirectory, "form_b.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();

                var candidates = service.DiscoverModCandidates(modDirectory);

                Assert.Equal(3, candidates.Count);
                Assert.All(candidates, candidate => Assert.Equal(modDirectory, candidate.FullPath));
                Assert.Contains(candidates, candidate => candidate.ConfigRelativePath.Equals("mod.ini", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(candidates, candidate => candidate.ConfigRelativePath.Equals("form_a.ini", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(candidates, candidate => candidate.ConfigRelativePath.Equals("form_b.ini", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldPairSingleOldAndNewCandidateByNormalizedKey()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);
            File.WriteAllText(Path.Combine(oldDirectory, "mod.ini"), "[Constants]\n");
            File.WriteAllText(Path.Combine(newDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));

                var jobs = service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate });

                var job = Assert.Single(jobs);
                Assert.Equal(VersionSyncJobKind.DirectUpdate, job.JobKind);
                Assert.Equal(oldDirectory, job.OldCandidate.FullPath);
                Assert.Equal(newDirectory, job.NewCandidate.FullPath);
                Assert.Equal(newDirectory, job.OutputDirectoryPath);
                Assert.Equal(Path.Combine(newDirectory, "mod.ini"), job.OutputConfigPath);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldPairMultipleOldAndNewCandidatesWithinSameId()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");

            var oldDirectories = new[]
            {
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_7d286"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_thicc_34030"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_sfw_normal_c5e2e"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_sfw_thicc_ec749")
            };
            var newDirectories = new[]
            {
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_69273"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_thicc_861a8"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_sfw_normal_6a603"),
                Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_sfw_thicc_9755a")
            };

            foreach (var directory in oldDirectories.Concat(newDirectories))
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mod.ini"), "[Constants]\n");
            }

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidates = candidates.Where(candidate => oldDirectories.Contains(candidate.FullPath)).ToArray();
                var newCandidates = candidates.Where(candidate => newDirectories.Contains(candidate.FullPath)).ToArray();

                var jobs = service.CreatePairingJobs(oldCandidates, newCandidates);

                Assert.Equal(4, jobs.Count);
                Assert.All(jobs, job => Assert.Equal(VersionSyncJobKind.DirectUpdate, job.JobKind));
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldDirectories[0] && job.NewCandidate.FullPath == newDirectories[0]);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldDirectories[1] && job.NewCandidate.FullPath == newDirectories[1]);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldDirectories[2] && job.NewCandidate.FullPath == newDirectories[2]);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldDirectories[3] && job.NewCandidate.FullPath == newDirectories[3]);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldExpandManyToOneIntoClonedOutputs()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");

            var oldDirectoryA = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_072a4_v1");
            var oldDirectoryB = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_072a4_v2");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_58df2");

            foreach (var directory in new[] { oldDirectoryA, oldDirectoryB, newDirectory })
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mod.ini"), "[Constants]\n");
            }

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidates = candidates.Where(candidate => candidate.FullPath == oldDirectoryA || candidate.FullPath == oldDirectoryB).ToArray();
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));

                var jobs = service.CreatePairingJobs(oldCandidates, new[] { newCandidate });

                Assert.Equal(2, jobs.Count);
                Assert.All(jobs, job => Assert.Equal(VersionSyncJobKind.CloneFromTemplate, job.JobKind));
                Assert.All(jobs, job => Assert.Equal(newDirectory, job.NewCandidate.FullPath));
                Assert.All(jobs, job => Assert.StartsWith(newDirectory + "__sync_", job.OutputDirectoryPath, StringComparison.OrdinalIgnoreCase));
                Assert.NotEqual(jobs[0].OutputDirectoryPath, jobs[1].OutputDirectoryPath);
                Assert.All(jobs, job => Assert.Equal(Path.Combine(job.OutputDirectoryPath, "mod.ini"), job.OutputConfigPath));
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldFallbackToSimilarNamesWithinSameCharacterAndId()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_release_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);
            File.WriteAllText(Path.Combine(oldDirectory, "mod.ini"), "[Constants]\n");
            File.WriteAllText(Path.Combine(newDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));

                var jobs = service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate });

                var job = Assert.Single(jobs);
                Assert.Equal(oldDirectory, job.OldCandidate.FullPath);
                Assert.Equal(newDirectory, job.NewCandidate.FullPath);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldSkipSelfPairAndPairDifferentFoldersWithinSingleDirectorySet()
        {
            var tempRoot = CreateTempRoot();
            var characterDirectory = Path.Combine(tempRoot, "Mods", "13 Rover");
            var oldDirectory = Path.Combine(characterDirectory, "[589362] rover_bikini_warrior_release_a9e13");
            var newDirectory = Path.Combine(characterDirectory, "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);
            File.WriteAllText(Path.Combine(oldDirectory, "mod.ini"), "[Constants]\n");
            File.WriteAllText(Path.Combine(newDirectory, "mod.ini"), "[Constants]\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(characterDirectory);

                var jobs = service.CreatePairingJobs(candidates, candidates);

                var job = Assert.Single(jobs);
                Assert.NotEqual(job.OldCandidate.FullPath, job.NewCandidate.FullPath);
                Assert.Contains(job.OldCandidate.FullPath, new[] { oldDirectory, newDirectory });
                Assert.Contains(job.NewCandidate.FullPath, new[] { oldDirectory, newDirectory });
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void CreatePairingJobs_ShouldPreferExpectedDirectionAndAvoidCrossPairingWithinSingleDirectorySet()
        {
            var tempRoot = CreateTempRoot();
            var characterDirectory = Path.Combine(tempRoot, "Mods", "13 Rover");

            var oldNormal = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_nsfw_normal_7d286");
            var oldThicc = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_nsfw_thicc_34030");
            var oldSfwNormal = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_sfw_normal_c5e2e");
            var oldSfwThicc = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_sfw_thicc_ec749");
            var newNormal = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_nsfw_normal_69273");
            var newThicc = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_nsfw_thicc_861a8");
            var newSfwNormal = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_sfw_normal_6a603");
            var newSfwThicc = Path.Combine(characterDirectory, "[636546] rover_astral_modulator_sfw_thicc_9755a");

            foreach (var directory in new[]
                     {
                         oldNormal,
                         oldThicc,
                         oldSfwNormal,
                         oldSfwThicc,
                         newNormal,
                         newThicc,
                         newSfwNormal,
                         newSfwThicc
                     })
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "mod.ini"), "[Constants]\n");
            }

            SetCandidateTimestamp(oldNormal, new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(oldThicc, new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(oldSfwNormal, new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(oldSfwThicc, new DateTime(2026, 1, 1, 4, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newNormal, new DateTime(2026, 1, 2, 1, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newThicc, new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newSfwNormal, new DateTime(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newSfwThicc, new DateTime(2026, 1, 2, 4, 0, 0, DateTimeKind.Utc));

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(characterDirectory);

                var jobs = service.CreatePairingJobs(candidates, candidates);

                Assert.Equal(4, jobs.Count);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldNormal && job.NewCandidate.FullPath == newNormal);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldThicc && job.NewCandidate.FullPath == newThicc);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldSfwNormal && job.NewCandidate.FullPath == newSfwNormal);
                Assert.Contains(jobs, job => job.OldCandidate.FullPath == oldSfwThicc && job.NewCandidate.FullPath == newSfwThicc);

                Assert.DoesNotContain(jobs, job => job.OldCandidate.FullPath == newNormal && job.NewCandidate.FullPath == oldNormal);
                Assert.DoesNotContain(jobs, job => job.OldCandidate.FullPath == newSfwNormal && job.NewCandidate.FullPath == oldSfwNormal);
                Assert.DoesNotContain(jobs, job => job.OldCandidate.FullPath == oldNormal && job.NewCandidate.FullPath == oldThicc);
                Assert.DoesNotContain(jobs, job => job.OldCandidate.FullPath == oldSfwNormal && job.NewCandidate.FullPath == newSfwThicc);
                Assert.DoesNotContain(jobs, job => job.OldCandidate.FullPath == newThicc && job.NewCandidate.FullPath == newSfwThicc);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldSucceedForSingleDirectoryHashOnlyPairing()
        {
            var tempRoot = CreateTempRoot();
            var characterDirectory = Path.Combine(tempRoot, "Mods", "13 Rover");
            var oldDirectory = Path.Combine(characterDirectory, "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(characterDirectory, "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 0\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F9\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");

            SetCandidateTimestamp(oldDirectory, new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newDirectory, new DateTime(2026, 1, 2, 1, 0, 0, DateTimeKind.Utc));

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(characterDirectory);

                var job = Assert.Single(service.CreatePairingJobs(candidates, candidates));
                var comparison = service.BuildComparison(job);

                Assert.Equal(oldDirectory, job.OldCandidate.FullPath);
                Assert.Equal(newDirectory, job.NewCandidate.FullPath);
                Assert.Contains("key = F1", comparison.ResultBuffer.Content);
                Assert.Contains("global persist $showHat = 0", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldSucceedForSingleDirectoryManyToOnePairings()
        {
            var tempRoot = CreateTempRoot();
            var characterDirectory = Path.Combine(tempRoot, "Mods", "13 Rover");
            var oldDirectoryA = Path.Combine(characterDirectory, "[637753] rover_belly_dancer_nsfw_072a4_v1");
            var oldDirectoryB = Path.Combine(characterDirectory, "[637753] rover_belly_dancer_nsfw_072a4_v2");
            var newDirectory = Path.Combine(characterDirectory, "[637753] rover_belly_dancer_nsfw_58df2");

            foreach (var directory in new[] { oldDirectoryA, oldDirectoryB, newDirectory })
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                Path.Combine(oldDirectoryA, "mod.ini"),
                "[Constants]\n" +
                "global persist $showBody = 0\n\n" +
                "[Key showBody]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = cycle\n" +
                "$showBody = 0,1\n");
            File.WriteAllText(
                Path.Combine(oldDirectoryB, "mod.ini"),
                "[Constants]\n" +
                "global persist $showBody = 1\n\n" +
                "[Key showBody]\n" +
                "condition = $object_detected\n" +
                "key = F4\n" +
                "type = cycle\n" +
                "$showBody = 0,1\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showBody = 1\n\n" +
                "[Key showBody]\n" +
                "condition = $object_detected\n" +
                "key = F8\n" +
                "type = cycle\n" +
                "$showBody = 0,1\n");

            SetCandidateTimestamp(oldDirectoryA, new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(oldDirectoryB, new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc));
            SetCandidateTimestamp(newDirectory, new DateTime(2026, 1, 2, 1, 0, 0, DateTimeKind.Utc));

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(characterDirectory);

                var jobs = service.CreatePairingJobs(candidates, candidates);

                Assert.Equal(2, jobs.Count);
                Assert.All(jobs, job => Assert.Equal(newDirectory, job.NewCandidate.FullPath));
                Assert.All(jobs, job => Assert.Equal(VersionSyncJobKind.CloneFromTemplate, job.JobKind));

                foreach (var job in jobs)
                {
                    var comparison = service.BuildComparison(job);
                    Assert.Contains("[Key showBody]", comparison.ResultBuffer.Content);
                }
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldSyncMatchedToggleAndParameterValues()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 0\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $showHat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F9\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $showHat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var toggleDiff = Assert.Single(comparison.ToggleDiffItems.Where(item => item.SectionName == "Key showHat"));
                Assert.Equal(VersionSyncDiffStatus.Updated, toggleDiff.Status);
                Assert.Equal("F1", toggleDiff.ResultKeyBindingsText);

                var parameterDiff = Assert.Single(comparison.ParameterDiffItems.Where(item => item.Name == "$showHat"));
                Assert.Equal(VersionSyncDiffStatus.Updated, parameterDiff.Status);
                Assert.Equal("0", parameterDiff.ResultDefaultValue);

                Assert.Contains("key = F1", comparison.ResultBuffer.Content);
                Assert.Contains("global persist $showHat = 0", comparison.ResultBuffer.Content);
                Assert.DoesNotContain("key = F9", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldMergeToggleTargetValuesAndPreserveNewExtras()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_7d286");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_69273");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $swapvar_clothing = -1\n\n" +
                "[KeySwapClothing]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $swapvar_clothing = -1\n\n" +
                "[KeySwapClothing]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$swapvar_clothing = -1, 0, 1, 6, 7\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var toggleDiff = Assert.Single(comparison.ToggleDiffItems.Where(item => item.SectionName == "KeySwapClothing"));
                Assert.Equal(VersionSyncDiffStatus.Updated, toggleDiff.Status);
                Assert.Contains("$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5, 6, 7", comparison.ResultBuffer.Content);
                Assert.DoesNotContain("$swapvar_clothing = -1, 0, 1, 6, 7", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldAppendMissingClothingValues_From636546Sample()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_7d286");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[636546] rover_astral_modulator_nsfw_normal_69273");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $swapvar_clothing = 0\n\n" +
                "[KeySwapClothing]\n" +
                "condition = $object_detected == 1\n" +
                "key = CONTROL 0\n" +
                "type = cycle\n" +
                "$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $swapvar_clothing = 0\n\n" +
                "[KeySwapClothing]\n" +
                "condition = $object_detected == 1\n" +
                "key = CONTROL 0\n" +
                "type = cycle\n" +
                "$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var toggleDiff = Assert.Single(comparison.ToggleDiffItems.Where(item => item.SectionName == "KeySwapClothing"));
                Assert.Equal(VersionSyncDiffStatus.Updated, toggleDiff.Status);
                Assert.Contains(
                    "$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30",
                    comparison.ResultBuffer.Content);
                Assert.Equal(
                    "$swapvar_clothing = -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30",
                    toggleDiff.ResultTargetValuesText);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldCreateMissingVisibilityBindingFromOldConfig()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 0\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "if $showHat == 1\n" +
                "    ; Draw Component 0.HAT\n" +
                "    drawindexed = 1, 0, 0\n" +
                "endif\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $other = 1\n\n" +
                "[Key other]\n" +
                "condition = $object_detected\n" +
                "key = NUMPAD9\n" +
                "type = cycle\n" +
                "$other = 0,1\n\n" +
                "[TextureOverrideComponent0]\n" +
                "; Draw Component 0.HAT\n" +
                "drawindexed = 1, 0, 0\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var visibilityDiff = Assert.Single(comparison.VisibilityDiffItems.Where(item => item.VisibilityKey.Contains("TextureOverrideComponent0", StringComparison.OrdinalIgnoreCase)));
                Assert.Equal(VersionSyncDiffStatus.Created, visibilityDiff.Status);
                Assert.Contains("补建模型控制绑定", visibilityDiff.Detail);

                Assert.Contains("global persist $showHat = 0", comparison.ResultBuffer.Content);
                Assert.Contains("[Key showHat]", comparison.ResultBuffer.Content);
                Assert.Contains("key = NUMPAD1", comparison.ResultBuffer.Content);
                Assert.Contains("if $showHat == 1", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldMarkDuplicateToggleSectionsAsManualReviewInsteadOfThrowing()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 0\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F1\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F2\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F9\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var toggleDiff = Assert.Single(comparison.ToggleDiffItems.Where(item => item.SectionName == "Key showHat"));
                Assert.Equal(VersionSyncDiffStatus.ManualReview, toggleDiff.Status);
                Assert.False(toggleDiff.CanApply);
                Assert.Contains("key = F9", comparison.ResultBuffer.Content);
                Assert.DoesNotContain("key = F1", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldMarkDuplicateVisibilityItemsAsManualReviewInsteadOfThrowing()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_a9e13");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[589362] rover_bikini_warrior_fee67");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n\n" +
                "[TextureOverrideComponent4]\n" +
                "; Draw Component 4.Garter\n" +
                "drawindexed = 1, 0, 0\n\n" +
                "[TextureOverrideComponent4]\n" +
                "; Draw Component 4.Garter\n" +
                "drawindexed = 1, 0, 0\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n\n" +
                "[TextureOverrideComponent4]\n" +
                "; Draw Component 4.Garter\n" +
                "drawindexed = 1, 0, 0\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var visibilityDiff = Assert.Single(comparison.VisibilityDiffItems.Where(item => item.VisibilityKey.Contains("TextureOverrideComponent4", StringComparison.OrdinalIgnoreCase)));
                Assert.Equal(VersionSyncDiffStatus.ManualReview, visibilityDiff.Status);
                Assert.Contains("重复", visibilityDiff.Detail);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void BuildComparison_ShouldMarkDuplicateToggleTargetVariablesAsManualReviewInsteadOfThrowing()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectory = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_072a4_v1");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_58df2");
            Directory.CreateDirectory(oldDirectory);
            Directory.CreateDirectory(newDirectory);

            File.WriteAllText(
                Path.Combine(oldDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showBody = 0\n\n" +
                "[Key showBody]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = cycle\n" +
                "$showBody = 0,1\n" +
                "$showBody = 0,1,2\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "mod.ini"),
                "[Constants]\n" +
                "global persist $showBody = 1\n\n" +
                "[Key showBody]\n" +
                "condition = $object_detected\n" +
                "key = F8\n" +
                "type = cycle\n" +
                "$showBody = 0,1\n");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == oldDirectory));
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(new[] { oldCandidate }, new[] { newCandidate }));

                var comparison = service.BuildComparison(job);

                var toggleDiff = Assert.Single(comparison.ToggleDiffItems.Where(item => item.SectionName == "Key showBody"));
                Assert.Equal(VersionSyncDiffStatus.ManualReview, toggleDiff.Status);
                Assert.False(toggleDiff.CanApply);
                Assert.DoesNotContain("$showBody = 0,1,2", comparison.ResultBuffer.Content);
                Assert.Contains("$showBody = 0,1", comparison.ResultBuffer.Content);
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        [Fact]
        public void ApplyComparison_ShouldCloneTemplateDirectoryForManyToOneJob()
        {
            var tempRoot = CreateTempRoot();
            var modRoot = Path.Combine(tempRoot, "Mods");
            var oldDirectoryA = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_072a4_v1");
            var oldDirectoryB = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_072a4_v2");
            var newDirectory = Path.Combine(modRoot, "13 Rover", "[637753] rover_belly_dancer_nsfw_58df2");

            Directory.CreateDirectory(Path.Combine(oldDirectoryA, "Config"));
            Directory.CreateDirectory(Path.Combine(oldDirectoryB, "Config"));
            Directory.CreateDirectory(Path.Combine(newDirectory, "Config"));
            Directory.CreateDirectory(Path.Combine(newDirectory, "Assets"));

            File.WriteAllText(
                Path.Combine(oldDirectoryA, "Config", "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 0\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F3\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");
            File.WriteAllText(Path.Combine(oldDirectoryB, "Config", "mod.ini"), "[Constants]\n");
            File.WriteAllText(
                Path.Combine(newDirectory, "Config", "mod.ini"),
                "[Constants]\n" +
                "global persist $showHat = 1\n\n" +
                "[Key showHat]\n" +
                "condition = $object_detected\n" +
                "key = F8\n" +
                "type = cycle\n" +
                "$showHat = 0,1\n");
            File.WriteAllText(Path.Combine(newDirectory, "Assets", "marker.txt"), "template");

            try
            {
                var service = CreateService();
                var candidates = service.DiscoverModCandidates(modRoot);
                var oldCandidates = candidates.Where(candidate => candidate.FullPath == oldDirectoryA || candidate.FullPath == oldDirectoryB).ToArray();
                var newCandidate = Assert.Single(candidates.Where(candidate => candidate.FullPath == newDirectory));
                var job = Assert.Single(service.CreatePairingJobs(oldCandidates, new[] { newCandidate }).Where(item => item.OldCandidate.FullPath == oldDirectoryA));

                var comparison = service.BuildComparison(job);
                var applyResult = service.ApplyComparison(comparison);

                Assert.Equal(VersionSyncJobKind.CloneFromTemplate, applyResult.Job.JobKind);
                Assert.True(Directory.Exists(applyResult.OutputDirectoryPath));
                Assert.True(File.Exists(Path.Combine(applyResult.OutputDirectoryPath, "Assets", "marker.txt")));
                Assert.True(File.Exists(applyResult.OutputConfigPath));
                Assert.EndsWith(Path.Combine("Config", "mod.ini"), applyResult.OutputConfigPath, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("key = F3", File.ReadAllText(applyResult.OutputConfigPath));
                Assert.Contains("global persist $showHat = 0", File.ReadAllText(applyResult.OutputConfigPath));
            }
            finally
            {
                DeleteTempRoot(tempRoot);
            }
        }

        private static IModConfigVersionSyncService CreateService()
        {
            var fileSystem = new FileSystemService();
            var discoveryService = new ModConfigDiscoveryService(fileSystem);
            var parser = new ModConfigParser(fileSystem);
            var analysis = new ModConfigAnalysisService(parser);
            var update = new ModConfigUpdateService(fileSystem, parser, analysis);
            return new ModConfigVersionSyncService(fileSystem, discoveryService, parser, analysis, update);
        }

        private static string CreateTempRoot()
        {
            return Path.Combine(Path.GetTempPath(), $"WuwaModModifier_VersionSyncTests_{Path.GetRandomFileName()}");
        }

        private static void DeleteTempRoot(string tempRoot)
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        private static void SetCandidateTimestamp(string directoryPath, DateTime timestampUtc)
        {
            Directory.SetCreationTimeUtc(directoryPath, timestampUtc);
            Directory.SetLastWriteTimeUtc(directoryPath, timestampUtc);

            var configPath = Path.Combine(directoryPath, "mod.ini");
            if (File.Exists(configPath))
            {
                File.SetCreationTimeUtc(configPath, timestampUtc);
                File.SetLastWriteTimeUtc(configPath, timestampUtc);
            }
        }
    }
}