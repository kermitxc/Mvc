// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Binders
{
    /// <summary>
    /// An <see cref="IModelBinder"/> which binds models from the request headers when a model
    /// has the binding source <see cref="BindingSource.Header"/>.
    /// </summary>
    public class HeaderModelBinder : IModelBinder
    {
        private readonly ILogger _logger;

        /// <summary>
        /// <para>This constructor is obsolete and will be removed in a future version. The recommended alternative
        /// is the overload that takes an <see cref="ILoggerFactory"/> and an <see cref="IModelBinder"/>.</para>
        /// <para>Initializes a new instance of <see cref="HeaderModelBinder"/>.</para>
        /// </summary>
        [Obsolete("This constructor is obsolete and will be removed in a future version. The recommended alternative"
            + " is the overload that takes an " + nameof(ILoggerFactory) + " and an " + nameof(IModelBinder) + ".")]
        public HeaderModelBinder()
            : this(NullLoggerFactory.Instance)
        {
            _logger = NullLogger.Instance;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="HeaderModelBinder"/>.
        /// </summary>
        public HeaderModelBinder(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HeaderModelBinder>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="HeaderModelBinder"/>.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        /// <param name="innerModelBinder">The <see cref="IModelBinder"/> which does the actual
        /// binding of values.</param>
        public HeaderModelBinder(ILoggerFactory loggerFactory, IModelBinder innerModelBinder)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (innerModelBinder == null)
            {
                throw new ArgumentNullException(nameof(innerModelBinder));
            }

            _logger = loggerFactory.CreateLogger<HeaderModelBinder>();
            InnerModelBinder = innerModelBinder;
        }

        // to enable unit testing
        internal IModelBinder InnerModelBinder { get; }

        /// <inheritdoc />
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            _logger.AttemptingToBindModel(bindingContext);

            // Property name can be null if the model metadata represents a type (rather than a property or parameter).
            var headerName = bindingContext.FieldName;

            // Do not set ModelBindingResult to Failed on not finding the value in the header as we want the inner 
            // modelbinder to do that. This would give a chance to the inner binder to add more useful information.
            // For example, SimpleTypeModelBinder adds a model error when binding to let's say an integer and the
            // model is null.
            var request = bindingContext.HttpContext.Request;
            if (!request.Headers.ContainsKey(headerName))
            {
                _logger.FoundNoValueInRequest(bindingContext);
            }

            if (InnerModelBinder == null)
            {
                BindWithoutInnerBinder(bindingContext);
                return;
            }

            var headerValueProvider = GetHeaderValueProvider(headerName, bindingContext);

            // Capture the top level object here as entering nested scope would make it 'false' always.
            var isTopLevelObject = bindingContext.IsTopLevelObject;

            // Create a new binding scope in order to supply the HeaderValueProvider so that the binders like
            // SimpleTypeModelBinder can find values from header.
            ModelBindingResult result;
            using (bindingContext.EnterNestedScope(
                    bindingContext.ModelMetadata,
                    fieldName: bindingContext.FieldName,
                    modelName: bindingContext.ModelName,
                    model: bindingContext.Model))
            {
                bindingContext.IsTopLevelObject = isTopLevelObject;
                bindingContext.ValueProvider = headerValueProvider;

                await InnerModelBinder.BindModelAsync(bindingContext);
                result = bindingContext.Result;
            }

            bindingContext.Result = result;

            _logger.DoneAttemptingToBindModel(bindingContext);
        }

        private HeaderValueProvider GetHeaderValueProvider(string headerName, ModelBindingContext bindingContext)
        {
            var request = bindingContext.HttpContext.Request;

            // Prevent breaking existing users in scenarios where they are binding to a 'string' property
            // and expect the whole comma separated string, if any, as a single string and not as a string array.
            var values = Array.Empty<string>();
            if (request.Headers.ContainsKey(headerName))
            {
                if (bindingContext.ModelMetadata.IsEnumerableType)
                {
                    values = request.Headers.GetCommaSeparatedValues(headerName);
                }
                else
                {
                    values = new[] { (string)request.Headers[headerName] };
                }
            }

            return new HeaderValueProvider(CultureInfo.InvariantCulture, values);
        }

        private void BindWithoutInnerBinder(ModelBindingContext bindingContext)
        {
            var headerName = bindingContext.FieldName;
            var request = bindingContext.HttpContext.Request;

            object model;
            if (bindingContext.ModelType == typeof(string))
            {
                var value = request.Headers[headerName];
                model = (string)value;
            }
            else if (ModelBindingHelper.CanGetCompatibleCollection<string>(bindingContext))
            {
                var values = request.Headers.GetCommaSeparatedValues(headerName);
                model = GetCompatibleCollection(bindingContext, values);
            }
            else
            {
                // An unsupported datatype or a new collection is needed (perhaps because target type is an array) but
                // can't assign it to the property.
                model = null;
            }

            if (model == null)
            {
                // Silently fail if unable to create an instance or use the current instance. Also reach here in the
                // typeof(string) case if the header does not exist in the request and in the
                // typeof(IEnumerable<string>) case if the header does not exist and this is not a top-level object.
                bindingContext.Result = ModelBindingResult.Failed();
            }
            else
            {
                bindingContext.ModelState.SetModelValue(
                    bindingContext.ModelName,
                    request.Headers.GetCommaSeparatedValues(headerName),
                    request.Headers[headerName]);

                bindingContext.Result = ModelBindingResult.Success(model);
            }

            _logger.DoneAttemptingToBindModel(bindingContext);
        }

        private static object GetCompatibleCollection(ModelBindingContext bindingContext, string[] values)
        {
            // Almost-always success if IsTopLevelObject.
            if (!bindingContext.IsTopLevelObject && values.Length == 0)
            {
                return null;
            }

            if (bindingContext.ModelType.IsAssignableFrom(typeof(string[])))
            {
                // Array we already have is compatible.
                return values;
            }

            var collection = ModelBindingHelper.GetCompatibleCollection<string>(bindingContext, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                collection.Add(values[i]);
            }

            return collection;
        }

        private class HeaderValueProvider : IValueProvider
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

            public bool ContainsPrefix(string prefix)
            {
                return _values.Length != 0;
            }

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
}
