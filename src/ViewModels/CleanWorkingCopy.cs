using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Removes files that are not tracked by git (`git clean`), with a choice of what to remove.
    ///     Tracked files, modified or not, are left as they are.
    /// </summary>
    public class CleanWorkingCopy : Popup
    {
        public bool IncludeUntracked
        {
            get => _includeUntracked;
            set
            {
                if (SetProperty(ref _includeUntracked, value))
                    OnPropertyChanged(nameof(HasAnythingToClean));
            }
        }

        public bool IncludeIgnored
        {
            get => _includeIgnored;
            set
            {
                if (SetProperty(ref _includeIgnored, value))
                    OnPropertyChanged(nameof(HasAnythingToClean));
            }
        }

        public bool IncludeNestedRepositories
        {
            get;
            set;
        }

        public bool HasSubmodules
        {
            get => _repo.Submodules.Count > 0;
        }

        public bool IncludeSubmodules
        {
            get;
            set;
        }

        public bool HasAnythingToClean
        {
            get => _includeUntracked || _includeIgnored;
        }

        public CleanWorkingCopy(Repository repo)
        {
            _repo = repo;
            _includeUntracked = repo.UIStates.CleanUntrackedFiles;
            _includeIgnored = repo.UIStates.CleanIgnoredFiles;
            IncludeNestedRepositories = repo.UIStates.CleanNestedRepositories;
            IncludeSubmodules = repo.UIStates.CleanSubmodules;
        }

        public override async Task<bool> Sure()
        {
            if (!HasAnythingToClean)
                return true;

            _repo.UIStates.CleanUntrackedFiles = _includeUntracked;
            _repo.UIStates.CleanIgnoredFiles = _includeIgnored;
            _repo.UIStates.CleanNestedRepositories = IncludeNestedRepositories;
            if (HasSubmodules)
                _repo.UIStates.CleanSubmodules = IncludeSubmodules;

            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Clean working copy ...";

            var log = _repo.CreateLog("Clean Working Copy");
            Use(log);

            var mode = (_includeUntracked, _includeIgnored) switch
            {
                (true, true) => Models.CleanMode.UntrackedAndIgnoredFiles,
                (true, false) => Models.CleanMode.OnlyUntrackedFiles,
                _ => Models.CleanMode.OnlyIgnoredFiles,
            };

            // Not quiet, so the log tells which paths have been removed.
            await new Commands.Clean(_repo.FullPath, mode, IncludeNestedRepositories, false)
                .Use(log)
                .ExecAsync();

            var cleanSubmodules = HasSubmodules && IncludeSubmodules;
            if (cleanSubmodules)
            {
                await new Commands.Clean(_repo.FullPath, mode, IncludeNestedRepositories, false)
                    .InSubmodules()
                    .Use(log)
                    .ExecAsync();
            }

            log.Complete();

            // Submodules with untracked files are shown as modified, so their states have changed too.
            if (cleanSubmodules)
                await _repo.RefreshSubmodulesAsync();

            _repo.MarkWorkingCopyDirtyManually();
            return true;
        }

        private readonly Repository _repo;
        private bool _includeUntracked;
        private bool _includeIgnored;
    }
}
