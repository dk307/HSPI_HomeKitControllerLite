using Serilog;
using Serilog.Events;

#nullable enable

namespace Hspi
{
    /// <summary>
    /// Class for the main program.
    /// </summary>
    public static class Program
    {
        private static void Main(string[] args)
        {
            Logger.ConfigureLogging(LogEventLevel.Warning, false);
            Log.Information("Starting");
            try
            {
                using var plugin = new HSPI_HomeKitControllerLite.HSPI();
                plugin.Connect(args);
            }
            finally
            {
                Log.Information("Exiting");
                Log.CloseAndFlush();
            }
        }
    }
}