// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace System.Net.Http.Headers
{
    internal class TransferCodingHeaderParser : BaseHeaderParser
    {
        private TransferCodingHeaderParser(bool supportsMultipleValues,
            Func<string, TransferCodingHeaderValue> transferCodingCreator)
            : base(supportsMultipleValues)
        {
        }

        private static TransferCodingHeaderValue CreateTransferCoding(string value)
        {
            return new TransferCodingHeaderValue(value);
        }

        private static TransferCodingHeaderValue CreateTransferCodingWithQuality(string value)
        {
            return new TransferCodingWithQualityHeaderValue(value);
        }

        internal static readonly TransferCodingHeaderParser MultipleValueParser =
            new(true, CreateTransferCoding);

        internal static readonly TransferCodingHeaderParser MultipleValueWithQualityParser =
            new(true, CreateTransferCodingWithQuality);

        internal static readonly TransferCodingHeaderParser SingleValueParser =
                                                    new(false, CreateTransferCoding);
        internal static readonly TransferCodingHeaderParser SingleValueWithQualityParser =
            new(false, CreateTransferCodingWithQuality);
    }
}