// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Binders
{
    /// <summary>
    /// An <see cref="IModelBinderProvider"/> for binding header values.
    /// </summary>
    public class HeaderModelBinderProvider : IModelBinderProvider
    {
        /// <inheritdoc />
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var modelMetadata = context.Metadata;

            if (context.BindingInfo.BindingSource != null
                && context.BindingInfo.BindingSource.CanAcceptDataFrom(BindingSource.Header)
                && IsSimpleType(modelMetadata))
            {
                var metadata = modelMetadata.GetMetadataForType(modelMetadata.ModelType);

                // Change the binding info to prevent recursion
                var nonHeaderBindingInfo = new BindingInfo(context.BindingInfo);
                nonHeaderBindingInfo.BindingSource = BindingSource.ModelBinding;

                var innerModelBinder = context.CreateBinder(metadata, nonHeaderBindingInfo);
                if (innerModelBinder == null)
                {
                    return null;
                }

                var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();
                return new HeaderModelBinder(loggerFactory, innerModelBinder);
            }

            return null;
        }

        // Support binding only to simple types or collection of simple types.
        private bool IsSimpleType(ModelMetadata modelMetadata)
        {
            if (modelMetadata.IsComplexType)
            {
                var elementMetadata = modelMetadata.ElementMetadata;

                // Is a complex type and not a collection
                if (elementMetadata == null)
                {
                    return false;
                }
                // The element of collection is not a simple type
                else if (elementMetadata.IsComplexType)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
