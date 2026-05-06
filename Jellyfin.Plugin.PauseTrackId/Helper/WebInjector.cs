using System.Runtime.Loader;
using Jellyfin.Plugin.PauseTrackId.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.PauseTrackId.Helper;

/// <summary>
/// Registers a safe web client loader through the File Transformation plugin.
/// </summary>
public static class WebInjector
{
    private const string TransformationId = "5fef7b20-2d69-4d79-b3d0-f968d5847f9f";
    private const string LoaderMarker = "pause-track-id-web-loader";
    private const string LoaderSnippet = "\n/* pause-track-id-web-loader */\n;(()=>{try{if(window.__pauseTrackIdLoaderInjected)return;window.__pauseTrackIdLoaderInjected=true;const src=window.ApiClient&&typeof window.ApiClient.getUrl===\"function\"?window.ApiClient.getUrl(\"PauseTrackId/Web/client.js\"):\"/PauseTrackId/Web/client.js\";if(document.querySelector('script[data-pause-track-id-web=\\\"true\\\"]'))return;const script=document.createElement(\"script\");script.src=src;script.defer=true;script.dataset.pauseTrackIdWeb=\"true\";document.head.appendChild(script);}catch(error){console.error(\"[pause-track-id] failed to inject web client\",error);}})();\n";

    private static bool _registered;
    private static readonly object SyncRoot = new();

    public static void TryRegister(PluginConfiguration config, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        if (!config.EnableWebButton)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_registered)
            {
                return;
            }

            var fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);

            if (fileTransformationAssembly is null)
            {
                logger.LogInformation("Pause Track ID web button is enabled, but the File Transformation plugin is not loaded. The fallback popup may still work.");
                return;
            }

            var pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            var registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation");
            if (registerMethod is null)
            {
                logger.LogWarning("Pause Track ID could not find File Transformation registration API.");
                return;
            }

            var payload = new JObject
            {
                ["id"] = TransformationId,
                ["fileNamePattern"] = "main.jellyfin.bundle.js",
                ["callbackAssembly"] = typeof(WebInjector).Assembly.FullName,
                ["callbackClass"] = typeof(WebInjector).FullName,
                ["callbackMethod"] = nameof(FileTransformer)
            };

            registerMethod.Invoke(null, [payload]);
            _registered = true;
            logger.LogInformation("Pause Track ID registered a namespaced Jellyfin web button injector.");
        }
    }

    public static string FileTransformer(PayloadRequest payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var contents = payload.Contents ?? string.Empty;
        if (string.IsNullOrEmpty(contents) || contents.Contains(LoaderMarker, StringComparison.Ordinal))
        {
            return contents;
        }

        return contents + LoaderSnippet;
    }
}
