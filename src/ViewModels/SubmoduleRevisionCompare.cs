using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SubmoduleRevisionCompare : ObservableObject
    {
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public Models.Commit Base
        {
            get => _base;
            private set => SetProperty(ref _base, value);
        }

        public Models.Commit To
        {
            get => _to;
            private set => SetProperty(ref _to, value);
        }

        public int UncommittedChanges
        {
            get;
        }

        public bool IncludeUncommittedChanges
        {
            get => _includeUncommittedChanges;
            set
            {
                if (SetProperty(ref _includeUncommittedChanges, value))
                {
                    // Local changes are applied on top of the submodule's HEAD, so it must be the target.
                    if (value && _base == _head)
                        (Base, To) = (To, Base);

                    Refresh();
                }
            }
        }

        public int TotalChanges
        {
            get => _totalChanges;
            private set => SetProperty(ref _totalChanges, value);
        }

        public List<Models.Change> VisibleChanges
        {
            get => _visibleChanges;
            private set => SetProperty(ref _visibleChanges, value);
        }

        public ChangeSelection ChangeSelection
        {
            get => _changeSelection;
            set
            {
                if (SetProperty(ref _changeSelection, value))
                {
                    if (value is { Count: 1, HasFolder: false })
                        DiffContext = new DiffContext(_repo, CreateDiffOption(value.Changes[0]), _diffContext);
                    else
                        DiffContext = null;
                }
            }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    RefreshVisible();
            }
        }

        public DiffContext DiffContext
        {
            get => _diffContext;
            private set => SetProperty(ref _diffContext, value);
        }

        public SubmoduleRevisionCompare(Models.SubmoduleDiff diff)
        {
            _repo = diff.FullPath;
            _base = diff.Old.Commit;
            _to = diff.New.Commit;
            _head = diff.New.Commit;

            // Only the working copy side of a submodule diff has uncommitted changes.
            UncommittedChanges = diff.New.UncommittedChanges;
            _includeUncommittedChanges = UncommittedChanges > 0;

            Refresh();
        }

        public void Swap()
        {
            if (_includeUncommittedChanges)
                return;

            (Base, To) = (To, Base);
            Refresh();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        public string GetAbsPath(string path)
        {
            return Native.OS.GetAbsPath(_repo, path);
        }

        public void OpenInExternalDiffTool(Models.Change change)
        {
            new Commands.DiffTool(_repo, CreateDiffOption(change)).Open();
        }

        public async Task<bool> SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
        {
            var opts = changes.ConvertAll(CreateDiffOption);
            return await Commands.SaveChangesAsPatch.ProcessStashChangesAsync(_repo, opts, saveTo);
        }

        private Models.DiffOption CreateDiffOption(Models.Change change)
        {
            if (change.WorkTree == Models.ChangeState.Untracked)
                return new Models.DiffOption(change, true);

            // Empty target revision means the working tree.
            return new Models.DiffOption(_base.SHA, _includeUncommittedChanges ? string.Empty : _to.SHA, change);
        }

        private void Refresh()
        {
            IsLoading = true;
            VisibleChanges = [];
            ChangeSelection = new(null);

            var baseRevision = _base.SHA;
            var targetRevision = _includeUncommittedChanges ? string.Empty : _to.SHA;
            Task.Run(async () =>
            {
                var changes = await new Commands.CompareRevisions(_repo, baseRevision, targetRevision)
                    .ReadAsync()
                    .ConfigureAwait(false);

                if (string.IsNullOrEmpty(targetRevision))
                {
                    // `git diff` does not list untracked files.
                    var locals = await new Commands.QueryLocalChanges(_repo).GetResultAsync().ConfigureAwait(false);
                    foreach (var c in locals)
                    {
                        if (c.WorkTree != Models.ChangeState.Untracked)
                            continue;

                        var added = new Models.Change() { Path = c.Path };
                        added.Set(Models.ChangeState.Added, Models.ChangeState.Untracked);
                        changes.Add(added);
                    }

                    changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
                }

                _changes = changes;

                var visible = _changes;
                if (!string.IsNullOrWhiteSpace(_searchFilter))
                {
                    visible = new List<Models.Change>();
                    foreach (var c in _changes)
                    {
                        if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(c);
                    }
                }

                Dispatcher.UIThread.Post(() =>
                {
                    TotalChanges = _changes.Count;
                    VisibleChanges = visible;
                    IsLoading = false;

                    if (VisibleChanges.Count > 0)
                        ChangeSelection = new(VisibleChanges.GetRange(0, 1));
                });
            });
        }

        private void RefreshVisible()
        {
            if (_changes == null)
                return;

            if (string.IsNullOrEmpty(_searchFilter))
            {
                VisibleChanges = _changes;
            }
            else
            {
                var visible = new List<Models.Change>();
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }

                VisibleChanges = visible;
            }
        }

        private string _repo;
        private bool _isLoading = true;
        private Models.Commit _base = null;
        private Models.Commit _to = null;
        private Models.Commit _head = null;
        private bool _includeUncommittedChanges = false;
        private int _totalChanges = 0;
        private List<Models.Change> _changes = null;
        private List<Models.Change> _visibleChanges = null;
        private ChangeSelection _changeSelection = new(null);
        private string _searchFilter = string.Empty;
        private DiffContext _diffContext = null;
    }
}
