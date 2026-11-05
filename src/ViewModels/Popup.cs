using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public partial class Popup : ObservableValidator, Models.ICommandLogReceiver
    {
        public bool CanTerminate
        {
            get => _canTerminate;
            protected set
            {
                if (OperatingSystem.IsWindows() || Native.OS.SupportSetSid())
                    SetProperty(ref _canTerminate, value);
            }
        }

        public bool InProgress
        {
            get => _inProgress;
            set => SetProperty(ref _inProgress, value);
        }

        public string ProgressDescription
        {
            get => _progressDescription;
            set => SetProperty(ref _progressDescription, value);
        }

        /// <summary>
        ///     Progress in percent, or a negative value if it is unknown.
        /// </summary>
        public double Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }

        [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026:RequiresUnreferencedCode")]
        public bool Check()
        {
            if (HasErrors)
                return false;
            ValidateAllProperties();
            return !HasErrors;
        }

        public void OnReceiveCommandLog(string data)
        {
            var desc = data.Trim();
            if (string.IsNullOrEmpty(desc))
                return;

            ProgressDescription = desc;

            // Operations with several steps only report finished steps, because every git command has its own phases.
            if (_totalSteps > 1)
                return;

            // Both git and git-lfs report progress like `Receiving objects:  45% (123/456)`.
            var match = REG_PROGRESS().Match(desc);
            Progress = match.Success ? Math.Min(double.Parse(match.Groups[1].Value), 100) : -1;
        }

        public void Cleanup()
        {
            _log?.Unsubscribe(this);
        }

        public virtual bool CanStartDirectly()
        {
            return true;
        }

        public virtual Task<bool> Sure()
        {
            return null;
        }

        public virtual void Terminate()
        {
        }

        protected void Use(CommandLog log)
        {
            _log = log;
            _log.Subscribe(this);
        }

        protected void SetProgressStep(int step, int totalSteps)
        {
            _totalSteps = totalSteps;
            Progress = totalSteps > 1 ? step * 100.0 / totalSteps : -1;
        }

        [GeneratedRegex(@":\s*(\d{1,3})% \(\d+/\d+\)")]
        private static partial Regex REG_PROGRESS();

        private bool _canTerminate = false;
        private bool _inProgress = false;
        private string _progressDescription = string.Empty;
        private double _progress = -1;
        private int _totalSteps = 0;
        private CommandLog _log = null;
    }
}
