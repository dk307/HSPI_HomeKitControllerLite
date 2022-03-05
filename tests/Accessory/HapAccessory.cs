using HomeKit;
using HomeKit.Model;
using HomeKit.Utils;
using HomeSeer.PluginSdk.Devices;
using Hspi.DeviceData;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{

    internal class HapAccessory : IDisposable
    {
        public HapAccessory(string dirName)
        {
            this.scriptFile = Path.Combine(GetWorkingDirectory(), dirName, dirName + ".py");
            this.accessoryFile = Path.Combine(GetWorkingDirectory(), dirName, "accessory.json");
            this.controllerFile = Path.Combine(GetWorkingDirectory(), dirName, "controller.json");
            this.defaultEnabledCharacteristics = Path.Combine(GetWorkingDirectory(), dirName, "enabledCharacteristics.json");
        }

        ~HapAccessory()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public PlugExtraData CreateDevicePlugExtraData()
        {
            var extraData = new PlugExtraData();
            extraData.AddNamed(HsHomeKitDevice.AidPlugExtraTag,
                               JsonConvert.SerializeObject(1UL));
            extraData.AddNamed(HsHomeKitDevice.EnabledCharacteristicPlugExtraTag,
                               JsonConvert.SerializeObject(GetEnabledCharacteristics()));
            extraData.AddNamed(HsHomeKitDevice.FallbackAddressPlugExtraTag,
                               JsonConvert.SerializeObject(new IPEndPoint(IPAddress.Any, 0), new IPEndPointJsonConverter()));
            extraData.AddNamed(HsHomeKitDevice.PairInfoPlugExtraTag,
                               JsonConvert.SerializeObject(GetAccessoryParingInfo()));

            return extraData;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public PairingDeviceInfo GetAccessoryParingInfo()
        {
            var controllerFileData = File.ReadAllText(this.controllerFile, Encoding.UTF8);
            var pairingInfo = JsonConvert.DeserializeObject<PairingDeviceInfo>(controllerFileData);
            return pairingInfo;
        }

        public IList<int> GetEnabledCharacteristics()
        {
            var controllerFileData = File.ReadAllText(this.defaultEnabledCharacteristics, Encoding.UTF8);
            var pairingInfo = JsonConvert.DeserializeObject<List<int>>(controllerFileData);
            return pairingInfo;
        }

        public async Task PairAndCreate(CancellationToken cancellationToken)
        {
            if (File.Exists(this.accessoryFile))
            {
                File.Delete(this.accessoryFile);
            }

            if (File.Exists(this.controllerFile))
            {
                File.Delete(this.controllerFile);
            }

            string pin = "233-34-235";
            scriptRunner = CreateUnPairedAccessory(scriptFile, pin, accessoryFile);
            await scriptRunner.WaitForSuccessStart(cancellationToken).ConfigureAwait(false);
            var discoveredDevices = await HomeKitDiscover.DiscoverIPs(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            if (!discoveredDevices.Any())
            {
                throw new InvalidProgramException("Nothing discovered");
            }
            var discoveredDevice = discoveredDevices[0];

            var pairing = await InsecureConnection.StartNewPairing(discoveredDevice,
                                                                   pin,
                                                                   cancellationToken);

            string workingDirectory = GetWorkingDirectory();

            var data = JsonConvert.SerializeObject(pairing, new IPEndPointJsonConverter());
            File.WriteAllText(Path.Combine(workingDirectory, "scripts", this.controllerFile), data);
        }

        public async Task StartPaired(CancellationToken cancellationToken)
        {
            string fileName2 = Path.ChangeExtension(accessoryFile, ".tmp");

            File.Copy(accessoryFile, fileName2, true);

            string args = $"111-11-1111 {fileName2}";
            scriptRunner = new PythonScriptWrapper(this.scriptFile, args);
            await scriptRunner.WaitForSuccessStart(cancellationToken).ConfigureAwait(false);
        }

        public async Task StartUnpaired(string pin, CancellationToken cancellationToken)
        {
            this.scriptRunner = CreateUnPairedAccessory(scriptFile, pin, null);
            await scriptRunner.WaitForSuccessStart(cancellationToken).ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.scriptRunner?.Dispose();
                }

                disposedValue = true;
            }
        }

        private static PythonScriptWrapper CreateUnPairedAccessory(string scriptName,
                                                                           string pin,
                                                                   string persistFile)
        {
            string fileName = persistFile ?? Guid.NewGuid().ToString("N") + ".obj";

            string args = $"{pin} {fileName}";
            var hapAccessory = new PythonScriptWrapper(scriptName, args);
            return hapAccessory;
        }

        private static string GetWorkingDirectory()
        {
            string codeBase = new Uri(typeof(PythonScriptWrapper).Assembly.CodeBase).LocalPath;
            string workingDirectory = Path.Combine(Path.GetDirectoryName(codeBase), "scripts");
            return workingDirectory;
        }

        protected readonly string accessoryFile;
        protected readonly string controllerFile;
        protected readonly string defaultEnabledCharacteristics;
        protected readonly string scriptFile;
        protected PythonScriptWrapper scriptRunner;
        private bool disposedValue;
    }
}