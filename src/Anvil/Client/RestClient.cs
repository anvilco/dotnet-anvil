using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Newtonsoft.Json;
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
            var ex = new Exception();
            var statusCode = response.StatusCode;
            var httpErrorObject = response.Content.ReadAsStringAsync().Result;

            if (statusCode == HttpStatusCode.NotFound)
            {
                // Doesn't return JSON for this error...
                ex.Data.Add("Not Found", "");
            }
            else
            {
                // TODO: This can potentially have more than one error... can it handle it?
                Console.WriteLine("Error");
                // var des = JsonSerializer.Deserialize<Payloads.Response.Error>(httpErrorObject);
                var des = new {Name = "what"};
                if (des != null)
                {
                    ex.Data.Add(des.Name, des);
                }
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
            try
            {
                var response = await _httpClient.PostAsync(uri, content);

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

        private async Task<HttpResponseMessage> DoFillPdf(string templateId, Payloads.Request.FillPdf payload)
        {
            var uri = string.Format(Constants.FillPdf, templateId);
            var json = SerializePayload(payload);
            return await SendPostRequest(uri, json);
        }

        public async Task<Stream> FillPdf(string templateId, Payloads.Request.FillPdf payload)
        {
            var response = await DoFillPdf(templateId, payload);

            // response will contain the resulting PDF in bytes if it was successful.
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<bool> FillPdf(string templateId, Payloads.Request.FillPdf payload, string outPath)
        {
            var stream = await FillPdf(templateId, payload);

            // TODO: Finalize file output
            var filePath = new DirectoryInfo(outPath);
            // if (!filePath.Exists)
            // {
            //     Console.WriteLine("DOES NOT EXIST");
            //     return false;
            // }

            var currentDir = Directory.GetCurrentDirectory();
            var outFilename = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString()}.pdf";
            var fullOutpath = new[] {currentDir, outFilename};

            using (FileStream outputFileStream = new FileStream(Path.Combine(fullOutpath), FileMode.Create))
            {
                stream.CopyTo(outputFileStream);
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