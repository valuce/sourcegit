using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     In-place updates from the GitHub releases of this fork (Windows only).
    ///
    ///     A release must carry a `sourcegit_{version}.{rid}.zip` asset (for example
    ///     `sourcegit_2026.23.2.win-x64.zip`) with the published app files, either at the
    ///     root of the archive or inside a single folder.
    ///
    ///     Windows does not allow overwriting a running executable or a loaded DLL, but it does
    ///     allow renaming them. So the update renames every file it replaces to `*.update-old`,
    ///     copies the new files in, restarts the app and removes the renamed files on the next start.
    /// </summary>
    public static class SelfUpdater
    {
        public const string Repository = "valuce/sourcegit";
        public const string ReleasesPage = $"https://github.com/{Repository}/releases/latest";

        /// <summary>
        ///     Fetches the latest release. The `SOURCEGIT_UPDATE_FEED` environment variable can point
        ///     to another URL returning the same JSON as the GitHub "latest release" API.
        /// </summary>
        public static async Task<Version> FetchLatestAsync()
        {
            var feed = Environment.GetEnvironmentVariable(FeedOverrideEnv);
            if (string.IsNullOrWhiteSpace(feed))
                feed = $"https://api.github.com/repos/{Repository}/releases/latest";

            using var client = CreateHttpClient(TimeSpan.FromSeconds(10));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var data = await client.GetStringAsync(feed).ConfigureAwait(false);
            return JsonSerializer.Deserialize(data, JsonCodeGen.Default.Version);
        }

        public static VersionAsset FindPackage(List<VersionAsset> assets)
        {
            if (assets == null || !OperatingSystem.IsWindows())
                return null;

            var rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.Arm64 => "win-arm64",
                _ => null,
            };

            if (rid == null)
                return null;

            var suffix = $".{rid}.zip";
            return assets.Find(x =>
                !string.IsNullOrEmpty(x.DownloadUrl) &&
                !string.IsNullOrEmpty(x.Name) &&
                x.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Downloads the package of the given release and puts its files in place of the
        ///     current ones. The running process keeps working; call <see cref="LaunchUpdatedApp"/>
        ///     and quit to switch to the new version.
        /// </summary>
        /// <param name="progress">Download progress in percent, or -1 while the files are being replaced.</param>
        public static async Task InstallAsync(Version ver, IProgress<double> progress, CancellationToken token)
        {
            var package = ver.Package ?? throw new InvalidOperationException("The release has no package for this platform.");
            var appDir = AppDirectory;
            EnsureWritable(appDir);

            var workDir = Path.Combine(Path.GetTempPath(), $"sourcegit-update-{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDir);

            try
            {
                var zip = Path.Combine(workDir, "package.zip");
                await DownloadAsync(package, zip, progress, token).ConfigureAwait(false);
                await VerifyDigestAsync(package, zip, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();
                progress?.Report(-1);

                await Task.Run(() =>
                {
                    var extractDir = Path.Combine(workDir, "files");
                    ZipFile.ExtractToDirectory(zip, extractDir);
                    ApplyPackage(extractDir, appDir);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(workDir, true);
                }
                catch
                {
                    // Leftovers in %TEMP% are harmless.
                }
            }
        }

        /// <summary>
        ///     Starts the (already replaced) executable. The new process waits until this one exits,
        ///     so the caller should quit right after this call.
        /// </summary>
        public static void LaunchUpdatedApp()
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                WorkingDirectory = AppDirectory,
            };

            start.Environment[WaitPidEnv] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            Process.Start(start);
        }

        /// <summary>
        ///     Must run at the very start of the process: waits for the previous instance when the app
        ///     was restarted by an update, then removes the files the update renamed.
        /// </summary>
        public static void FinishPendingUpdate()
        {
            var pidStr = Environment.GetEnvironmentVariable(WaitPidEnv);
            if (!string.IsNullOrEmpty(pidStr))
            {
                // Do not leak the variable to git or other child processes.
                Environment.SetEnvironmentVariable(WaitPidEnv, null);

                if (int.TryParse(pidStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    try
                    {
                        using var self = Process.GetCurrentProcess();
                        using var previous = Process.GetProcessById(pid);
                        if (previous.ProcessName.Equals(self.ProcessName, StringComparison.OrdinalIgnoreCase))
                            previous.WaitForExit(TimeSpan.FromSeconds(30));
                    }
                    catch
                    {
                        // The previous instance has already exited.
                    }
                }
            }

            try
            {
                var list = Path.Combine(AppDirectory, CleanupListFile);
                if (!File.Exists(list))
                    return;

                var remaining = new List<string>();
                foreach (var file in File.ReadAllLines(list))
                {
                    if (!file.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        if (File.Exists(file))
                            File.Delete(file);
                    }
                    catch
                    {
                        // Still in use (another instance of the old version is running). Retry next time.
                        remaining.Add(file);
                    }
                }

                if (remaining.Count == 0)
                    File.Delete(list);
                else
                    File.WriteAllLines(list, remaining);
            }
            catch
            {
                // Cleanup is best effort.
            }
        }

        private static string AppDirectory => Path.GetDirectoryName(Environment.ProcessPath)!;

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new System.Version(0, 0);
            var client = new HttpClient() { Timeout = timeout };

            // GitHub rejects API requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"SourceGit/{version}");
            return client;
        }

        private static void EnsureWritable(string dir)
        {
            var probe = Path.Combine(dir, $".update-write-test-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                throw new UnauthorizedAccessException($"Cannot write to '{dir}' ({e.Message}). Please download the update manually.");
            }
        }

        private static async Task DownloadAsync(VersionAsset package, string target, IProgress<double> progress, CancellationToken token)
        {
            using var client = CreateHttpClient(TimeSpan.FromSeconds(30));
            using var response = await client.GetAsync(package.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? package.Size;
            var received = 0L;
            var lastPercent = -1;
            var buffer = new byte[81920];

            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = File.Create(target))
            {
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    received += read;

                    if (total > 0)
                    {
                        var percent = (int)(received * 100 / total);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress?.Report(percent);
                        }
                    }
                }
            }

            if (total > 0 && received != total)
                throw new IOException($"Download is incomplete: received {received} of {total} bytes.");
        }

        private static async Task VerifyDigestAsync(VersionAsset package, string file, CancellationToken token)
        {
            // GitHub reports "sha256:<hex>" for release assets. Other feeds may omit it.
            const string prefix = "sha256:";
            if (string.IsNullOrEmpty(package.Digest) || !package.Digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;

            await using var stream = File.OpenRead(file);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
            if (!hash.Equals(package.Digest.Substring(prefix.Length), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded package is corrupted (SHA-256 mismatch).");
        }

        private static void ApplyPackage(string extractDir, string appDir)
        {
            var exeName = Path.GetFileName(Environment.ProcessPath)!;
            var exe = Directory.EnumerateFiles(extractDir, exeName, SearchOption.AllDirectories)
                .OrderBy(x => x.Length)
                .FirstOrDefault() ?? throw new InvalidDataException($"The package does not contain {exeName}.");

            var srcRoot = Path.GetDirectoryName(exe)!;
            var replaced = new List<(string File, string Backup)>();
            var created = new List<string>();

            try
            {
                foreach (var src in Directory.EnumerateFiles(srcRoot, "*", SearchOption.AllDirectories))
                {
                    var dst = Path.Combine(appDir, Path.GetRelativePath(srcRoot, src));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                    if (File.Exists(dst))
                    {
                        var backup = MakeBackupPath(dst);
                        File.Move(dst, backup);
                        replaced.Add((dst, backup));
                    }
                    else
                    {
                        created.Add(dst);
                    }

                    File.Copy(src, dst);
                }
            }
            catch
            {
                // Put the old version back so the app keeps working.
                foreach (var file in created)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                for (var i = replaced.Count - 1; i >= 0; i--)
                {
                    var (file, backup) = replaced[i];
                    try
                    {
                        if (File.Exists(file))
                            File.Delete(file);
                        File.Move(backup, file);
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                throw;
            }

            // The renamed files are still in use by this process. Remove them on the next start.
            if (replaced.Count > 0)
                File.AppendAllLines(Path.Combine(appDir, CleanupListFile), replaced.Select(x => x.Backup));
        }

        private static string MakeBackupPath(string file)
        {
            var backup = file + BackupSuffix;
            if (!File.Exists(backup))
                return backup;

            try
            {
                File.Delete(backup);
                return backup;
            }
            catch
            {
                // Locked leftover of a previous update that is still running somewhere.
                return $"{file}.{Guid.NewGuid():N}{BackupSuffix}";
            }
        }

        private const string FeedOverrideEnv = "SOURCEGIT_UPDATE_FEED";
        private const string WaitPidEnv = "SOURCEGIT_UPDATE_WAIT_PID";
        private const string BackupSuffix = ".update-old";
        private const string CleanupListFile = ".update-cleanup";
    }
}
