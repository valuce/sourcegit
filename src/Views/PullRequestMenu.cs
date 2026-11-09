using System;

using Avalonia.Controls;

namespace SourceGit.Views
{
    public static class PullRequestMenu
    {
        /// <summary>
        ///     Builds the "Create PR" context menu item for a branch. A local branch that has never been pushed,
        ///     or has commits its upstream doesn't have yet, gets "Push and Create PR" instead: it opens the Push
        ///     popup, which opens the create-PR page after a successful push.
        ///     Returns null when no remote can give a create-PR link.
        /// </summary>
        public static MenuItem CreatePullRequestMenuItem(this Control owner, ViewModels.Repository repo, Models.Branch branch)
        {
            if (branch == null || branch.IsDetachedHead)
                return null;

            if (!branch.IsLocal)
            {
                var remote = repo.Remotes.Find(x => x.Name.Equals(branch.Remote, StringComparison.Ordinal));
                if (remote == null || !remote.TryGetCreatePullRequestURL(out var url, branch.Name))
                    return null;

                return CreateItem(owner, App.Text("BranchCM.CreatePR"), () => Native.OS.OpenBrowser(url));
            }

            var upstream = string.IsNullOrEmpty(branch.Upstream) || branch.IsUpstreamGone
                ? null
                : repo.Branches.Find(x => x.FullName.Equals(branch.Upstream, StringComparison.Ordinal));

            if (upstream != null && branch.Ahead.Count == 0)
            {
                var remote = repo.Remotes.Find(x => x.Name.Equals(upstream.Remote, StringComparison.Ordinal));
                if (remote == null || !remote.TryGetCreatePullRequestURL(out var url, upstream.Name))
                    return null;

                return CreateItem(owner, App.Text("BranchCM.CreatePRForUpstream", upstream.FriendlyName), () => Native.OS.OpenBrowser(url));
            }

            if (!repo.Remotes.Exists(x => x.TryGetCreatePullRequestURL(out _, branch.Name)))
                return null;

            return CreateItem(owner, App.Text("BranchCM.PushAndCreatePR"), () =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Push(repo, branch, true));
            });
        }

        private static MenuItem CreateItem(Control owner, string header, Action onClick)
        {
            var item = new MenuItem();
            item.Header = header;
            item.Icon = owner.CreateMenuIcon("Icons.CreatePR");
            item.Click += (_, e) =>
            {
                onClick();
                e.Handled = true;
            };
            return item;
        }
    }
}
