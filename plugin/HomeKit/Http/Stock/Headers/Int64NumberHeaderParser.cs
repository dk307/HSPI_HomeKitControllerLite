// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;

#nullable enable

namespace System.Net.Http.Headers
{
    internal class Int64NumberHeaderParser : BaseHeaderParser
    {
        // Note that we don't need a custom comparer even though we have a value type that gets boxed (comparing two
        // equal boxed value types returns 'false' since the object instances used for boxing the two values are
        // different). The reason is that the comparer is only used by HttpHeaders when comparing values in a collection.
        // Value types are never used in collections (in fact HttpHeaderValueCollection expects T to be a reference
        // type).

        private Int64NumberHeaderParser()
            : base(false)
        {
        }

        public override string ToString(object value)
        {
            Debug.Assert(value is long);

            return ((long)value).ToString(NumberFormatInfo.InvariantInfo);
        }

        internal static readonly Int64NumberHeaderParser Parser = new();
    }
}