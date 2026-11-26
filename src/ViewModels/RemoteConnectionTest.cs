using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum RemoteConnectionState
    {
        None = 0,
        Testing,
        Succeeded,
        Failed,
    }

    /// <summary>
    ///     "Test Connection" of the popups that take a remote URL (Clone, Add Remote, Edit Remote).
    ///     It checks the URL and SSH key as they are typed in the popup, before anything is saved.
    /// </summary>
    public class RemoteConnectionTest : ObservableObject
    {
        public RemoteConnectionState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        /// <summary>
        ///     The line of git's error output that tells why the connection failed.
        /// </summary>
        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        /// <summary>
        ///     Whole error output of the failed check, shown as a tooltip.
        /// </summary>
        public string ErrorDetails
        {
            get => _errorDetails;
            private set => SetProperty(ref _errorDetails, value);
        }

        /// <param name="getTarget">
        ///     Gives the working directory (may be null when there is no repository yet), the URL and
        ///     the SSH private key (null or empty when the default SSH settings are used) to test.
        /// </param>
        public RemoteConnectionTest(Func<(string WorkingDir, string Url, string SSHKey)> getTarget)
        {
            _getTarget = getTarget;
        }

        public async Task RunAsync()
        {
            var (workingDir, url, sshKey) = _getTarget();
            if (!Models.Remote.IsValidURL(url))
            {
                ShowError(App.Text("Remote.TestConnection.BadURL"), string.Empty);
                return;
            }

            _cts?.Cancel();

            var cts = new CancellationTokenSource();
            _cts = cts;
            State = RemoteConnectionState.Testing;

            var rs = await new Commands.TestRemoteConnection(workingDir, url, sshKey, cts.Token).GetResultAsync();

            // The check was restarted, the URL or key has changed, or the popup was closed meanwhile.
            var canceled = cts.IsCancellationRequested;
            if (_cts == cts)
                _cts = null;
            cts.Dispose();

            if (canceled)
                return;

            if (rs.IsSuccess)
                State = RemoteConnectionState.Succeeded;
            else
                ShowError(GetErrorSummary(rs.StdErr), rs.StdErr.Trim());
        }

        /// <summary>
        ///     Stops a running check and forgets the result. Called when the URL or key changes and when the
        ///     popup is closed.
        /// </summary>
        public void Reset()
        {
            _cts?.Cancel();
            _cts = null;
            State = RemoteConnectionState.None;
        }

        private void ShowError(string error, string details)
        {
            Error = error;
            ErrorDetails = string.IsNullOrEmpty(details) ? error : details;
            State = RemoteConnectionState.Failed;
        }

        // Git prints the reason first and its generic "fatal: Could not read from remote repository." after it.
        // ssh may put a notice about a newly known host before both of them.
        private static string GetErrorSummary(string stderr)
        {
            var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("Warning: Permanently added", StringComparison.Ordinal) ||
                    line.StartsWith("hint:", StringComparison.Ordinal))
                    continue;

                return line;
            }

            return App.Text("Remote.TestConnection.Failed");
        }

        private readonly Func<(string WorkingDir, string Url, string SSHKey)> _getTarget;
        private RemoteConnectionState _state = RemoteConnectionState.None;
        private string _error = string.Empty;
        private string _errorDetails = string.Empty;
        private CancellationTokenSource _cts = null;
    }
}
