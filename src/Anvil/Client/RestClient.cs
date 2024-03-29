using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Anvil.Client
{
    public class RestClient : BaseClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public RestClient(string apiKey)
        {
            _apiKey = apiKey;
            var encodedKey = EncodeApiKey();
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Constants.RestEndpoint);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {encodedKey}");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient = httpClient;

            // Default all JSON serialization to be `camelCase`. 
            // Classes representing payload data uses the typical `PascalCase`
            // C# standard and this is what we almost always want.
            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            _jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                NullValueHandling = NullValueHandling.Ignore
            };

            // If using the `System.Text.Json` serializer, we should use these options:
            //
            // _jsonSerializerOptions = new JsonSerializerOptions()
            // {
            //     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //     PropertyNameCaseInsensitive = true,
            //     IgnoreNullValues = true,
            // };
        }

        private Exception CreateExceptionFromResponse(HttpResponseMessage response)
        {
            var statusCode = response.StatusCode;
            var httpErrorReponse = (JObject)JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result);
            var ex = new AnvilClientException($"Error: {statusCode}");
            var errors = httpErrorReponse["errors"];
            var count = 1;

            foreach (JObject item in errors)
            {
                ex.Data.Add($"Message{count}", item["message"]);
                count += 1;
            }

            if (statusCode == HttpStatusCode.NotFound)
            {
                // Doesn't return JSON for this error...
                ex.Data.Add("Not Found", "");
            }
            else
            {
                // TODO: This can potentially have more than one error
            }

            return ex;
        }

        public async Task<HttpResponseMessage> SendGetRequest(string uri)
        {
            try
            {
                var response = await _httpClient.GetAsync(uri);

                if (!response.IsSuccessStatusCode)
                {
                    // Failed call, so create a custom exception for this.
                    var exc = CreateExceptionFromResponse(response);
                }

                return response;
            }
            catch (Exception e)
            {
                // TODO: How should we handle errors? 
                Console.WriteLine(e);
                throw e;
            }
        }

        private StringContent SerializePayload<T>(T payload)
        {
            return new StringContent(
                JsonConvert.SerializeObject(payload, _jsonSerializerSettings),
                Encoding.UTF8,
                "application/json"
            );
        }

        /// <summary>
        /// POSTs an HTTP request to the given URI.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> SendPostRequest(string uri, StringContent content)
        {
            var response = await _httpClient.PostAsync(uri, content);

            // TODO: Better error handling
            if (!response.IsSuccessStatusCode)
            {
                // Failed call, so create a custom exception for this.
                var exc = CreateExceptionFromResponse(response);
                throw exc;
            }

            return response;
        }

        private async Task<HttpResponseMessage> DoFillPdf(string templateId, Payloads.Request.FillPdf payload)
        {
            var uri = string.Format(Constants.FillPdf, templateId);
            var json = SerializePayload(payload);
            return await SendPostRequest(uri, json);
        }

        private async Task<HttpResponseMessage> DoFillPdf(string templateId, Payloads.Request.FillPdf payload,
            int versionNumber)
        {
            var uri = string.Format(Constants.FillPdf, templateId);
            uri = $"{uri}?versionNumber={versionNumber}";
            var json = SerializePayload(payload);
            return await SendPostRequest(uri, json);
        }

        public async Task<Stream> FillPdf(string templateId, Payloads.Request.FillPdf payload)
        {
            var response = await DoFillPdf(templateId, payload);

            // response will contain the resulting PDF in bytes if it was successful.
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<bool> FillPdf(string templateId, Payloads.Request.FillPdf payload, string destFile)
        {
            var stream = await FillPdf(templateId, payload);

            using (FileStream outputFileStream = new FileStream(destFile, FileMode.Create))
            {
                await stream.CopyToAsync(outputFileStream);
            }

            return true;
        }

        public async Task<Stream> FillPdf(string templateId, Payloads.Request.FillPdf payload, int versionNumber)
        {
            var response = await DoFillPdf(templateId, payload, versionNumber);

            // response will contain the resulting PDF in bytes if it was successful.
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<bool> FillPdf(string templateId, Payloads.Request.FillPdf payload, string destFile,
            int versionNumber)
        {
            var stream = await FillPdf(templateId, payload, versionNumber);

            using (FileStream outputFileStream = new FileStream(destFile, FileMode.Create))
            {
                await stream.CopyToAsync(outputFileStream);
            }

            return true;
        }


        private async Task<HttpResponseMessage> DoGeneratePdf(Payloads.Request.GeneratePdf payload)
        {
            var uri = Constants.GeneratePdf;
            var json = SerializePayload(payload);
            return await SendPostRequest(uri, json);
        }

        public async Task<HttpContent> GeneratePdf(Payloads.Request.GeneratePdf payload)
        {
            var response = await DoGeneratePdf(payload);

            // response will contain the resulting PDF in bytes if it was successful.
            return response.Content;
        }

        /// <summary>
        /// Sends a GeneratePDF request and saves the resulting data in the provided path.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="destFile"></param>
        /// <returns></returns>
        public async Task<bool> GeneratePdf(Payloads.Request.GeneratePdf payload, string destFile)
        {
            var stream = await GeneratePdf(payload);

            using (FileStream outputFileStream = new FileStream(destFile, FileMode.Create))
            {
                await stream.CopyToAsync(outputFileStream);
            }

            return true;
        }

        public async Task<Stream> DownloadDocuments(string documentGroupEid)
        {
            var uri = string.Format(Constants.DownloadDocuments, documentGroupEid);
            var stream = await SendGetRequest(uri);

            return await stream.Content.ReadAsStreamAsync();
        }
    }
}