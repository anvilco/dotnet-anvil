using System;
using System.Collections.Generic;
using System.Linq;

using Anvil;

using Newtonsoft.Json.Linq;

using Xunit;

namespace AnvilTests
{
    public class ErrorsTests
    {
        [Fact]
        public void LooksLikeJsonError_ForErrorsMessageOrName()
        {
            Assert.True(Errors.LooksLikeJsonError(JObject.Parse(@"{""errors"":[{""message"":""x""}]}")));
            Assert.True(Errors.LooksLikeJsonError(JObject.Parse(@"{""message"":""x""}")));
            Assert.True(Errors.LooksLikeJsonError(JObject.Parse(@"{""name"":""ValidationError""}")));
            Assert.False(Errors.LooksLikeJsonError(JObject.Parse(@"{""errors"":[]}")));
            Assert.False(Errors.LooksLikeJsonError(JObject.Parse(@"{""data"":{}}")));
            Assert.False(Errors.LooksLikeJsonError(new JArray()));
            Assert.False(Errors.LooksLikeJsonError(null));
        }

        [Fact]
        public void NormalizeJson_KeepsGraphQLErrors()
        {
            var json = JObject.Parse(@"{""errors"":[
                {""message"":""Not found"",""name"":""NotFoundError"",""path"":[""etchPacket""]},
                {""message"":""Second""}
            ]}");

            var errors = Errors.NormalizeJson(json, "OK");

            Assert.Equal(2, errors.Count);
            Assert.Equal("NotFoundError", errors[0].Name);
            Assert.Equal("Not found", errors[0].Message);
            Assert.Equal("etchPacket", errors[0].Raw!["path"]![0]!.ToString());
            Assert.Null(errors[1].Name);
            Assert.Equal("Second", errors[1].Message);
        }

        [Fact]
        public void NormalizeJson_WrapsARestErrorWithItsFields()
        {
            var json = JObject.Parse(@"{""name"":""ValidationError"",""message"":""Invalid"",""fields"":[
                {""property"":""data"",""message"":""Required""},
                {""property"":""title"",""message"":""Too long""}
            ]}");

            var error = Assert.Single(Errors.NormalizeJson(json, "Bad Request"));

            Assert.Equal("ValidationError", error.Name);
            Assert.Equal("Invalid", error.Message);
            Assert.Equal(new[] { "data: Required", "title: Too long" }, error.Fields.Select(f => f.ToString()));
            Assert.Equal("ValidationError: Invalid (data: Required, title: Too long)", error.ToString());
        }

        [Fact]
        public void NormalizeJson_FallsBackToTheStatusText()
        {
            var error = Assert.Single(Errors.NormalizeJson(JObject.Parse(@"{""something"":""else""}"), "Bad Gateway"));

            Assert.Equal("Bad Gateway", error.Name);
            Assert.Equal("Bad Gateway", error.Message);
            Assert.Equal("Bad Gateway", error.ToString());
        }

        [Fact]
        public void FromStatus_KeepsTheStartOfTheBody()
        {
            var body = new string('x', 5000);

            var error = Assert.Single(Errors.FromStatus("Bad Gateway", body));

            Assert.Equal("Bad Gateway", error.Name);
            Assert.Equal(2000, error.Raw!["body"]!.ToString().Length);
        }

        [Fact]
        public void FromStatus_WithoutAStatusText_IsUnknown()
        {
            Assert.Equal("Unknown Error", Assert.Single(Errors.FromStatus("", null)).ToString());
            Assert.Equal("Unknown Error", Assert.Single(Errors.FromStatus(null, "")).ToString());
        }

        [Fact]
        public void FromException_NamesTheExceptionType()
        {
            var error = Assert.Single(Errors.FromException(new InvalidOperationException("cut short")));

            Assert.Equal("InvalidOperationException", error.Name);
            Assert.Equal("cut short", error.Message);
        }

        [Fact]
        public void Exception_FormatsStatusAndEveryError()
        {
            var errors = new List<AnvilError>
            {
                new AnvilError("ValidationError", "Invalid", new[] { new AnvilErrorField("data", "Required") }),
                new AnvilError(null, "Second"),
            };

            var ex = new AnvilClientException(errors, 400);

            Assert.Equal("400: ValidationError: Invalid (data: Required); Second", ex.Message);
            Assert.Equal(400, ex.StatusCode);
            Assert.Empty(ex.Headers);
        }

        [Fact]
        public void Exception_WithoutAStatus_IsJustTheErrors()
        {
            var ex = new AnvilClientException(new[] { new AnvilError("TypeError", "fetch failed") }, 0);

            Assert.Equal("TypeError: fetch failed", ex.Message);
            Assert.Equal(0, ex.StatusCode);
            Assert.Empty(ex.Headers);
        }

        [Fact]
        public void Exception_FromAMessage_StillWorks()
        {
            var ex = new AnvilClientException("API key cannot be null or empty");

            Assert.Equal("API key cannot be null or empty", ex.Message);
            Assert.Equal("API key cannot be null or empty", Assert.Single(ex.Errors).Message);
            Assert.Equal(0, ex.StatusCode);
        }
    }
}
