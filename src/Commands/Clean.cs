namespace SourceGit.Commands
{
    public class Clean : Command
    {
        public Clean(string repo, Models.CleanMode mode) : this(repo, mode, false, true)
        {
        }

        /// <param name="removeNestedRepositories">
        ///     Also remove untracked folders that are git repositories of their own (`-ff`). Registered submodules
        ///     are tracked, so they are never removed.
        /// </param>
        /// <param name="quiet">Do not report each removed path (`-q`).</param>
        public Clean(string repo, Models.CleanMode mode, bool removeNestedRepositories, bool quiet)
        {
            WorkingDirectory = repo;
            Context = repo;

            // For example `-qfd` (quiet), or `-ffd` (reports the removed paths, removes nested repositories too).
            var flags = (quiet ? "-q" : "-") + (removeNestedRepositories ? "ffd" : "fd");

            Args = mode switch
            {
                Models.CleanMode.OnlyUntrackedFiles => $"clean {flags}",
                Models.CleanMode.OnlyIgnoredFiles => $"clean {flags}X",
                _ => $"clean {flags}x",
            };
        }

        /// <summary>
        ///     Runs the same clean inside every checked out submodule, nested ones included, instead of the repository
        ///     itself (`git clean` never enters submodules).
        /// </summary>
        public Clean InSubmodules()
        {
            Args = $"submodule foreach --recursive git {Args}";
            return this;
        }
    }
}
