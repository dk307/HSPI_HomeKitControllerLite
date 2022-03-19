// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Net.Http.Headers
{
    internal sealed class GenericHeaderParser : BaseHeaderParser
    {
        internal static readonly GenericHeaderParser HostParser = new(false);
        internal static readonly GenericHeaderParser TokenListParser = new(true);
        internal static readonly GenericHeaderParser SingleValueNameValueWithParametersParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueNameValueWithParametersParser = new(true);
        internal static readonly GenericHeaderParser SingleValueNameValueParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueNameValueParser = new(true);
        internal static readonly GenericHeaderParser MailAddressParser = new(false);
        internal static readonly GenericHeaderParser SingleValueProductParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueProductParser = new(true);
        internal static readonly GenericHeaderParser RangeConditionParser = new(false);
        internal static readonly GenericHeaderParser SingleValueAuthenticationParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueAuthenticationParser = new(true);
        internal static readonly GenericHeaderParser RangeParser = new(false);
        internal static readonly GenericHeaderParser RetryConditionParser = new(false);
        internal static readonly GenericHeaderParser ContentRangeParser = new(false);
        internal static readonly GenericHeaderParser ContentDispositionParser = new(false);
        internal static readonly GenericHeaderParser SingleValueStringWithQualityParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueStringWithQualityParser = new(true);
        internal static readonly GenericHeaderParser SingleValueEntityTagParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueEntityTagParser = new(true);
        internal static readonly GenericHeaderParser SingleValueViaParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueViaParser = new(true);
        internal static readonly GenericHeaderParser SingleValueWarningParser = new(false);
        internal static readonly GenericHeaderParser MultipleValueWarningParser = new(true);

        private GenericHeaderParser(bool supportsMultipleValues) : base(supportsMultipleValues)
        {
        }
    }
}