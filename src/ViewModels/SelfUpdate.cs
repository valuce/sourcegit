using System;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SelfUpdateInstalling : ObservableObject
    {
        public Models.Version Version { get; }

        public double Percent
        {
            get => _percent;
            set => SetProperty(ref _percent, value);
        }

        public bool IsApplying
        {
            get => _isApplying;
            set => SetProperty(ref _isApplying, value);
        }

        public SelfUpdateInstalling(Models.Version ver)
        {
            Version = ver;
        }

        private double _percent = 0;
        private bool _isApplying = false;
    }

    public class SelfUpdate : ObservableObject
    {
        public object Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        /// <summary>
        ///     Downloads and installs the given release. Returns true when the new files are in place
        ///     and the app should restart. Returns false on failure (shown in the dialog) or when the
        ///     dialog was closed meanwhile (the new version then starts with the next launch).
        /// </summary>
        public async Task<bool> InstallAsync(Models.Version ver)
        {
            var state = new SelfUpdateInstalling(ver);
            var progress = new Progress<double>(v =>
            {
                if (v < 0)
                    state.IsApplying = true;
                else
                    state.Percent = v;
            });

            _cancellation = new CancellationTokenSource();
            Data = state;

            try
            {
                await Models.SelfUpdater.InstallAsync(ver, progress, _cancellation.Token);
                return !_cancellation.IsCancellationRequested;
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception e)
            {
                Data = new Models.SelfUpdateFailed(e);
                return false;
            }
        }

        public void Restart()
        {
            try
            {
                Models.SelfUpdater.LaunchUpdatedApp();
                App.Quit(0);
            }
            catch (Exception e)
            {
                // Files are already replaced: the new version will start next time.
                Data = new Models.SelfUpdateFailed(e);
            }
        }

        public void CancelInstall()
        {
            _cancellation?.Cancel();
        }

        private object _data = null;
        private CancellationTokenSource _cancellation = null;
    }
}
