using HomeKit.Http;
using Nito.AsyncEx;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    public sealed class MockNetworkReadStream : INetworkReadStream
    {
        private readonly MemoryStream source;
        private readonly int maxBuckeSize;

        public MockNetworkReadStream(MemoryStream source, int maxBuckeSize = int.MaxValue)
        {
            this.source = source;
            this.maxBuckeSize = maxBuckeSize;
        }

        public async Task<int> ReadAsync(byte[] buffer, int index, int v, CancellationToken cancellationToken)
        {
            int maxSize = Math.Min(maxBuckeSize, v); // do not send more than maxBuckeSize bytes
            var count = await source.ReadAsync(buffer, index, maxSize, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                var cancel = new CancellationTokenTaskSource<bool>(cancellationToken);
                await cancel.Task;
            }
            return count;
        }
    }

}