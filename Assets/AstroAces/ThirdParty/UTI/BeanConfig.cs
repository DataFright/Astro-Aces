using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UTI
{
    // A plain text file, not a Unity asset (ScriptableObject) - deliberately, so it lives in the
    // same project-root UTI/ folder as everything else UTI generates (logs, snapshots, the
    // end-user docs), rather than needing to live inside Assets/ the way a real Unity asset would
    // have to. Dev-editable by hand (Key=Value, one per line); UTI only ever reads it, never
    // rewrites an existing one.
    /// <summary>
    /// Project-wide default settings for newly-added Bean components, loaded from a plain text
    /// file at <project root>/UTI/BeanConfig.txt. See CONFIG.md.
    /// </summary>
    public class BeanConfig
    {
        private const string FileName = "BeanConfig.txt";

        public BeanCaptureMode DefaultCaptureMode = BeanCaptureMode.EveryUpdate;
        public float DefaultCaptureInterval = 0.5f;
        public BeanOutputTargets DefaultOutputTargets = BeanOutputTargets.Console;
        public BeanDimensionMode DefaultDimensionMode = BeanDimensionMode.Auto;
        // Scale-dependent - a fixed 2f world units was too tight for project 2's actual scale on
        // a near-stationary path (T23, TESTS/TestTracker.md), so it's now a per-project default
        // instead of a compiled-in constant.
        public float DefaultMinFramingRadius = 2f;

        public static string FilePath => Path.Combine(BeanArtifactPaths.RootDirectory, FileName);

        /// <summary>
        /// Loads BeanConfig.txt from the project's UTI/ folder, or returns null if it doesn't
        /// exist yet (nothing to apply). Also returns null (with a warning) if the file exists
        /// but can't actually be read - a locked or permission-denied file is a real system
        /// boundary that should degrade to "no config" the same way "no file" already does,
        /// rather than throwing out of Reset() and breaking the Editor's "add component" flow.
        /// </summary>
        public static BeanConfig Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return null;

            try
            {
                return ParseLines(File.ReadAllLines(path));
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Bean] Could not read {path}, falling back to compiled-in defaults: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses Key=Value lines into a BeanConfig - blank lines and lines starting with '#'
        /// are ignored (comments), as are malformed lines or unrecognized keys/values, so a
        /// dev's stray typo doesn't break loading the rest of the file. Pure function of the
        /// lines themselves, so it's testable without touching the real filesystem.
        /// </summary>
        public static BeanConfig ParseLines(IEnumerable<string> lines)
        {
            var config = new BeanConfig();

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                int separatorIndex = line.IndexOf('=');
                if (separatorIndex < 0)
                    continue;

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();

                switch (key)
                {
                    case "DefaultCaptureMode":
                        if (Enum.TryParse(value, out BeanCaptureMode captureMode))
                            config.DefaultCaptureMode = captureMode;
                        break;
                    case "DefaultCaptureInterval":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float interval))
                            config.DefaultCaptureInterval = interval;
                        break;
                    case "DefaultOutputTargets":
                        if (Enum.TryParse(value, out BeanOutputTargets outputTargets))
                            config.DefaultOutputTargets = outputTargets;
                        break;
                    case "DefaultDimensionMode":
                        if (Enum.TryParse(value, out BeanDimensionMode dimensionMode))
                            config.DefaultDimensionMode = dimensionMode;
                        break;
                    case "DefaultMinFramingRadius":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float minFramingRadius))
                            config.DefaultMinFramingRadius = minFramingRadius;
                        break;
                }
            }

            return config;
        }

        /// <summary>
        /// Writes a template BeanConfig.txt (commented, with the compiled-in defaults) to the
        /// project's UTI/ folder if one doesn't already exist - the one-click bootstrap for a dev
        /// who wants to start customizing. Never overwrites an existing file.
        /// </summary>
        public static void CreateTemplateIfMissing()
        {
            string path = FilePath;
            if (File.Exists(path))
                return;

            try
            {
                Directory.CreateDirectory(BeanArtifactPaths.RootDirectory);
                File.WriteAllText(path, TemplateContents);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Bean] Could not create {path}: {ex.Message}");
            }
        }

        public const string TemplateContents =
            "# UTI project config - see CONFIG.md for what each setting does.\n" +
            "# Applied to newly-added BeanTracker/BeanLogger/BeanSnapshotExporter components only\n" +
            "# (Unity's Reset() hook, when you add the component in the Editor) - not live\n" +
            "# at runtime, and never changes a Bean you've already configured.\n" +
            "\n" +
            "DefaultCaptureMode=EveryUpdate\n" +
            "DefaultCaptureInterval=0.5\n" +
            "DefaultOutputTargets=Console\n" +
            "DefaultDimensionMode=Auto\n" +
            "DefaultMinFramingRadius=2\n";

#if UNITY_EDITOR
        [UnityEditor.MenuItem("UTI/Create Bean Config")]
        private static void CreateBeanConfigMenuItem() => CreateTemplateIfMissing();

        // The standard one-click install step for a new project: config template plus the three
        // end-user docs, both landing in the same <project root>/UTI/ folder as everything else
        // UTI generates. Added because copying the docs by hand was a real, recurring gap - see
        // HANDOFF.md/DESIGN.md Sec 8.7's install-automation note - not a one-off fix.
        [UnityEditor.MenuItem("UTI/Setup Project (Config + Docs)")]
        private static void SetupProjectMenuItem()
        {
            CreateTemplateIfMissing();
            CopyEndUserDocsIfMissing();
        }

        private static readonly string[] EndUserDocFileNames =
        {
            "USAGE.md",
            "READING_LOGS_AND_VISUALS.md",
            "CONFIG.md"
        };

        /// <summary>
        /// Copies UTI's own end-user docs from the package's docs/ folder into
        /// <project root>/UTI/ - these are docs for whoever's using UTI in this game, not for
        /// developing UTI itself, so they belong next to the BeanLogs/BeanSnapshots/
        /// BeanConfig.txt a dev in this project is already looking at, not buried in the separate
        /// package repo. Resolves the package's real root via PackageInfo.FindForAssembly (works
        /// regardless of where/how the "file:" dependency resolves) rather than a hardcoded path.
        /// Never overwrites a file already present, matching CreateTemplateIfMissing's "don't
        /// clobber a dev's own copy" convention.
        /// </summary>
        private static void CopyEndUserDocsIfMissing()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BeanConfig).Assembly);
            if (packageInfo == null)
                return;

            try
            {
                Directory.CreateDirectory(BeanArtifactPaths.RootDirectory);

                foreach (string fileName in EndUserDocFileNames)
                {
                    // The docs live under the package's own docs/ folder (moved there 2026-08-08
                    // to keep the package root clean for the public repo) - only the source side
                    // needs that extra segment, since the destination in the consuming project
                    // still uses just the bare filename.
                    string source = Path.Combine(packageInfo.resolvedPath, "docs", fileName);
                    string destination = Path.Combine(BeanArtifactPaths.RootDirectory, fileName);
                    if (File.Exists(source) && !File.Exists(destination))
                        File.Copy(source, destination);
                }
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[Bean] Could not copy end-user docs into {BeanArtifactPaths.RootDirectory}: {ex.Message}");
            }
        }
#endif
    }
}
