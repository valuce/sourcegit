using System;
using System.Collections.Generic;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     A run of adjacent tabs, Chrome-style: the first tab (the root) is a repository and every tab
    ///     right after it is a repository located inside the root's working tree (its submodules, their
    ///     submodules, nested repositories). Groups are derived from the tab order, so they need no
    ///     storage and survive restarts: a tab dragged away from its root leaves the group, a tab dragged
    ///     back joins it again.
    /// </summary>
    public class LauncherTabGroup
    {
        public static readonly StringComparison PathComparison =
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        public LauncherPage Root { get; }

        /// <summary>Index of the root tab in <see cref="Launcher.Pages"/>.</summary>
        public int Start { get; }

        /// <summary>Index of the last tab of the group (inclusive).</summary>
        public int End { get; }

        public int Count => End - Start + 1;

        /// <summary>
        ///     Index into <see cref="Models.Bookmarks.Brushes"/>. The root repository's bookmark wins, so the
        ///     color can be changed from the tab's Edit dialog; otherwise a stable color is picked from the
        ///     root's path.
        /// </summary>
        public int ColorIndex
        {
            get
            {
                var count = Models.Bookmarks.Brushes.Length;
                var bookmark = Root.Node.Bookmark;
                if (bookmark > 0 && bookmark < count)
                    return bookmark;

                return 1 + (int)(StableHash(Root.Node.Id) % (uint)(count - 1));
            }
        }

        public LauncherTabGroup(LauncherPage root, int start, int end)
        {
            Root = root;
            Start = start;
            End = end;
        }

        public bool Contains(int index)
        {
            return index >= Start && index <= End;
        }

        public static List<LauncherTabGroup> Collect(IList<LauncherPage> pages)
        {
            var groups = new List<LauncherTabGroup>();
            var i = 0;
            while (i < pages.Count)
            {
                var root = pages[i];
                var end = i;
                while (end + 1 < pages.Count && IsInside(pages[end + 1].Node, root.Node))
                    end++;

                if (end > i)
                    groups.Add(new LauncherTabGroup(root, i, end));

                i = end + 1;
            }

            return groups;
        }

        public static LauncherTabGroup Find(IList<LauncherPage> pages, LauncherPage page)
        {
            var index = pages.IndexOf(page);
            if (index < 0)
                return null;

            foreach (var group in Collect(pages))
            {
                if (group.Contains(index))
                    return group;
            }

            return null;
        }

        /// <summary>
        ///     New tab order for dropping <paramref name="from"/> onto <paramref name="to"/>, or null when nothing moves.
        ///     Dragging a group's root tab drags the whole group, and a tab dropped onto another group lands before or
        ///     after it instead of splitting it. Moving a tab inside its own group keeps it there; moving it out of the
        ///     group's range takes it out of the group.
        /// </summary>
        public static List<LauncherPage> ReorderForMove(IList<LauncherPage> pages, LauncherPage from, LauncherPage to)
        {
            var fromIdx = pages.IndexOf(from);
            var toIdx = pages.IndexOf(to);
            if (fromIdx < 0 || toIdx < 0 || fromIdx == toIdx)
                return null;

            var groups = Collect(pages);
            var fromGroup = groups.Find(g => g.Contains(fromIdx));
            var toGroup = groups.Find(g => g.Contains(toIdx));

            var blockStart = fromIdx;
            var blockEnd = fromIdx;
            if (fromGroup != null && fromGroup.Start == fromIdx)
            {
                blockStart = fromGroup.Start;
                blockEnd = fromGroup.End;
            }

            if (toIdx >= blockStart && toIdx <= blockEnd)
                return null;

            if (toGroup != null && toGroup != fromGroup)
                toIdx = toIdx > fromIdx ? toGroup.End : toGroup.Start;

            var order = new List<LauncherPage>(pages);
            var block = order.GetRange(blockStart, blockEnd - blockStart + 1);
            order.RemoveRange(blockStart, block.Count);
            order.InsertRange(toIdx > blockEnd ? toIdx - block.Count + 1 : toIdx, block);
            return order;
        }

        /// <summary>
        ///     New tab order that keeps a freshly opened repository in the group it belongs to: it goes right after the
        ///     tabs of the repository it lives in, and already open tabs of repositories inside it (its submodules)
        ///     follow it. A repository with no open parent keeps its position.
        /// </summary>
        public static List<LauncherPage> ReorderForOpen(IList<LauncherPage> pages, LauncherPage opened)
        {
            var originalIdx = pages.IndexOf(opened);
            if (originalIdx < 0 || !opened.Node.IsRepository)
                return null;

            var children = new List<LauncherPage>();
            var order = new List<LauncherPage>();
            var insertAt = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                if (p == opened)
                    continue;

                if (IsInside(p.Node, opened.Node))
                {
                    children.Add(p);
                    continue;
                }

                order.Add(p);
                if (i < originalIdx)
                    insertAt = order.Count;
            }

            LauncherPage parent = null;
            foreach (var p in order)
            {
                if (IsInside(opened.Node, p.Node) && (parent == null || p.Node.Id.Length > parent.Node.Id.Length))
                    parent = p;
            }

            if (parent != null)
            {
                var end = order.IndexOf(parent);
                while (end + 1 < order.Count && IsInside(order[end + 1].Node, parent.Node))
                    end++;

                insertAt = end + 1;
            }

            order.Insert(insertAt, opened);
            order.InsertRange(insertAt + 1, children);
            return order;
        }

        /// <summary>True when <paramref name="child"/> is a repository inside <paramref name="parent"/>'s working tree.</summary>
        public static bool IsInside(RepositoryNode child, RepositoryNode parent)
        {
            if (child == null || parent == null || !child.IsRepository || !parent.IsRepository)
                return false;

            var p = parent.Id;
            var c = child.Id;
            return p.Length > 0 &&
                c.Length > p.Length + 1 &&
                c[p.Length] == '/' &&
                c.StartsWith(p, PathComparison);
        }

        // string.GetHashCode() is randomized per process, so it would repaint groups on every launch.
        private static uint StableHash(string value)
        {
            var hash = 2166136261u;
            foreach (var ch in value)
            {
                hash ^= char.ToLowerInvariant(ch);
                hash *= 16777619u;
            }

            return hash;
        }
    }
}
