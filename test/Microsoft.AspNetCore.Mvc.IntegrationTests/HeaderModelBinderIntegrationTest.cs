// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.IntegrationTests
{
    public class HeaderModelBinderIntegrationTest
    {
        private enum CarType
        {
            Coupe,
            Sedan
        }

        private class Person
        {
            public Address Address { get; set; }
        }

        private class Address
        {
            [FromHeader(Name = "Header")]
            [Required]
            public string Street { get; set; }

            [FromHeader]
            public string OneCommaSeparatedString { get; set; }

            [FromHeader]
            public int IntProperty { get; set; }

            [FromHeader]
            public int? NullableIntProperty { get; set; }

            [FromHeader]
            public long? NullableLongProperty { get; set; }

            [FromHeader]
            public string[] ArrayOfString { get; set; }

            [FromHeader]
            public IEnumerable<double> EnumerableOfDouble { get; set; }

            [FromHeader]
            public List<CarType> ListOfEnum { get; set; }
        }

        [Fact]
        public async Task BindPropertyFromHeader_NoData_UsesFullPathAsKeyForModelStateErrors()
        {
            // Arrange
            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor()
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo()
                {
                    BinderModelName = "CustomParameter",
                },
                ParameterType = typeof(Person)
            };

            // Do not add any headers.
            var testContext = ModelBindingTestHelper.GetTestContext();
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var boundPerson = Assert.IsType<Person>(modelBindingResult.Model);
            Assert.NotNull(boundPerson);

            // ModelState
            Assert.False(modelState.IsValid);
            var key = Assert.Single(modelState.Keys);
            Assert.Equal("CustomParameter.Address.Header", key);
            var error = Assert.Single(modelState[key].Errors);
            Assert.Equal(ValidationAttributeUtil.GetRequiredErrorMessage("Street"), error.ErrorMessage);
        }

        [Fact]
        public async Task BindPropertyFromHeader_WithPrefix_GetsBound()
        {
            // Arrange
            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor()
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo()
                {
                    BinderModelName = "prefix",
                },
                ParameterType = typeof(Person)
            };

            var testContext = ModelBindingTestHelper.GetTestContext(
                request =>
                {
                    request.Headers.Add("Header", "someValue");
                    request.Headers.Add("OneCommaSeparatedString", "one, two, three");
                    request.Headers.Add("IntProperty", "10");
                    request.Headers.Add("NullableIntProperty", "300");
                    request.Headers.Add("ArrayOfString", "first, second");
                    request.Headers.Add("EnumerableOfDouble", "10.51, 45.44");
                    request.Headers.Add("ListOfEnum", "Sedan, Coupe");
                });
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var boundPerson = Assert.IsType<Person>(modelBindingResult.Model);
            Assert.NotNull(boundPerson);
            Assert.NotNull(boundPerson.Address);
            Assert.Equal("someValue", boundPerson.Address.Street);
            Assert.Equal("one, two, three", boundPerson.Address.OneCommaSeparatedString);
            Assert.Equal(10, boundPerson.Address.IntProperty);
            Assert.Equal(300, boundPerson.Address.NullableIntProperty);
            Assert.Null(boundPerson.Address.NullableLongProperty);
            Assert.Equal(new[] { "first", "second" }, boundPerson.Address.ArrayOfString);
            Assert.Equal(new double[] { 10.51, 45.44 }, boundPerson.Address.EnumerableOfDouble);
            Assert.Equal(new CarType[] { CarType.Sedan, CarType.Coupe }, boundPerson.Address.ListOfEnum);

            // ModelState
            Assert.True(modelState.IsValid);
            var entry = modelState["prefix.Address.Header"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("someValue", entry.AttemptedValue);
            Assert.Equal("someValue", entry.RawValue);

            entry = modelState["prefix.Address.OneCommaSeparatedString"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("one, two, three", entry.AttemptedValue);
            Assert.Equal("one, two, three", entry.RawValue);

            entry = modelState["prefix.Address.IntProperty"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("10", entry.AttemptedValue);
            Assert.Equal("10", entry.RawValue);

            entry = modelState["prefix.Address.NullableIntProperty"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("300", entry.AttemptedValue);
            Assert.Equal("300", entry.RawValue);

            entry = modelState["prefix.Address.NullableLongProperty"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("", entry.AttemptedValue);
            Assert.Null(entry.RawValue);

            entry = modelState["prefix.Address.ArrayOfString"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("first,second", entry.AttemptedValue);
            Assert.Equal(new[] { "first", "second" }, entry.RawValue);

            entry = modelState["prefix.Address.EnumerableOfDouble"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("10.51,45.44", entry.AttemptedValue);
            Assert.Equal(new[] { "10.51", "45.44" }, entry.RawValue);

            entry = modelState["prefix.Address.ListOfEnum"];
            Assert.NotNull(entry);
            Assert.Empty(entry.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.ValidationState);
            Assert.Equal("Sedan,Coupe", entry.AttemptedValue);
            Assert.Equal(new[] { "Sedan", "Coupe" }, entry.RawValue);
        }

        // The scenario is interesting as we to bind the top level model we fallback to empty prefix,
        // and hence the model state keys have an empty prefix.
        [Fact]
        public async Task BindPropertyFromHeader_WithData_WithEmptyPrefix_GetsBound()
        {
            // Arrange
            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor()
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo(),
                ParameterType = typeof(Person)
            };

            var testContext = ModelBindingTestHelper.GetTestContext(
                request => request.Headers.Add("Header", new[] { "someValue" }));
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            var boundPerson = Assert.IsType<Person>(modelBindingResult.Model);
            Assert.NotNull(boundPerson);
            Assert.NotNull(boundPerson.Address);
            Assert.Equal("someValue", boundPerson.Address.Street);

            // ModelState
            Assert.True(modelState.IsValid);
            var entry = Assert.Single(modelState);
            Assert.Equal("Address.Header", entry.Key);
            Assert.Empty(entry.Value.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.Value.ValidationState);
            Assert.Equal("someValue", entry.Value.AttemptedValue);
            Assert.Equal(new string[] { "someValue" }, entry.Value.RawValue);
        }

        private class ListContainer1
        {
            [FromHeader(Name = "Header")]
            public List<string> ListProperty { get; set; }
        }

        [Fact]
        public async Task BindCollectionPropertyFromHeader_WithData_IsBound()
        {
            // Arrange
            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo(),
                ParameterType = typeof(ListContainer1),
            };

            var testContext = ModelBindingTestHelper.GetTestContext(
                request => request.Headers.Add("Header", new[] { "someValue" }));
            var modelState = testContext.ModelState;

            // Act
            var result = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert
            Assert.True(result.IsModelSet);

            // Model
            var boundContainer = Assert.IsType<ListContainer1>(result.Model);
            Assert.NotNull(boundContainer);
            Assert.NotNull(boundContainer.ListProperty);
            var entry = Assert.Single(boundContainer.ListProperty);
            Assert.Equal("someValue", entry);

            // ModelState
            Assert.True(modelState.IsValid);
            var kvp = Assert.Single(modelState);
            Assert.Equal("Header", kvp.Key);
            var modelStateEntry = kvp.Value;
            Assert.NotNull(modelStateEntry);
            Assert.Empty(modelStateEntry.Errors);
            Assert.Equal(ModelValidationState.Valid, modelStateEntry.ValidationState);
            Assert.Equal("someValue", modelStateEntry.AttemptedValue);
            Assert.Equal(new[] { "someValue" }, modelStateEntry.RawValue);
        }

        private class ListContainer2
        {
            [FromHeader(Name = "Header")]
            public List<string> ListProperty { get; } = new List<string> { "One", "Two", "Three" };
        }

        [Fact]
        public async Task BindReadOnlyCollectionPropertyFromHeader_WithData_IsBound()
        {
            // Arrange
            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo(),
                ParameterType = typeof(ListContainer2),
            };

            var testContext = ModelBindingTestHelper.GetTestContext(
                request => request.Headers.Add("Header", new[] { "someValue" }));
            var modelState = testContext.ModelState;

            // Act
            var result = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert
            Assert.True(result.IsModelSet);

            // Model
            var boundContainer = Assert.IsType<ListContainer2>(result.Model);
            Assert.NotNull(boundContainer);
            Assert.NotNull(boundContainer.ListProperty);
            var entry = Assert.Single(boundContainer.ListProperty);
            Assert.Equal("someValue", entry);

            // ModelState
            Assert.True(modelState.IsValid);
            var kvp = Assert.Single(modelState);
            Assert.Equal("Header", kvp.Key);
            var modelStateEntry = kvp.Value;
            Assert.NotNull(modelStateEntry);
            Assert.Empty(modelStateEntry.Errors);
            Assert.Equal(ModelValidationState.Valid, modelStateEntry.ValidationState);
            Assert.Equal("someValue", modelStateEntry.AttemptedValue);
            Assert.Equal(new[] { "someValue" }, modelStateEntry.RawValue);
        }

        [Theory]
        [InlineData(typeof(string[]), "value1, value2, value3")]
        [InlineData(typeof(string), "value")]
        public async Task BindParameterFromHeader_WithData_WithPrefix_ModelGetsBound(Type modelType, string value)
        {
            // Arrange
            object expectedValue;
            object expectedRawValue;
            if (modelType == typeof(string))
            {
                expectedValue = value;
                expectedRawValue = new string[] { value };
            }
            else
            {
                expectedValue = value.Split(',').Select(v => v.Trim()).ToArray();
                expectedRawValue = expectedValue;
            }

            var parameterBinder = ModelBindingTestHelper.GetParameterBinder();
            var parameter = new ParameterDescriptor
            {
                Name = "Parameter1",
                BindingInfo = new BindingInfo
                {
                    BinderModelName = "CustomParameter",
                    BindingSource = BindingSource.Header
                },
                ParameterType = modelType
            };

            Action<HttpRequest> action = r => r.Headers.Add("CustomParameter", new[] { value });
            var testContext = ModelBindingTestHelper.GetTestContext(action);

            // Do not add any headers.
            var httpContext = testContext.HttpContext;
            var modelState = testContext.ModelState;

            // Act
            var modelBindingResult = await parameterBinder.BindModelAsync(parameter, testContext);

            // Assert

            // ModelBindingResult
            Assert.True(modelBindingResult.IsModelSet);

            // Model
            Assert.NotNull(modelBindingResult.Model);
            Assert.IsType(modelType, modelBindingResult.Model);

            // ModelState
            Assert.True(modelState.IsValid);
            var entry = Assert.Single(modelState);
            Assert.Equal("CustomParameter", entry.Key);
            Assert.Empty(entry.Value.Errors);
            Assert.Equal(ModelValidationState.Valid, entry.Value.ValidationState);
            Assert.Equal(value, entry.Value.AttemptedValue);
            Assert.Equal(expectedRawValue, entry.Value.RawValue);
        }
    }
}