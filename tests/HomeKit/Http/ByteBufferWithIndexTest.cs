using HomeKit.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    [TestClass]
    public class ByteBufferWithIndexTest
    {
        [TestMethod]
        public void Initialize()
        {
            var data = new ByteBufferWithIndex(635);
            Assert.AreEqual(0, data.Length);
            Assert.AreEqual(0, data.AsSpan().Length);
            Assert.AreEqual(0, data.AsMemory().Length);
        }

        [TestMethod]
        public void AddData()
        {
            var data = new ByteBufferWithIndex(512);
            data.AddToBack(new byte[1024]);
            Assert.AreEqual(1024, data.Length);
            Assert.AreEqual(1024, data.AsSpan().Length);
            Assert.AreEqual(1024, data.AsMemory().Length);
        }

        [TestMethod]
        public void RemoveData()
        {
            var data = new ByteBufferWithIndex(512);
            byte[] addedData = new byte[] { 2, 3, 4, 5, 34, 34, 23, 12 };
            data.AddToBack(addedData);
            Assert.AreEqual(addedData.Length, data.Length);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => data.RemoveFromFront(1024));
            var returnData = data.RemoveFromFront(5);

            Assert.AreEqual(addedData.Length - 5, data.Length);

            CollectionAssert.AreEqual(addedData.AsSpan(0, 5).ToArray(), returnData);
        }

        [TestMethod]
        public async Task ReadFromStream()
        {
            var data = new ByteBufferWithIndex(5);

            var stream = new MemoryStream(new byte[10]);
            await data.ReadFromStream(stream, 10, CancellationToken.None);
            Assert.AreEqual(10, data.Length);
        }
    }
}