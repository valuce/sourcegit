using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DiscardAllMode
    {
        public bool IncludeModified
        {
            get;
            set;
        } = true;

        public bool IncludeUntracked
        {
            get;
            set;
        } = false;

        public bool IncludeIgnored
        {
            get;
            set;
        } = false;
    }

    public class DiscardSingleFile
    {
        public string Path
        {
            get;
            set;
        } = string.Empty;
    }

    public class DiscardMultipleFiles
    {
        public int Count
        {
            get;
            set;
        } = 0;
    }

    public class Discard : Popup
    {
        public object Mode
        {
            get;
        }

        public Discard(Repository repo)
        {
            _repo = repo;
            Mode = new DiscardAllMode();
        }

        public Discard(Repository repo, List<Models.Change> changes, Models.Change next)
        {
            _repo = repo;
            _changes = changes;
            _next = next;

            if (_changes == null)
                Mode = new DiscardAllMode();
            else if (_changes.Count == 1)
                Mode = new DiscardSingleFile() { Path = _changes[0].Path };
            else
                Mode = new DiscardMultipleFiles() { Count = _changes.Count };
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = _changes == null ? "Discard all local changes ..." : $"Discard total {_changes.Count} changes ...";

            var log = _repo.CreateLog("Discard Changes");
            Use(log);

            if (Mode is DiscardAllMode all)
            {
                await Commands.Discard.AllAsync(_repo.FullPath, all.IncludeModified, all.IncludeUntracked, all.IncludeIgnored, log);
                if (_repo.Submodules.Count > 0)
                {
                    await Commands.Discard.AllSubmodulesAsync(_repo.FullPath, all.IncludeModified, all.IncludeUntracked, all.IncludeIgnored, log);
                    _repo.RefreshSubmodules();
                }

                _repo.ClearCommitMessage();
            }
            else
            {
                // Discarding a submodule returns it to the revision recorded by this repository, without local changes.
                var changes = new List<Models.Change>();
                var submodules = new List<string>();
                foreach (var c in _changes)
                {
                    if (_repo.Submodules.Find(x => x.Path.Equals(c.Path, StringComparison.Ordinal)) != null)
                        submodules.Add(c.Path);
                    else
                        changes.Add(c);
                }

                if (changes.Count > 0)
                    await Commands.Discard.ChangesAsync(_repo.FullPath, changes, log);

                foreach (var submodule in submodules)
                    await Commands.Discard.SubmoduleAsync(_repo.FullPath, submodule, log);

                if (submodules.Count > 0)
                    _repo.RefreshSubmodules();
            }

            log.Complete();

            var selection = new ChangeSelection(null);
            if (_next != null)
                selection.Changes.Add(_next);
            _repo.WorkingCopy.SelectedUnstaged = selection;
            _repo.MarkWorkingCopyDirtyManually();
            return true;
        }

        private readonly Repository _repo = null;
        private readonly List<Models.Change> _changes = null;
        private readonly Models.Change _next = null;
    }
}
