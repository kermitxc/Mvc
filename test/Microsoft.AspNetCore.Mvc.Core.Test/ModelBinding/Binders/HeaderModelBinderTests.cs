// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Binders
{
    public class HeaderModelBinderTests
    {
        [Fact]
        public async Task HeaderBinder_BindsHeaders_ToStringCollection()
        {
            // Arrange
            var type = typeof(string[]);
            var headerValue = "application/json,text/json";
            var bindingContext = CreateContext(type);
            var binder = CreateBinder(bindingContext.ModelMetadata);
            bindingContext.HttpContext.Request.Headers.Add("Header", new[] { headerValue });

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.Equal(headerValue.Split(','), bindingContext.Result.Model);
        }

        public static TheoryData<string, Type, object> BinderHeaderToSimpleTypesData
        {
            get
            {
                var guid = Guid.NewGuid();

                return new TheoryData<string, Type, object>()
                {
                    { "10", typeof(int), 10 },
                    { "10.50", typeof(double), 10.50 },
                    { "10.50", typeof(IEnumerable<double>), new List<double>() { 10.50 } },
                    { "Sedan", typeof(CarType), CarType.Sedan },
                    { null, typeof(CarType?), null },
                    { "", typeof(CarType?), null },
                    { guid.ToString(), typeof(Guid), guid },
                    { "foo", typeof(string), "foo" },
                    { "foo, bar", typeof(string[]), new[]{ "foo", "bar" } },
                    { "foo, \"bar\"", typeof(string[]), new[]{ "foo", "bar" } },
                    { "\"foo,bar\"", typeof(string[]), new[]{ "foo,bar" } }
                };
            }
        }

        [Theory]
        [MemberData(nameof(BinderHeaderToSimpleTypesData))]
        public async Task HeaderBinder_BindsHeaders_ToSimpleTypes(
            string headerValue, 
            Type modelType, 
            object expectedModel)
        {
            // Arrange
            var bindingContext = CreateContext(modelType);
            var binder = CreateBinder(bindingContext.ModelMetadata);

            if (headerValue != null)
            {
                bindingContext.HttpContext.Request.Headers.Add("Header", headerValue);
            }

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.Equal(expectedModel, bindingContext.Result.Model);
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
            var headerValue = "application/json,text/json";
            var bindingContext = CreateContext(destinationType);
            var binder = CreateBinder(bindingContext.ModelMetadata);
            bindingContext.HttpContext.Request.Headers.Add("Header", new[] { headerValue });

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
            var bindingContext = CreateContext(GetMetadataForReadOnlyArray());
            var binder = CreateBinder(bindingContext.ModelMetadata);
            bindingContext.HttpContext.Request.Headers.Add("Header", "application/json,text/json");

            // Act
            await binder.BindModelAsync(bindingContext);

            // Assert
            Assert.True(bindingContext.Result.IsModelSet);
            Assert.NotNull(bindingContext.Result.Model);
        }

        private static DefaultModelBindingContext CreateContext(Type modelType)
        {
            return CreateContext(GetMetadataForType(modelType));
        }

        private static DefaultModelBindingContext CreateContext(ModelMetadata metadata)
        {
            return new DefaultModelBindingContext()
            {
                IsTopLevelObject = true,
                ModelMetadata = metadata,
                BinderModelName = metadata.BinderModelName,
                BindingSource = metadata.BindingSource,
                ModelName = "theModel", // HeaderModelBinder must always use the field name for back compat reasons
                FieldName = "Header",
                ModelState = new ModelStateDictionary(),
                ActionContext = new ActionContext()
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }

        private static IModelBinder CreateBinder(ModelMetadata metadata)
        {
            var options = new MvcOptions();
            var setup = new MvcCoreMvcOptionsSetup(new TestHttpRequestStreamReaderFactory());
            setup.Configure(options);

            var factory = TestModelBinderFactory.Create(options.ModelBinderProviders.ToArray());
            return factory.CreateBinder(new ModelBinderFactoryContext()
            {
                Metadata = metadata,
                BindingInfo = new BindingInfo()
                {
                    BinderModelName = metadata.BinderModelName,
                    BinderType = metadata.BinderType,
                    BindingSource = metadata.BindingSource,
                    PropertyFilterProvider = metadata.PropertyFilterProvider,
                },
            });
        }

        private static ModelMetadata GetMetadataForType(Type modelType)
        {
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider.ForType(modelType).BindingDetails(d => d.BindingSource = BindingSource.Header);
            return metadataProvider.GetMetadataForType(modelType);
        }

        private static ModelMetadata GetMetadataForReadOnlyArray()
        {
            var metadataProvider = new TestModelMetadataProvider();
            metadataProvider
                .ForProperty<ModelWithReadOnlyArray>(nameof(ModelWithReadOnlyArray.ArrayProperty))
                .BindingDetails(bd => bd.BindingSource = BindingSource.Header);
            return metadataProvider.GetMetadataForProperty(
                typeof(ModelWithReadOnlyArray),
                nameof(ModelWithReadOnlyArray.ArrayProperty));
        }

        private class ModelWithReadOnlyArray
        {
            public string[] ArrayProperty { get; }
        }

        private class StringList : List<string>
        {
        }

        private enum CarType
        {
            Sedan,
            Coupe
        }
    }
}