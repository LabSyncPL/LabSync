using LabSync.Core.Interfaces;
using System.Reflection;

namespace LabSync.Agent.Services
{
    public class ModuleLoader
    {
        private readonly ILogger<ModuleLoader> _logger;
        private readonly IServiceProvider      _serviceProvider; 
        private readonly List<IAgentModule>    _loadedModules = new();

        public ModuleLoader(ILogger<ModuleLoader> logger, IServiceProvider serviceProvider)
        {
            _logger          = logger;
            _serviceProvider = serviceProvider;
        }

        public void LoadPlugins()
        {
            string pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules");

            if (!Directory.Exists(pluginsPath))
            {
                _logger.LogWarning("Modules directory not found at: {Path}. Creating...", pluginsPath);
                Directory.CreateDirectory(pluginsPath);
                return;
            }

            var dllFiles = Directory.GetFiles(pluginsPath, "*.dll");
            _logger.LogInformation("Found {Count} DLLs in modules folder.", dllFiles.Length);

            foreach (var dll in dllFiles)
            {
                try
                {
                    LoadPluginFromDll(dll);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin: {File}", Path.GetFileName(dll));
                }
            }
        }

        private void LoadPluginFromDll(string path)
        {
            var assembly = Assembly.LoadFrom(path);
            var moduleTypes = assembly.GetTypes()
                .Where(t => typeof(IAgentModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in moduleTypes)
            {
                var module = (IAgentModule)Activator.CreateInstance(type)!;
                module.InitializeAsync(_serviceProvider).Wait();

                _loadedModules.Add(module);
                _logger.LogInformation("Plugin Loaded: {Name} v{Version}", module.Name, module.Version);
            }
        }

        public IAgentModule? FindModuleForJob(string jobType)
        {
            return _loadedModules.FirstOrDefault(m => m.CanHandle(jobType));
        }
    }
}