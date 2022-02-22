using Hspi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class PlugInTest
    {
        [TestMethod]
        public void VerifyNameAndId()
        {
            var plugin = new PlugIn();
            Assert.AreEqual(plugin.Id, PlugInData.PlugInId);
            Assert.AreEqual(plugin.Name, PlugInData.PlugInName);
        }

        [TestMethod]
        public void InitFirstTime()
        {
            var plugin = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugin, new Dictionary<string, string>());
            Assert.IsTrue(plugin.Object.InitIO());
            plugin.Object.ShutdownIO();
        }

        [TestMethod]
        public void AddDeviceTestWithNoAccessory()
        {
            var plugIn = TestHelper.CreatePlugInMock();
            TestHelper.SetupHsControllerAndSettings(plugIn, new Dictionary<string, string>());
            Assert.IsTrue(plugIn.Object.InitIO());

            string data = plugIn.Object.PostBackProc("AddDevice.html", "{\"action\":\"search\"}", string.Empty, 0);
            Assert.AreEqual("{\"ErrorMessage\":null,\"Data\":[]}", data);
            plugIn.Object.ShutdownIO();
        }
    }
}