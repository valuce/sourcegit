using System;
using System.Collections.Generic;

using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Registry of all configurable hotkeys. Defaults live here, user overrides are stored in
    ///     `Preferences.Hotkeys` (command id => gesture, an empty string means "no hotkey").
    /// </summary>
    public class HotkeyManager : ObservableObject
    {
        public static HotkeyManager Instance => _instance ??= new HotkeyManager();

        /// <summary>
        ///     Incremented on every change, so bindings that depend on hotkeys can refresh.
        /// </summary>
        public int Revision
        {
            get => _revision;
            private set => SetProperty(ref _revision, value);
        }

        public List<Models.HotkeyDefinition> Definitions { get; } = new();

        public event Action Changed;

        private HotkeyManager()
        {
            var cmd = Models.HotkeyGesture.Cmd;
            var isMacOS = OperatingSystem.IsMacOS();

            // Global
            Add("Global.OpenPreferences", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.OpenPreferences", Key.OemComma, cmd);
            Add("Global.OpenHotkeys", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.OpenHotkeys", Key.F1);
            Add("Global.NewTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.NewTab", Key.T, cmd);
            Add("Global.CloseTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.CloseTab", Key.W, cmd);
            if (isMacOS)
            {
                Add("Global.GotoPrevTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.GotoPrevTab", Key.Left, cmd | KeyModifiers.Alt);
                Add("Global.GotoNextTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.GotoNextTab", Key.Right, cmd | KeyModifiers.Alt);
            }
            else
            {
                Add("Global.GotoPrevTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.GotoPrevTab", Key.Tab, KeyModifiers.Control | KeyModifiers.Shift);
                Add("Global.GotoNextTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.GotoNextTab", Key.Tab, KeyModifiers.Control);
            }
            Add("Global.SwitchTab", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.SwitchTab", Key.P, cmd, false);
            Add("Global.ShowWorkspaceMenu", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.ShowWorkspaceDropdownMenu", Key.P, cmd | KeyModifiers.Alt, false);
            Add("Global.ConfigureWorkspace", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.ConfigureWorkspace");
            Add("Global.Clone", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.Clone", Key.R, cmd);
            Add("Global.OpenLocalRepository", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.OpenLocalRepository", Key.L, cmd);
            Add("Global.OpenTerminal", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.OpenTerminal", Key.OemTilde, KeyModifiers.Control);
            Add("Global.Refresh", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.Refresh", Key.F5);
            Add("Global.ZoomIn", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.ZoomIn", Key.OemPlus, cmd, false);
            Add("Global.ZoomOut", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.ZoomOut", Key.OemMinus, cmd, false);
            Add("Global.ZoomReset", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Hotkeys.Global.ZoomReset", Key.None, KeyModifiers.None, false);
            Add("Global.SSHKeyHelper", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "SSHKeyHelper");
            if (App.IsCheckForUpdateCommandVisible)
                Add("Global.CheckForUpdate", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "SelfUpdate");
            Add("Global.Quit", Models.HotkeyGroup.Global, Models.HotkeyScope.Global, "Quit", Key.Q, cmd);

            // Repository
            Add("Repo.ViewHistories", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.ViewDashboard", Key.D1, cmd);
            Add("Repo.ViewChanges", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.ViewChanges", Key.D2, cmd);
            Add("Repo.ViewStashes", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.ViewStashes", Key.D3, cmd);
            Add("Repo.OpenCommandPalette", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.OpenCommandPalette", Key.P, cmd | KeyModifiers.Shift);
            Add("Repo.Fetch", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Fetch", Key.Down, cmd);
            Add("Repo.FetchWithOptions", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.FetchWithOptions");
            Add("Repo.Pull", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Pull", Key.Down, cmd | KeyModifiers.Shift);
            Add("Repo.PullWithOptions", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.PullWithOptions");
            Add("Repo.Push", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Push", Key.Up, cmd | KeyModifiers.Shift);
            Add("Repo.PushWithOptions", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.PushWithOptions");
            Add("Repo.Stash", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Stash");
            Add("Repo.StashDirectly", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.StashDirectly");
            Add("Repo.ApplyPatch", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Apply.Title");
            Add("Repo.DiscardAll", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.DiscardAll");
            Add("Repo.CleanWorkingCopy", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "CleanWorkingCopy");
            Add("Repo.NewBranch", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.CreateBranch", Key.B, cmd);
            Add("Repo.NewBranchFromSelected", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.CreateBranchFromSelected", Key.B, cmd | KeyModifiers.Shift);
            Add("Repo.NewTag", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.CreateTag");
            Add("Repo.NewTagFromSelected", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.CreateTagFromSelected", Key.T, cmd | KeyModifiers.Shift);
            Add("Repo.Checkout", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Checkout");
            Add("Repo.Merge", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Merge");
            Add("Repo.Compare", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Compare");
            Add("Repo.Blame", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Blame");
            Add("Repo.FileHistory", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.FileHistory");
            Add("Repo.OpenFile", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.OpenFile");
            Add("Repo.CustomActions", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.CustomActions");
            Add("Repo.Explore", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.Explore", Key.E, cmd);
            Add("Repo.ExternalTools", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.ExternalTools");
            Add("Repo.GitFlow", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.GitFlow");
            Add("Repo.GitLFS", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.GitLFS");
            Add("Repo.Bisect", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Hotkeys.Repo.Bisect");
            Add("Repo.Cleanup", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.Clean");
            Add("Repo.ViewLogs", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.ViewLogs");
            Add("Repo.Statistics", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.Statistics");
            Add("Repo.Configure", Models.HotkeyGroup.Repository, Models.HotkeyScope.Repository, "Repository.Configure");

            // History
            Add("Repo.FindInHistory", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.FindInHistory", Key.F, cmd);
            Add("Repo.ToggleSearchCommits", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.ToggleSearchCommits", Key.F, cmd | KeyModifiers.Shift);
            Add("Repo.NavigateToHead", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Repository.NavigateToCurrentHead");
            Add("Repo.ToggleFocusOnCurrentBranch", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.ToggleFocusOnCurrentBranch");
            Add("History.GotoParent", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.GoToParent", Key.Down, KeyModifiers.Alt, false);
            Add("History.GotoChild", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.GoToChild", Key.Up, KeyModifiers.Alt, false);
            Add("History.ToggleCommitDetail", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.Repo.ToggleCommitDetailPanel", Key.J, cmd, false);
            Add("History.OpenDetailsStandalone", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.History.OpenDetailsStandalone", Key.N, cmd, false);
            Add("History.OpenRevisionFile", Models.HotkeyGroup.History, Models.HotkeyScope.Repository, "Hotkeys.History.OpenRevisionFile", Key.O, cmd, false);

            // Local changes
            Add("WorkingCopy.Commit", Models.HotkeyGroup.LocalChanges, Models.HotkeyScope.Repository, "Hotkeys.Repo.Commit", Key.Enter, cmd, false);
            Add("WorkingCopy.CommitWithAutoStage", Models.HotkeyGroup.LocalChanges, Models.HotkeyScope.Repository, "Hotkeys.Repo.CommitWithAutoStage", Key.Enter, cmd | KeyModifiers.Shift, false);
            Add("WorkingCopy.CommitAndPush", Models.HotkeyGroup.LocalChanges, Models.HotkeyScope.Repository, "Hotkeys.Repo.CommitAndPush", Key.Enter, cmd | KeyModifiers.Alt, false);
            Add("WorkingCopy.StageAll", Models.HotkeyGroup.LocalChanges, Models.HotkeyScope.Repository, "Hotkeys.WorkingCopy.StageAll", Key.S, cmd | KeyModifiers.Shift, false);
            Add("WorkingCopy.UnstageAll", Models.HotkeyGroup.LocalChanges, Models.HotkeyScope.Repository, "Hotkeys.WorkingCopy.UnstageAll", Key.U, cmd | KeyModifiers.Shift, false);

            // Diff
            Add("Diff.GotoFirstChange", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Diff.First", Key.Home, cmd | KeyModifiers.Alt, false);
            Add("Diff.GotoPrevChange", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Diff.Prev", Key.Up, cmd | KeyModifiers.Alt, false);
            Add("Diff.GotoNextChange", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Diff.Next", Key.Down, cmd | KeyModifiers.Alt, false);
            Add("Diff.GotoLastChange", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Diff.Last", Key.End, cmd | KeyModifiers.Alt, false);
            Add("Diff.OpenExternalMergeTool", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Diff.UseMerger", Key.D, cmd | KeyModifiers.Shift, false);
            Add("Diff.StageChunk", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Hotkeys.Diff.StageChunk", Key.S, cmd, false);
            Add("Diff.UnstageChunk", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Hotkeys.Diff.UnstageChunk", Key.U, cmd, false);
            Add("Diff.DiscardChunk", Models.HotkeyGroup.Diff, Models.HotkeyScope.Repository, "Hotkeys.Diff.DiscardChunk", Key.D, cmd, false);

            // Welcome page
            Add("Welcome.Search", Models.HotkeyGroup.Welcome, Models.HotkeyScope.Welcome, "Hotkeys.Welcome.Search", Key.F, cmd, false);

            LoadOverrides();
        }

        public Models.HotkeyDefinition Find(string id)
        {
            return _byId.GetValueOrDefault(id);
        }

        /// <summary>
        ///     Gesture currently assigned to the command, or null when it has none.
        /// </summary>
        public KeyGesture Get(string id)
        {
            return _effective.GetValueOrDefault(id);
        }

        public string GetText(string id)
        {
            return Models.HotkeyGesture.Format(Get(id));
        }

        public bool Matches(string id, KeyEventArgs e)
        {
            return Models.HotkeyGesture.Matches(Get(id), e);
        }

        public bool IsCustomized(string id)
        {
            return _overrides.ContainsKey(id);
        }

        public bool HasCustomized()
        {
            return _overrides.Count > 0;
        }

        /// <summary>
        ///     Commands whose scope overlaps with `id` and that currently use `gesture`.
        /// </summary>
        public List<Models.HotkeyDefinition> FindConflicts(string id, KeyGesture gesture)
        {
            var conflicts = new List<Models.HotkeyDefinition>();
            var def = Find(id);
            if (def == null || gesture == null)
                return conflicts;

            foreach (var other in Definitions)
            {
                if (other.Id == id || !def.OverlapsWith(other))
                    continue;

                if (Models.HotkeyGesture.AreEqual(Get(other.Id), gesture))
                    conflicts.Add(other);
            }

            return conflicts;
        }

        /// <summary>
        ///     Assigns a gesture to a command. Pass null to remove its hotkey.
        /// </summary>
        public void Set(string id, KeyGesture gesture)
        {
            SetWithoutNotify(id, gesture);
            Commit();
        }

        /// <summary>
        ///     Assigns a gesture and removes it from every conflicting command, as one change.
        /// </summary>
        public void Assign(string id, KeyGesture gesture, List<Models.HotkeyDefinition> conflicts)
        {
            foreach (var other in conflicts)
                SetWithoutNotify(other.Id, null);

            SetWithoutNotify(id, gesture);
            Commit();
        }

        public void Reset(string id)
        {
            if (_overrides.Remove(id))
                Commit();
        }

        public void ResetAll()
        {
            if (_overrides.Count == 0)
                return;

            _overrides.Clear();
            Commit();
        }

        private void Add(string id, Models.HotkeyGroup group, Models.HotkeyScope scope, string labelKey, Key key = Key.None, KeyModifiers modifiers = KeyModifiers.None, bool handledByLauncher = true)
        {
            var def = new Models.HotkeyDefinition(
                id,
                group,
                scope,
                labelKey,
                key == Key.None ? null : Models.HotkeyGesture.Create(key, modifiers),
                handledByLauncher);

            Definitions.Add(def);
            _byId[id] = def;
        }

        private void SetWithoutNotify(string id, KeyGesture gesture)
        {
            var def = Find(id);
            if (def == null)
                return;

            if (Models.HotkeyGesture.AreEqual(def.Default, gesture))
                _overrides.Remove(id);
            else
                _overrides[id] = gesture;
        }

        private void LoadOverrides()
        {
            var saved = Preferences.Instance.Hotkeys;
            if (saved != null)
            {
                foreach (var (id, text) in saved)
                {
                    var def = Find(id);
                    if (def == null)
                        continue;

                    if (string.IsNullOrEmpty(text))
                    {
                        _overrides[id] = null;
                        continue;
                    }

                    var gesture = Models.HotkeyGesture.Parse(text);
                    if (gesture != null)
                        _overrides[id] = gesture;
                }
            }

            RebuildEffective();
        }

        private void Commit()
        {
            var saved = new Dictionary<string, string>();
            foreach (var (id, gesture) in _overrides)
                saved[id] = Models.HotkeyGesture.Serialize(gesture);

            Preferences.Instance.Hotkeys = saved;
            Preferences.Instance.Save();

            RebuildEffective();
            Revision++;
            Changed?.Invoke();
        }

        private void RebuildEffective()
        {
            _effective.Clear();
            foreach (var def in Definitions)
            {
                var gesture = _overrides.TryGetValue(def.Id, out var custom) ? custom : def.Default;
                if (gesture != null)
                    _effective[def.Id] = gesture;
            }
        }

        private static HotkeyManager _instance = null;
        private int _revision = 0;
        private readonly Dictionary<string, Models.HotkeyDefinition> _byId = new();
        private readonly Dictionary<string, KeyGesture> _overrides = new();
        private readonly Dictionary<string, KeyGesture> _effective = new();
    }
}
