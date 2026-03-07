using LabSync.Core.Interfaces;
using System.Reflection;
using System.Runtime.Loader;

namespace LabSync.Agent.Services
{

    public class ModuleLoader
    {
        private readonly ILogger<ModuleLoader> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<LoadedModule> _loadedModules = new();

        public ModuleLoader(ILogger<ModuleLoader> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public IReadOnlyList<LoadedModule> LoadedModules => _loadedModules.AsReadOnly();

        public async Task LoadPluginsAsync()
        {
            string pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");

            if (!Directory.Exists(pluginsPath))
            {
                _logger.LogWarning("Modules directory not found at: {Path}. Creating...", pluginsPath);
                Directory.CreateDirectory(pluginsPath);
                return;
            }

            var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
            _logger.LogInformation("Found {Count} DLL(s) in modules folder.", dllFiles.Length);

            if (dllFiles.Length == 0)
            {
                _logger.LogInformation("No modules found.");
                return;
            }

            foreach (var dll in dllFiles)
            {
                try
                {
                    await LoadPluginFromDllAsync(dll);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin: {File}. Error: {Error}", 
                        Path.GetFileName(dll), ex.Message);
                }
            }

            _logger.LogInformation("Successfully loaded {Count} module(s).", _loadedModules.Count);
        }

        private async Task LoadPluginFromDllAsync(string path)
        {
            var fileName = Path.GetFileName(path);
            var loadContext = new PluginLoadContext(path);

            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(path);

                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IAgentModule).IsAssignableFrom(t) 
                             && !t.IsInterface 
                             && !t.IsAbstract
                             && t.GetConstructor(Type.EmptyTypes) != null) 
                    .ToList();

                if (moduleTypes.Count == 0)
                {
                    loadContext.Unload();
                    return;
                }

                foreach (var type in moduleTypes)
                {
                    try
                    {
                        var module = (IAgentModule)Activator.CreateInstance(type)!;
                        await module.InitializeAsync(_serviceProvider);

                        if (string.IsNullOrWhiteSpace(module.Name))
                        {
                            _logger.LogError("Module from {File} has empty Name. Skipping.", fileName);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(module.Version))
                        {
                            _logger.LogWarning("Module {Name} from {File} has empty Version.", module.Name, fileName);
                        }

                        if (_loadedModules.Any(m => m.Module.Name.Equals(module.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning("Module {Name} from {File} conflicts with already loaded module. Skipping.", 
                                module.Name, fileName);
                            continue;
                        }

                        _loadedModules.Add(new LoadedModule
                        {
                            Module = module,
                            AssemblyPath = path,
                            LoadContext = loadContext,
                            LoadedAt = DateTime.UtcNow
                        });

                        _logger.LogInformation("Plugin Loaded: {Name} v{Version} from {File}", 
                            module.Name, module.Version, fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to instantiate module type {Type} from {File}.", 
                            type.Name, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not inspect {File} as a plugin. ({Message})", fileName, ex.Message);
                loadContext.Unload();
            }
        }

        public IAgentModule? FindModuleForJob(string jobType)
        {
            if (string.IsNullOrWhiteSpace(jobType))
            {
                _logger.LogWarning("FindModuleForJob called with null or empty jobType.");
                return null;
            }

            var module = _loadedModules
                .Select(m => m.Module)
                .FirstOrDefault(m => m.CanHandle(jobType));

            if (module == null)
            {
                _logger.LogDebug("No module found that can handle job type: {JobType}. Available modules: {Modules}", 
                    jobType, string.Join(", ", _loadedModules.Select(m => m.Module.Name)));
            }

            return module;
        }

        public LoadedModule? GetModuleInfo(string moduleName)
        {
            return _loadedModules.FirstOrDefault(m => 
                m.Module.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
        }
    }


    internal class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var hostAssembly = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);

            if (hostAssembly != null)
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
    public class LoadedModule
    {
        public required IAgentModule Module { get; init; }
        public required string AssemblyPath { get; init; }
        public required AssemblyLoadContext LoadContext { get; init; }
        public DateTime LoadedAt { get; init; }
    }
}