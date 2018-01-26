// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Internal
{
    internal class HeaderValueProvider : IValueProvider
    {
        private readonly CultureInfo _culture;
        private readonly string[] _values;

        public HeaderValueProvider(CultureInfo culture, string[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            _culture = culture;
            _values = values;
        }

        /// <inheritdoc />
        public bool ContainsPrefix(string prefix)
        {
            return _values.Length != 0;
        }

        /// <inheritdoc />
        public ValueProviderResult GetValue(string key)
        {
            if (_values.Length == 0)
            {
                return ValueProviderResult.None;
            }
            else
            {
                return new ValueProviderResult(_values, _culture);
            }
        }
    }
}