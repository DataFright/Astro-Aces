using System;
using System.IO;
using UnityEngine;

namespace UTI
{
    /// <summary>
    /// Shared root for where UTI writes generated files (CSV logs, snapshot PNGs) by default.
    /// Deliberately not Application.persistentDataPath - that resolves to a hidden, per-user
    /// AppData folder (Windows: AppData/LocalLow/{Company}/{Product}) nowhere near the project a
    /// developer actually has open, which is exactly what made a real capture undiscoverable the
    /// first time someone who wasn't the one who wrote the path-resolution code went looking for
    /// it. ProjectRootDirectory resolves to the project root in the Editor (one level up from
    /// Assets/, so alongside Library/Logs/Temp) or the folder next to the built executable in a
    /// Player build - somewhere a dev browsing the project would actually see it. RootDirectory
    /// nests everything UTI generates one level further, in its own "UTI/" folder there, so all
    /// of a Bean's output (logs, snapshots, anything added later) lives in one place instead of
    /// being scattered as loose subfolders alongside the project's own Library/Logs/Temp.
    /// </summary>
    public static class BeanArtifactPaths
    {
        private const string UtiFolderName = "UTI";

        public static string ProjectRootDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string RootDirectory => Path.Combine(ProjectRootDirectory, UtiFolderName);

        public static string ResolveDefaultPath(string subfolder, string fileName) =>
            Path.Combine(RootDirectory, subfolder, fileName);

        /// <summary>
        /// A short random token to disambiguate two files that would otherwise resolve to the
        /// same default name in the same instant - e.g. two identically-named prefab clones
        /// (BeanLogger/BeanSnapshotExporter both use this to close DESIGN.md Sec 13/T13).
        /// Deliberately not GameObject.GetInstanceID() - reported as a hard compile error
        /// (CS0619, obsolete-as-error) against a build of this project, which couldn't be
        /// independently confirmed as a real Unity API change. A fresh GUID sidesteps needing to
        /// resolve that dispute at all, and gives a stronger uniqueness guarantee than an
        /// instance ID would anyway (no dependency on Unity's own object-identity internals).
        /// </summary>
        public static string NewUniqueToken() => Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
