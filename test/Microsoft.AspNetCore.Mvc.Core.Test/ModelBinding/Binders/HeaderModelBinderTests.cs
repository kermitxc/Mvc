// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Binders
{
    public class HeaderModelBinderTests
    {
        [Theory]
        [InlineData(typeof(string))]
        //[InlineData(typeof(string[]))]
        //[InlineData(typeof(object))]
        //[InlineData(typeof(int))]
        //[InlineData(typeof(int[]))]
        //[InlineData(typeof(BindingSource))]
        public async Task BindModelAsync_ReturnsNonEmptyResult_ForAllTypes_WithHeaderBindingSource(Type type)
        {
            // Arrange
            var modelMetadata = GetModelMetadata(type);
            var bindingContext = GetBindingContext(modelMetadata);
            var binder = GetBinder(modelMetadata);

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.False(bindingContext.Result.IsModelSet);
        }

        [Fact]
        public async Task HeaderBinder_BindsHeaders_ToStringCollection()
        {
            // Arrange
            var type = typeof(string[]);
            var header = "Accept";
            var headerValue = "application/json,text/json";
            var modelMetadata = GetModelMetadata(type);
            var bindingContext = GetBindingContext(modelMetadata);
            var binder = GetBinder(modelMetadata);

            bindingContext.FieldName = header;
            bindingContext.HttpContext.Request.Headers.Add(header, new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.Equal(headerValue.Split(','), bindingContext.Result.Model);
        }

        [Fact]
        public async Task HeaderBinder_BindsHeaders_ToStringType()
        {
            // Arrange
            var type = typeof(string);
            var header = "User-Agent";
            var headerValue = "UnitTest";
            var modelMetadata = GetModelMetadata(type);
            var bindingContext = GetBindingContext(modelMetadata);
            var binder = GetBinder(modelMetadata);

            bindingContext.FieldName = header;
            bindingContext.HttpContext.Request.Headers.Add(header, new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.Equal(headerValue, bindingContext.Result.Model);
        }

        [Theory]
        [InlineData(typeof(IEnumerable<string>))]
        [InlineData(typeof(ICollection<string>))]
        [InlineData(typeof(IList<string>))]
        [InlineData(typeof(List<string>))]
        [InlineData(typeof(LinkedList<string>))]
        [InlineData(typeof(StringList))]
        public async Task HeaderBinder_BindsHeaders_ForCollectionsItCanCreate(Type destinationType)
        {
            // Arrange
            var header = "Accept";
            var headerValue = "application/json,text/json";
            var modelMetadata = GetModelMetadata(destinationType);
            var bindingContext = GetBindingContext(modelMetadata);
            var binder = GetBinder(modelMetadata);

            bindingContext.FieldName = header;
            bindingContext.HttpContext.Request.Headers.Add(header, new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.IsAssignableFrom(destinationType, bindingContext.Result.Model);
            Assert.Equal(headerValue.Split(','), bindingContext.Result.Model as IEnumerable<string>);
        }

        [Fact]
        public async Task HeaderBinder_ReturnsResult_ForReadOnlyDestination()
        {
            // Arrange
            var header = "Accept";
            var headerValue = "application/json,text/json";
            var binder = new HeaderModelBinder(NullLoggerFactory.Instance);
            var bindingContext = GetBindingContextForReadOnlyArray();

            bindingContext.FieldName = header;
            bindingContext.HttpContext.Request.Headers.Add(header, new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.NotNull(bindingContext.Result.Model);
        }

        [Fact]
        public async Task HeaderBinder_ReturnsFailedResult_ForCollectionsItCannotCreate()
        {
            // Arrange
            var header = "Accept";
            var headerValue = "application/json,text/json";
            var modelMetadata = GetModelMetadata(typeof(ISet<string>));
            var bindingContext = GetBindingContext(modelMetadata);
            var binder = GetBinder(modelMetadata);

            bindingContext.FieldName = header;
            bindingContext.HttpContext.Request.Headers.Add(header, new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.False(bindingContext.Result.IsModelSet);
            Assert.Null(bindingContext.Result.Model);
        }

        private static IModelBinder GetBinder(ModelMetadata modelMetadata)
        {
            var options = Options.Create(new MvcOptions());
            var setup = new MvcCoreMvcOptionsSetup(new TestHttpRequestStreamReaderFactory());
            setup.Configure(options.Value);

            var modelBinderProviderContext = new TestModelBinderProviderContext(modelMetadata, new BindingInfo()
            {
                BinderModelName = modelMetadata.BinderModelName,
                BinderType = modelMetadata.BinderType,
                BindingSource = modelMetadata.BindingSource,
                PropertyFilterProvider = modelMetadata.PropertyFilterProvider,
            });
            var headerModelBinderProvider = new HeaderModelBinderProvider();
            return headerModelBinderProvider.GetBinder(modelBinderProviderContext);
        }

        private static ModelMetadata GetModelMetadata(Type modelType)
        {
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider.ForType(modelType).BindingDetails(d => d.BindingSource = BindingSource.Header);
            return metadataProvider.GetMetadataForType(modelType);
        }

        private static DefaultModelBindingContext GetBindingContextForReadOnlyArray()
        {
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider
                .ForProperty<ModelWithReadOnlyArray>(nameof(ModelWithReadOnlyArray.ArrayProperty))
                .BindingDetails(bd => bd.BindingSource = BindingSource.Header);
            var modelMetadata = metadataProvider.GetMetadataForProperty(
                typeof(ModelWithReadOnlyArray),
                nameof(ModelWithReadOnlyArray.ArrayProperty));

            return GetBindingContext(modelMetadata);
        }

        private static DefaultModelBindingContext GetBindingContext(ModelMetadata modelMetadata)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

            var bindingContext = new DefaultModelBindingContext
            {
                ActionContext = new ActionContext()
                {
                    HttpContext = new DefaultHttpContext()
                    {
                        RequestServices = services.BuildServiceProvider()
                    }
                },
                ModelMetadata = modelMetadata,
                ModelName = "modelName",
                FieldName = "modelName",
                ModelState = new ModelStateDictionary(),
                BinderModelName = modelMetadata.BinderModelName,
                BindingSource = modelMetadata.BindingSource,
            };

            return bindingContext;
        }

        private class ModelWithReadOnlyArray
        {
            public string[] ArrayProperty { get; }
        }

        private class StringList : List<string>
        {
        }
    }
}