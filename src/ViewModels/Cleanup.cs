﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Cleanup : Popup
    {
        public bool HasSubmodules
        {
            get => _submodules.Count > 0;
        }

        public bool IncludeSubmodules
        {
            get;
            set;
        } = true;

        public bool CanPruneLFSObjects
        {
            get;
        }

        public bool PruneLFSObjects
        {
            get;
            set;
        } = false;

        public Cleanup(Repository repo)
        {
            _repo = repo;

            foreach (var submodule in repo.Submodules)
            {
                var fullPath = Path.GetFullPath(Path.Combine(repo.FullPath, submodule.Path));
                var gitDir = GetSubmoduleGitDir(fullPath);
                if (!string.IsNullOrEmpty(gitDir))
                    _submodules.Add((submodule.Path, fullPath, gitDir));
            }

            CanPruneLFSObjects = UsesLFS(repo.GitDir) || _submodules.Exists(x => UsesLFS(x.GitDir));
        }

        public override bool CanStartDirectly()
        {
            return !HasSubmodules && !CanPruneLFSObjects;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Cleanup (GC & prune) ...";

            var log = _repo.CreateLog("Cleanup (GC & prune)");
            Use(log);

            var totalSteps = IncludeSubmodules ? _submodules.Count + 1 : 1;
            SetProgressStep(0, totalSteps);
            await CleanupAsync(_repo.FullPath, _repo.GitDir, log);

            if (IncludeSubmodules)
            {
                for (var i = 0; i < _submodules.Count; i++)
                {
                    var submodule = _submodules[i];
                    SetProgressStep(i + 1, totalSteps);
                    log.AppendLine($"Entering '{submodule.Path}'");
                    await CleanupAsync(submodule.FullPath, submodule.GitDir, log);
                }
            }

            log.Complete();
            return true;
        }

        private async Task CleanupAsync(string repo, string gitDir, CommandLog log)
        {
            await new Commands.GC(repo)
                .Use(log)
                .ExecAsync();

            if (PruneLFSObjects && UsesLFS(gitDir))
                await new Commands.LFS(repo)
                    .Use(log)
                    .PruneAsync();
        }

        private static bool UsesLFS(string gitDir)
        {
            if (Directory.Exists(Path.Combine(gitDir, "lfs", "objects")))
                return true;

            // With a custom `lfs.storage` there are no objects in the git dir, but Git LFS still installs its hooks here.
            try
            {
                var hook = Path.Combine(gitDir, "hooks", "pre-push");
                return File.Exists(hook) && File.ReadAllText(hook).Contains("git lfs pre-push");
            }
            catch
            {
                return false;
            }
        }

        private static string GetSubmoduleGitDir(string fullPath)
        {
            var dotGit = Path.Combine(fullPath, ".git");
            if (Directory.Exists(dotGit))
                return dotGit;

            if (!File.Exists(dotGit))
                return null;

            // Usually a submodule has a `.git` file like `gitdir: ../.git/modules/name`.
            var redirect = File.ReadAllText(dotGit).Trim();
            if (!redirect.StartsWith("gitdir: ", StringComparison.Ordinal))
                return null;

            redirect = redirect.Substring(8);
            return Path.IsPathRooted(redirect) ? redirect : Path.GetFullPath(Path.Combine(fullPath, redirect));
        }

        private readonly Repository _repo = null;
        private readonly List<(string Path, string FullPath, string GitDir)> _submodules = [];
    }
}
