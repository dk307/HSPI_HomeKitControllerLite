using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class HapAccessorySetup
    {
        [DataTestMethod]
        [DataRow("temperaturesensor")]
        [DataRow("ecobeethermostat")]
        public async Task CreatePairedData(string dirName)
        {
            using var accessory = new HapAccessory(dirName);
            await accessory.PairAndCreate(CancellationToken.None).ConfigureAwait(false);
        }
    }
}