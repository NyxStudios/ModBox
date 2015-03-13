﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Linq;
using Terraria;

namespace TerrariaApi.Server
{
	// TODO: Maybe re-implement a reload functionality for plugins, but you'll have to load all assemblies into their own
	// AppDomain in order to unload them again later. Beware that having them in their own AppDomain might cause threading 
	// problems as usual locks will only work in their own AppDomains.
	public static class ServerApi
	{
		public const string PluginsPath = "ServerPlugins";

		public static readonly Version ApiVersion = new Version(1, 17, 0, 0);
		private static Main game;
		private static readonly Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();
		private static readonly List<PluginContainer> plugins = new List<PluginContainer>();
	
		public static bool IgnoreVersion
		{
			get;
			set;
		}
		public static string ServerPluginsDirectoryPath
		{
			get;
			private set;
		}
		public static ReadOnlyCollection<PluginContainer> Plugins
		{
			get { return new ReadOnlyCollection<PluginContainer>(plugins); }
		}
		public static HookManager Hooks
		{
			get; 
			private set;
		}
		public static LogWriterManager LogWriter
		{
			get;
			private set;
		}
		public static ProfilerManager Profiler
		{
			get; 
			private set;
		}
		public static bool IsWorldRunning
		{
			get; 
			internal set;
		}
		public static bool RunningMono { get; private set; }
		public static bool ForceUpdate { get; private set; }
        public static bool UseAsyncSocketsInMono { get; private set; }

		static ServerApi()
		{
			Hooks = new HookManager();
			LogWriter = new LogWriterManager();
			Profiler = new ProfilerManager();

            UseAsyncSocketsInMono = false;
		    ForceUpdate = false;
			Type t = Type.GetType("Mono.Runtime");
			RunningMono = (t != null);
		}

		internal static void Initialize(string[] commandLineArgs, Main game)
		{

			Profiler.BeginMeasureServerInitTime();
			ServerApi.LogWriter.ServerWriteLine(
				string.Format("TerrariaApi - Server v{0} started.", ApiVersion), TraceLevel.Verbose);
			ServerApi.LogWriter.ServerWriteLine(
				"\tCommand line: " + Environment.CommandLine, TraceLevel.Verbose);
			ServerApi.LogWriter.ServerWriteLine(
				string.Format("\tOS: {0} (64bit: {1})", Environment.OSVersion, Environment.Is64BitOperatingSystem), TraceLevel.Verbose);
			ServerApi.LogWriter.ServerWriteLine(
				"\tMono: " + RunningMono, TraceLevel.Verbose);

			ServerApi.game = game;
			HandleCommandLine(commandLineArgs);

			ServerPluginsDirectoryPath = Path.Combine(Environment.CurrentDirectory, PluginsPath);

			if (!Directory.Exists(ServerPluginsDirectoryPath))
			{
				string lcDirectoryPath = 
					Path.Combine(Path.GetDirectoryName(ServerPluginsDirectoryPath), PluginsPath.ToLower());

				if (Directory.Exists(lcDirectoryPath))
				{
					Directory.Move(lcDirectoryPath, ServerPluginsDirectoryPath);
					LogWriter.ServerWriteLine("Case sensitive filesystem detected, serverplugins directory has been renamed.", TraceLevel.Warning);
				}
				else
				{
					Directory.CreateDirectory(ServerPluginsDirectoryPath);
				}
			}

			// Add assembly resolver instructing it to use the server plugins directory as a search path.
			// TODO: Either adding the server plugins directory to PATH or as a privatePath node in the assembly config should do too.
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			LoadPlugins();
		}

		internal static void DeInitialize()
		{
			UnloadPlugins();
			Profiler.Deatch();
			LogWriter.Deatch();
		}

		internal static void HandleCommandLine(string[] commandLineArgs)
		{
			for (int i = 0; i < commandLineArgs.Length; i++)
			{
				switch (commandLineArgs[i].ToLower())
				{
					case "-config":
					{
						string filePath = commandLineArgs[++i];
						LogWriter.ServerWriteLine(string.Format("Loading dedicated config file: {0}", filePath), TraceLevel.Verbose);
						game.LoadDedConfig(filePath);

						break;
					}
					case "-port":
					{
						int serverPort;
						if (int.TryParse(commandLineArgs[++i], out serverPort))
						{
							Netplay.serverPort = serverPort;
							LogWriter.ServerWriteLine(string.Format("Listening on port {0}.", serverPort), TraceLevel.Verbose);
						}
						else
						{
							// The server should not start up if this argument is invalid.
							throw new InvalidOperationException("Invalid value given for command line argument \"-ip\".");
						}

						break;
					}
					case "-world":
					{
						string worldPath = commandLineArgs[++i];
						game.SetWorld(worldPath);
						LogWriter.ServerWriteLine(string.Format("World set for auto loading: {0}", worldPath), TraceLevel.Verbose);

						break;
					}
					case "-worldname":
					{
						string worldName = commandLineArgs[++i];
						game.SetWorldName(worldName);
						LogWriter.ServerWriteLine(string.Format("World name will be overridden by: {0}", worldName), TraceLevel.Verbose);

						break;
					}
					case "-autoshutdown":
					{
						game.autoShut();
						break;
					}
					case "-autocreate":
					{
						string newOpt = commandLineArgs[++i];
						game.autoCreate(newOpt);
						break;
					}
					case "-ip":
					{
						IPAddress ip;
						if (IPAddress.TryParse(commandLineArgs[++i], out ip))
						{
							Netplay.serverListenIP = ip;
							LogWriter.ServerWriteLine(string.Format("Listening on IP {0}.", ip), TraceLevel.Verbose);
						}
						else
						{
							// The server should not start up if this argument is invalid.
							throw new InvalidOperationException("Invalid value given for command line argument \"-ip\".");
						}

						break;
					}
					case "-connperip":
					{
						int limit;
						if (int.TryParse(commandLineArgs[++i], out limit))
						{
							Netplay.connectionLimit = limit;
							LogWriter.ServerWriteLine(string.Format(
								"Connections per IP have been limited to {0} connections.", limit), TraceLevel.Verbose);
						}
						else
							LogWriter.ServerWriteLine("Invalid value given for command line argument \"-connperip\".", TraceLevel.Warning);

						break;
					}
					case "-killinactivesocket":
					{
						Netplay.killInactive = true;
						break;
					}
					case "-lang":
					{
						int langIndex;
						if (int.TryParse(commandLineArgs[++i], out langIndex))
						{
							Lang.lang = langIndex;
							LogWriter.ServerWriteLine(string.Format("Language index set to {0}.", langIndex), TraceLevel.Verbose);
						}
						else
							LogWriter.ServerWriteLine("Invalid value given for command line argument \"-lang\".", TraceLevel.Warning);

						break;
					}
					case "-ignoreversion":
					{
						IgnoreVersion = true;
						LogWriter.ServerWriteLine(
							"Plugin versions are no longer being regarded, you are on your own! If problems arise, TShock developers will not help you with issues regarding this.", 
							TraceLevel.Warning);

						break;
					}
					case "-forceupdate":
					{
						ForceUpdate = true;
						LogWriter.ServerWriteLine(
							"Forcing game updates regardless of players! This is experimental, and will cause constant CPU usage, you are on your own.",
							TraceLevel.Warning);

						break;
					}
                    case "-asyncmono":
				    {
				        UseAsyncSocketsInMono = true;
                        LogWriter.ServerWriteLine(
                            "Forcing Mono to use asynchronous sockets.  This is highly experimental and may not work on all versions of Mono.",
                            TraceLevel.Warning);
				        break;
				    }
				}
			}
		}

		internal static void LoadPlugins()
		{
			string ignoredPluginsFilePath = Path.Combine(ServerPluginsDirectoryPath, "ignoredplugins.txt");

			List<string> ignoredFiles = new List<string>();
			if (File.Exists(ignoredPluginsFilePath))
				ignoredFiles.AddRange(File.ReadAllLines(ignoredPluginsFilePath));

			List<FileInfo> fileInfos = new DirectoryInfo(ServerPluginsDirectoryPath).GetFiles("*.dll").ToList();
			fileInfos.AddRange(new DirectoryInfo(ServerPluginsDirectoryPath).GetFiles("*.dll-plugin"));

			Dictionary<TerrariaPlugin,Stopwatch> pluginInitWatches = new Dictionary<TerrariaPlugin,Stopwatch>();
			foreach (FileInfo fileInfo in fileInfos)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo.Name);
				if (ignoredFiles.Contains(fileNameWithoutExtension))
				{
					LogWriter.ServerWriteLine(
						string.Format("{0} was ignored from being loaded.", fileNameWithoutExtension), TraceLevel.Verbose);

					continue;
				}

				try
				{
					Assembly assembly;
					// The plugin assembly might have been resolved by another plugin assembly already, so no use to
					// load it again, but we do still have to verify it and create plugin instances.
					if (!loadedAssemblies.TryGetValue(fileNameWithoutExtension, out assembly)) {
						assembly = Assembly.Load(File.ReadAllBytes(fileInfo.FullName));
						loadedAssemblies.Add(fileNameWithoutExtension, assembly);
					}

					if (!InvalidateAssembly(assembly, fileInfo.Name))
						continue;

					foreach (Type type in assembly.GetExportedTypes())
					{
						if (!type.IsSubclassOf(typeof (TerrariaPlugin)) || !type.IsPublic || type.IsAbstract)
							continue;
						object[] customAttributes = type.GetCustomAttributes(typeof (ApiVersionAttribute), false);
						if (customAttributes.Length == 0)
							continue;

						if (!IgnoreVersion)
						{
							var apiVersionAttribute = (ApiVersionAttribute)customAttributes[0];
							Version apiVersion = apiVersionAttribute.ApiVersion;
							if (apiVersion.Major != ApiVersion.Major || apiVersion.Minor != ApiVersion.Minor)
							{
								LogWriter.ServerWriteLine(
									string.Format("Plugin \"{0}\" is designed for a different Server API version ({1}) and was ignored.", 
									type.FullName, apiVersion.ToString(2)), TraceLevel.Warning);

								continue;
							}
						}

						TerrariaPlugin pluginInstance;
						try
						{
							Stopwatch initTimeWatch = new Stopwatch();
							initTimeWatch.Start();

							pluginInstance = (TerrariaPlugin)Activator.CreateInstance(type, game);
						
							initTimeWatch.Stop();
							pluginInitWatches.Add(pluginInstance, initTimeWatch);
						}
						catch (Exception ex)
						{
							// Broken plugins better stop the entire server init.
							throw new InvalidOperationException(
								string.Format("Could not create an instance of plugin class \"{0}\".", type.FullName), ex);
						}
						plugins.Add(new PluginContainer(pluginInstance));
					}
				}
				catch (Exception ex)
				{
					// Broken assemblies / plugins better stop the entire server init.
					throw new InvalidOperationException(
						string.Format("Failed to load assembly \"{0}\".", fileInfo.Name), ex);
				}
			}
			IOrderedEnumerable<PluginContainer> orderedPluginSelector =
				from x in Plugins
				orderby x.Plugin.Order, x.Plugin.Name
				select x;

			foreach (PluginContainer current in orderedPluginSelector)
			{
				Stopwatch initTimeWatch = pluginInitWatches[current.Plugin];
				initTimeWatch.Start();

				try
				{
					current.Initialize();
				}
				catch (Exception ex)
				{
					// Broken plugins better stop the entire server init.
					throw new InvalidOperationException(string.Format(
						"Plugin \"{0}\" has thrown an exception during initialization.", current.Plugin.Name), ex);
				}

				initTimeWatch.Stop();
				LogWriter.ServerWriteLine(string.Format(
					"Plugin {0} v{1} (by {2}) initiated.", current.Plugin.Name, current.Plugin.Version, current.Plugin.Author), 
					TraceLevel.Info);
			}

			if (Profiler.WrappedProfiler != null)
			{
				foreach (var pluginWatchPair in pluginInitWatches)
				{
					TerrariaPlugin plugin = pluginWatchPair.Key;
					Stopwatch initTimeWatch = pluginWatchPair.Value;

					Profiler.InputPluginInitTime(plugin, initTimeWatch.Elapsed);
				}
			}
		}

		internal static void UnloadPlugins()
		{
			var pluginUnloadWatches = new Dictionary<PluginContainer, Stopwatch>();
			foreach (PluginContainer pluginContainer in plugins)
			{
				Stopwatch unloadWatch = new Stopwatch();
				unloadWatch.Start();

				try
				{
					pluginContainer.DeInitialize();
				}
				catch (Exception ex)
				{
					LogWriter.ServerWriteLine(string.Format(
						"Plugin \"{0}\" has thrown an exception while being deinitialized:\n{1}", pluginContainer.Plugin.Name, ex), 
						TraceLevel.Error);
				}

				unloadWatch.Stop();
				pluginUnloadWatches.Add(pluginContainer, unloadWatch);
			}

			foreach (PluginContainer pluginContainer in plugins)
			{
				Stopwatch unloadWatch = pluginUnloadWatches[pluginContainer];
				unloadWatch.Start();

				try
				{
					pluginContainer.Dispose();
				}
				catch (Exception ex)
				{
					LogWriter.ServerWriteLine(string.Format(
						"Plugin \"{0}\" has thrown an exception while being disposed:\n{1}", pluginContainer.Plugin.Name, ex), 
						TraceLevel.Error);
				}

				unloadWatch.Stop();
				Profiler.InputPluginUnloadTime(pluginContainer.Plugin, unloadWatch.Elapsed);
			}
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			string fileName = args.Name.Split(',')[0];
			string path = Path.Combine(ServerPluginsDirectoryPath, fileName + ".dll");
			try
			{
				if (File.Exists(path))
				{
					Assembly assembly;
					if (!loadedAssemblies.TryGetValue(fileName, out assembly))
					{
						assembly = Assembly.Load(File.ReadAllBytes(path));
						// We just do this to return a proper error message incase this is a resolved plugin assembly 
						// referencing an old TerrariaServer version.
						if (!InvalidateAssembly(assembly, fileName))
							throw new InvalidOperationException(
								"The assembly is referencing a version of TerrariaServer prior 1.14.");

						loadedAssemblies.Add(fileName, assembly);
					}
					return assembly;
				}
			}
			catch (Exception ex)
			{
				LogWriter.ServerWriteLine(
					string.Format("Error on resolving assembly \"{0}.dll\":\n{1}", fileName, ex),
					TraceLevel.Error);
			}
			return null;
		}

		// Many types have changed with 1.14 and thus we won't even be able to check the ApiVersionAttribute of 
		// plugin classes of assemblies targeting a TerrariaServer prior 1.14 as they can not be loaded at all.
		// We work around this by checking the referenced assemblies, if we notice a reference to the old 
		// TerrariaServer assembly, we expect the plugin assembly to be outdated.
		private static bool InvalidateAssembly(Assembly assembly, string fileName) {
			AssemblyName[] referencedAssemblies = assembly.GetReferencedAssemblies();
			AssemblyName terrariaServerReference = referencedAssemblies.FirstOrDefault(an => an.Name == "TerrariaServer");
			if (terrariaServerReference != null && terrariaServerReference.Version == new Version(0, 0, 0, 0))
			{
				LogWriter.ServerWriteLine(
					string.Format("Plugin assembly \"{0}\" was compiled for a Server API version prior 1.14 and was ignored.", 
					fileName), TraceLevel.Warning);

				return false;
			}

			return true;
		}
	}
}
