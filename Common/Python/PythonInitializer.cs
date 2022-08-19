/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.IO;
using System.Linq;
using Python.Runtime;
using QuantConnect.Util;
using QuantConnect.Logging;
using System.Collections.Generic;

namespace QuantConnect.Python
{
    /// <summary>
    /// Helper class for Python initialization
    /// </summary>
    public static class PythonInitializer
    {
        private static string PathToVirtualEnv;

        // Used to allow multiple Python unit and regression tests to be run in the same test run
        private static bool _isInitialized;

        // Used to hold pending path additions before Initialize is called
        private static List<string> _pendingPathAdditions = new List<string>();

        /// <summary>
        /// Initialize python
        /// </summary>
        public static void Initialize()
        {
            if (!_isInitialized)
            {
                Log.Trace("PythonInitializer.Initialize(): start...");
                PythonEngine.Initialize();

                // required for multi-threading usage
                PythonEngine.BeginAllowThreads();

                _isInitialized = true;

                TryInitPythonVirtualEnvironment();
                Log.Trace("PythonInitializer.Initialize(): ended");

                AddPythonPaths(new []{ Environment.CurrentDirectory });
            }
        }

        /// <summary>
        /// Shutdown python
        /// </summary>
        public static void Shutdown()
        {
            if (_isInitialized)
            {
                Log.Trace("PythonInitializer.Shutdown(): start");
                _isInitialized = false;

                try
                {
                    PythonEngine.Shutdown();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                Log.Trace("PythonInitializer.Shutdown(): ended");
            }
        }

        /// <summary>
        /// Adds directories to the python path at runtime
        /// </summary>
        public static bool AddPythonPaths(IEnumerable<string> paths)
        {
            // Filter out any paths that are already on our Python path
            if (paths.IsNullOrEmpty())
            {
                return false;
            }

            // Add these paths to our pending additions
            _pendingPathAdditions.AddRange(paths);

            if (_isInitialized)
            {
                using (Py.GIL())
                {
                    using dynamic sys = Py.Import("sys");
                    using var locals = new PyDict();
                    locals.SetItem("sys", sys);

                    // Filter out any already paths that already exist on our current PythonPath
                    using var pythonCurrentPath = PythonEngine.Eval("sys.path", locals: locals);
                    var currentPath = pythonCurrentPath.As<List<string>>();
                    _pendingPathAdditions = _pendingPathAdditions.Where(x => !currentPath.Contains(x.Replace('\\', '/'))).ToList();

                    // Insert any pending path additions
                    if (!_pendingPathAdditions.IsNullOrEmpty())
                    {
                        var code = string.Join(";", _pendingPathAdditions
                            .Select(s => $"sys.path.insert(0, '{s}')")).Replace('\\', '/');
                        PythonEngine.Exec(code, locals: locals);

                        _pendingPathAdditions.Clear();
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// "Activate" a virtual Python environment by prepending its library storage to Pythons
        /// path. This allows the libraries in this venv to be selected prior to our base install.
        /// Requires PYTHONNET_PYDLL to be set to base install.
        /// </summary>
        /// <remarks>If a module is already loaded, Python will use its cached version first
        /// these modules must be reloaded by reload() from importlib library</remarks>
        public static bool ActivatePythonVirtualEnvironment(string pathToVirtualEnv)
        {
            if (string.IsNullOrEmpty(pathToVirtualEnv))
            {
                return false;
            }

            if(!Directory.Exists(pathToVirtualEnv))
            {
                Log.Error($"PythonIntializer.ActivatePythonVirtualEnvironment(): Path {pathToVirtualEnv} to virtual environment does not exist");
                return false;
            }

            PathToVirtualEnv = pathToVirtualEnv;

            bool? includeSystemPackages = null;
            var configFile = new FileInfo(Path.Combine(PathToVirtualEnv, "pyvenv.cfg"));
            if(configFile.Exists)
            {
                foreach (var line in File.ReadAllLines(configFile.FullName))
                {
                    if (line.Contains("include-system-site-packages", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // format: include-system-site-packages = false (or true)
                        var equalsIndex = line.IndexOf('=', StringComparison.InvariantCultureIgnoreCase);
                        if(equalsIndex != -1 && line.Length > (equalsIndex + 1) && bool.TryParse(line.Substring(equalsIndex + 1).Trim(), out var result))
                        {
                            includeSystemPackages = result;
                            break;
                        }
                    }
                }
            }

            if(!includeSystemPackages.HasValue)
            {
                includeSystemPackages = true;
                Log.Error($"PythonIntializer.ActivatePythonVirtualEnvironment(): failed to find system packages configuration. ConfigFile.Exits: {configFile.Exists}. Will default to true.");
            }
            else
            {
                Log.Trace($"PythonIntializer.ActivatePythonVirtualEnvironment(): will use system packages: {includeSystemPackages.Value}");
            }

            if (!includeSystemPackages.Value)
            {
                PythonEngine.SetNoSiteFlag();
            }

            TryInitPythonVirtualEnvironment();
            return true;
        }

        private static void TryInitPythonVirtualEnvironment()
        {
            if (!_isInitialized || string.IsNullOrEmpty(PathToVirtualEnv))
            {
                return;
            }

            using (Py.GIL())
            {
                using dynamic sys = Py.Import("sys");
                // fix the prefixes to point to our venv
                sys.prefix = PathToVirtualEnv;
                sys.exec_prefix = PathToVirtualEnv;

                using dynamic site = Py.Import("site");
                // This has to be overwritten because site module may already have been loaded by the interpreter (but not run yet)
                site.PREFIXES = new List<PyObject> { sys.prefix, sys.exec_prefix };
                // Run site path modification with tweaked prefixes
                site.main();

                if (Log.DebuggingEnabled)
                {
                    using dynamic os = Py.Import("os");
                    var path = new List<string>();
                    foreach (var p in sys.path)
                    {
                        path.Add((string)p);
                    }

                    Log.Debug($"PythonIntializer.InitPythonVirtualEnvironment(): PYTHONHOME: {os.getenv("PYTHONHOME")}." +
                        $" PYTHONPATH: {os.getenv("PYTHONPATH")}." +
                        $" sys.executable: {sys.executable}." +
                        $" sys.prefix: {sys.prefix}." +
                        $" sys.base_prefix: {sys.base_prefix}." +
                        $" sys.exec_prefix: {sys.exec_prefix}." +
                        $" sys.base_exec_prefix: {sys.base_exec_prefix}." +
                        $" sys.path: [{string.Join(",", path)}]");
                }
            }
        }
    }
}
