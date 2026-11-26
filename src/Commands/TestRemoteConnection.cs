using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Checks that a remote URL can be reached with the current credentials (`git ls-remote <url> HEAD`).
    ///     Nothing is written to the repository, so the URL being edited can be tested before it is saved.
    /// </summary>
    public class TestRemoteConnection : Command
    {
        public TestRemoteConnection(string workingDir, string url, string sshKey, CancellationToken cancellationToken)
        {
            WorkingDirectory = workingDir;
            SSHKey = sshKey;
            Editor = EditorType.None;
            RaiseError = false;
            CancellationToken = cancellationToken;
            Args = $"ls-remote {url.Quoted()} HEAD";
        }

        public async Task<Result> GetResultAsync()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }

            // Git (and the ssh it runs) may wait for a host that never answers, so the check is stopped when
            // it is restarted or the popup is closed. Disposing the registration waits for a running callback.
            using var cancel = CancellationToken.Register(() =>
            {
                try
                {
                    if (!proc.HasExited)
                        Native.OS.TerminateProcess(proc);
                }
                catch
                {
                    // The process has exited already.
                }
            });

            var stdOutTask = proc.StandardOutput.ReadToEndAsync();
            var stdErrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync().ConfigureAwait(false);

            return new Result
            {
                IsSuccess = proc.ExitCode == 0 && !CancellationToken.IsCancellationRequested,
                StdOut = await stdOutTask.ConfigureAwait(false),
                StdErr = await stdErrTask.ConfigureAwait(false),
            };
        }
    }
}
