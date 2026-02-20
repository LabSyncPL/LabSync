using LabSync.Core.Interfaces;
using System.Reflection;
using System.Runtime.Loader;

namespace LabSync.Agent.Services
{
    /// <summary>
    /// Manages loading and lifecycle of agent plugin modules.
    /// Uses AssemblyLoadContext for proper plugin isolation and unloading.
    /// </summary>
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

        /// <summary>
        /// Gets all currently loaded modules.
        /// </summary>
        public IReadOnlyList<LoadedModule> LoadedModules => _loadedModules.AsReadOnly();

        /// <summary>
        /// Loads all plugin DLLs from the Modules directory.
        /// </summary>
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
                _logger.LogInformation("No modules found. Place plugin DLLs in: {Path}", pluginsPath);
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
            _logger.LogDebug("Attempting to load plugin: {File}", fileName);

            try
            {
                // Use AssemblyLoadContext for better plugin isolation
                // This allows for potential unloading in the future
                var loadContext = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(fileName)}", isCollectible: false);
                var assembly = loadContext.LoadFromAssemblyPath(path);

                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IAgentModule).IsAssignableFrom(t) 
                             && !t.IsInterface 
                             && !t.IsAbstract
                             && t.GetConstructor(Type.EmptyTypes) != null) // Must have parameterless constructor
                    .ToList();

                if (moduleTypes.Count == 0)
                {
                    _logger.LogWarning("No IAgentModule implementations found in {File}.", fileName);
                    loadContext.Unload();
                    return;
                }

                foreach (var type in moduleTypes)
                {
                    try
                    {
                        var module = (IAgentModule)Activator.CreateInstance(type)!;
                        
                        // Initialize the module
                        await module.InitializeAsync(_serviceProvider);

                        // Validate module properties
                        if (string.IsNullOrWhiteSpace(module.Name))
                        {
                            _logger.LogError("Module from {File} has empty Name. Skipping.", fileName);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(module.Version))
                        {
                            _logger.LogWarning("Module {Name} from {File} has empty Version.", module.Name, fileName);
                        }

                        // Check for duplicate module names
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

                        _logger.LogInformation("✓ Plugin Loaded: {Name} v{Version} from {File}", 
                            module.Name, module.Version, fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to instantiate module type {Type} from {File}.", 
                            type.Name, fileName);
                    }
                }
            }
            catch (BadImageFormatException ex)
            {
                _logger.LogError(ex, "Invalid assembly format for {File}. Ensure it's a valid .NET assembly.", fileName);
                throw;
            }
            catch (FileLoadException ex)
            {
                _logger.LogError(ex, "Failed to load assembly {File}. Check dependencies and file permissions.", fileName);
                throw;
            }
        }

        /// <summary>
        /// Finds a module that can handle the specified job type.
        /// </summary>
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

        /// <summary>
        /// Gets information about a loaded module.
        /// </summary>
        public LoadedModule? GetModuleInfo(string moduleName)
        {
            return _loadedModules.FirstOrDefault(m => 
                m.Module.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Represents a loaded module with its metadata.
    /// </summary>
    public class LoadedModule
    {
        public required IAgentModule Module { get; init; }
        public required string AssemblyPath { get; init; }
        public required AssemblyLoadContext LoadContext { get; init; }
        public DateTime LoadedAt { get; init; }
    }
}
