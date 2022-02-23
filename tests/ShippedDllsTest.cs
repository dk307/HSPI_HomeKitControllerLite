using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class ShippedDllsTest
    {
        [TestMethod]
        public void InstallFileHasAllDlls()
        {
            string path = GetInstallFilePath();
            var dllFilesPaths = Directory.GetFiles(path, "*.dll");
            Assert.AreNotEqual(0, dllFilesPaths.Length);

            var dllFiles = dllFilesPaths.Select(x => Path.GetFileName(x)).ToList();

            // files not shipped
            dllFiles.Remove("HSCF.dll");
            dllFiles.Remove("PluginSdk.dll");
            dllFiles.Remove("HomeSeerAPI.dll");

            // Parse shipped dlls
            var installDlls = File.ReadLines(Path.Combine(path, "DllsToShip.txt")).ToList();

            CollectionAssert.AreEquivalent(installDlls, dllFiles, "Dlls in output is not same as shipped dlls");
        }

        private static string GetInstallFilePath()
        {
            string dllPath = Assembly.GetExecutingAssembly().Location;
            var parentDirectory = new DirectoryInfo(Path.GetDirectoryName(dllPath));
            return Path.Combine(parentDirectory.Parent.Parent.Parent.Parent.FullName, "plugin", "bin", "x86", "Debug");
        }
    }
}