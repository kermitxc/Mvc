// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
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
        /// is the overload that takes an <see cref="ILoggerFactory"/>.</para>
        /// <para>Initializes a new instance of <see cref="HeaderModelBinder"/>.</para>
        /// </summary>
        [Obsolete("This constructor is obsolete and will be removed in a future version. The recommended alternative"
            + " is the overload that takes an " + nameof(ILoggerFactory) + ".")]
        public HeaderModelBinder()
            : this(NullLoggerFactory.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="HeaderModelBinder"/>.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public HeaderModelBinder(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<HeaderModelBinder>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="HeaderModelBinder"/>.
        /// </summary>
        /// <param name="innerModelBinder"></param>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
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

            InnerModelBinder = innerModelBinder;
            _logger = loggerFactory.CreateLogger<HeaderModelBinder>();
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

            var request = bindingContext.HttpContext.Request;

            // Property name can be null if the model metadata represents a type (rather than a property or parameter).
            var headerName = bindingContext.FieldName;

            _logger.AttemptingToBindModel(bindingContext);

            var headerValueProvider = new HeaderValueProvider(request.Headers, CultureInfo.InvariantCulture, bindingContext.FieldName);
            // Prevent breaking existing users in scenarios where they are binding to a 'string' property and expect
            // the whole comma separated string, if any, as a single string and not as a string array
            headerValueProvider.UseCommaSeparatedValues = bindingContext.ModelMetadata.IsEnumerableType;

            // Create a new binding scope in order to supply the HeaderValueProvider so that the binders like
            // SimpleTypeModelBinder can find values from header.
            ModelBindingResult result;
            using (bindingContext.EnterNestedScope(
                    bindingContext.ModelMetadata,
                    fieldName: bindingContext.FieldName,
                    modelName: bindingContext.ModelName,
                    model: bindingContext.Model))
            {
                bindingContext.ValueProvider = headerValueProvider;

                await InnerModelBinder.BindModelAsync(bindingContext);
                result = bindingContext.Result;
            }

            bindingContext.Result = result;

            _logger.DoneAttemptingToBindModel(bindingContext);
        }
    }
}
