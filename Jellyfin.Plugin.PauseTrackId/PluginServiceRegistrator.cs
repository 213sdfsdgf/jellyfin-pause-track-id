using Jellyfin.Plugin.PauseTrackId.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.PauseTrackId;

/// <summary>
/// Registers services for the plugin.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<RecognitionResultStore>();
        serviceCollection.AddSingleton<ChromaprintRecognitionService>();
        serviceCollection.AddHostedService<PauseRecognitionHostedService>();
    }
}
