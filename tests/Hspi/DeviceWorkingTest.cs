using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using Hspi;
using Hspi.DeviceData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class DeviceWorkingTest
    {
        public DeviceWorkingTest()
        {
            cancellationTokenSource.CancelAfter(60 * 1000);
        }

        [TestMethod]
        public async Task ConnectionWorking()
        {
            using var hapAccessory =
                await TestHelper.CreateChangingTemperaturePairedAccessory(cancellationTokenSource.Token).ConfigureAwait(false);
            string hsData = hapAccessory.GetHsDeviceAndFeaturesString();

            Nito.AsyncEx.AsyncManualResetEvent asyncManualResetEvent = new(false);
            int count = 0;

            SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData = null;

            void updateValueCallback(int devOrFeatRef, EProperty property, object value)
            {
                // wait for temp changing 3 times
                if (deviceOrFeatureData.Keys.Last() == devOrFeatRef)
                {
                    if (count == 3)
                    {
                        asyncManualResetEvent.Set();
                    }
                    count++;
                }
            }

            TestHelper.SetupHsDataForSyncing(hsData,
                                             updateValueCallback,
                                             out Mock<PlugIn> plugIn,
                                             out Mock<IHsController> mockHsController,
                                             out deviceOrFeatureData);

            Assert.IsTrue(plugIn.Object.InitIO());

            await asyncManualResetEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            var refIds = deviceOrFeatureData.Keys.ToArray();
            var plugExtraData = ((PlugExtraData)deviceOrFeatureData[refIds[0]][EProperty.PlugExtraData]);
            Assert.IsFalse(plugExtraData[HsHomeKitDevice.FallbackAddressPlugExtraTag].Contains("0.0.0.0"));

            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task ChangeValueInAccessory()

        {
            using var hapAccessory = await TestHelper.CreateEcobeeThermostatPairedAccessory(CancellationToken.None).ConfigureAwait(false);
            string hsData = hapAccessory.GetHsDeviceAndFeaturesString();

            Nito.AsyncEx.AsyncManualResetEvent onlineEvent = new(false);
            Nito.AsyncEx.AsyncManualResetEvent targetTemperatureSetOnUpdate = new(false);

            int[] refIds = null;
            const int TargetTemperatureRefId = 9390;

            void updateValueCallback(int devOrFeatRef, EProperty property, object value)
            {
                if (refIds[1] == devOrFeatRef &&
                    property == EProperty.Value &&
                    (double)value == 1 &&
                    !onlineEvent.IsSet)
                {
                    onlineEvent.Set();
                }
                else if (TargetTemperatureRefId == devOrFeatRef &&
                         property == EProperty.Value &&
                         (double)value == 86D && 
                         !targetTemperatureSetOnUpdate.IsSet)
                {
                    targetTemperatureSetOnUpdate.Set();
                }
            }

            TestHelper.SetupHsDataForSyncing(hsData,
                                             updateValueCallback,
                                             out Mock<PlugIn> plugIn,
                                             out Mock<IHsController> mockHsController,
                                             out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            refIds = deviceOrFeatureData.Keys.ToArray();

            Assert.IsTrue(plugIn.Object.InitIO());
            await onlineEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            //online now

            // Target temperature
            ControlEvent controlEvent = new(TargetTemperatureRefId)
            {
                ControlValue = 86D
            };

            plugIn.Object.SetIOMulti(new List<ControlEvent> { controlEvent });
            await targetTemperatureSetOnUpdate.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            Assert.AreEqual(86D, deviceOrFeatureData[TargetTemperatureRefId][EProperty.Value]);

            plugIn.Object.ShutdownIO();
        }

        [TestMethod]
        public async Task ConnectionReconnect()
        {
            using var hapAccessory1 = await TestHelper.CreateTemperaturePairedAccessory(CancellationToken.None).ConfigureAwait(false);
            string hsData = hapAccessory1.GetHsDeviceAndFeaturesString();

            Nito.AsyncEx.AsyncManualResetEvent onlineEvent = new(false);
            Nito.AsyncEx.AsyncManualResetEvent onlineEvent2 = new(false);
            Nito.AsyncEx.AsyncManualResetEvent offlineEvent = new(false);

            int[] refIds = null;
            void updateValueCallback(int devOrFeatRef, EProperty property, object value)
            {
                // wait for connection changing 3 times
                if (refIds[1] == devOrFeatRef && property == EProperty.Value)
                {
                    if ((double)value == 1)
                    {
                        if (!onlineEvent.IsSet)
                        {
                            onlineEvent.Set();
                        }
                        else
                        {
                            onlineEvent2.Set();
                        }
                    }
                    else
                    {
                        offlineEvent.Set();
                    }
                }
            }

            TestHelper.SetupHsDataForSyncing(hsData,
                                             updateValueCallback,
                                             out Mock<PlugIn> plugIn,
                                             out Mock<IHsController> mockHsController,
                                             out SortedDictionary<int, Dictionary<EProperty, object>> deviceOrFeatureData);

            refIds = deviceOrFeatureData.Keys.ToArray();

            Assert.IsTrue(plugIn.Object.InitIO());

            await onlineEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            hapAccessory1.Dispose();

            await offlineEvent.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(0D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            //Restart accessory
            using var hapAccessory2 = await TestHelper.CreateTemperaturePairedAccessory(CancellationToken.None).ConfigureAwait(false);

            await onlineEvent2.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            Assert.AreEqual(1D, deviceOrFeatureData[refIds[1]][EProperty.Value]);

            plugIn.Object.ShutdownIO();
        }


        private readonly CancellationTokenSource cancellationTokenSource = new();
    }
}