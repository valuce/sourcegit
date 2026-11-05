using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    public class VersionAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("digest")]
        public string Digest { get; set; }
    }

    public class Version
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("assets")]
        public List<VersionAsset> Assets { get; set; } = [];

        [JsonIgnore]
        public System.Version CurrentVersion { get; }

        [JsonIgnore]
        public string CurrentVersionStr => CurrentVersion.Build > 0
            ? $"v{CurrentVersion.Major}.{CurrentVersion.Minor:D2}.{CurrentVersion.Build}"
            : $"v{CurrentVersion.Major}.{CurrentVersion.Minor:D2}";

        [JsonIgnore]
        public bool IsNewVersion
        {
            get
            {
                if (string.IsNullOrEmpty(TagName) || !System.Version.TryParse(TagName.TrimStart('v', 'V'), out var latest))
                    return false;

                return CurrentVersion.CompareTo(latest) < 0;
            }
        }

        /// <summary>
        ///     Release asset that can be installed in place on this machine, or null when there is none.
        /// </summary>
        [JsonIgnore]
        public VersionAsset Package => SelfUpdater.FindPackage(Assets);

        [JsonIgnore]
        public bool CanInstallAutomatically => Package != null;

        [JsonIgnore]
        public string ReleaseDateStr => DateTimeFormat.Format(PublishedAt, true);

        public Version()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            CurrentVersion = assembly.Version ?? new System.Version();
        }
    }

    public class AlreadyUpToDate;

    public class SelfUpdateFailed
    {
        public string Reason
        {
            get;
            private set;
        }

        public SelfUpdateFailed(Exception e)
        {
            if (e.InnerException is { } inner)
                Reason = inner.Message;
            else
                Reason = e.Message;
        }
    }
}
