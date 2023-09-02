using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public static class Initialize
    {
        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            Logger.ConfigureLogging(Serilog.Events.LogEventLevel.Warning, false);
            Log.Information("Starting Tests");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            Log.Information("Finishing Tests");
        }
    }
}