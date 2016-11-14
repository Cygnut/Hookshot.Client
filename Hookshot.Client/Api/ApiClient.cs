using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Specialized;

using Android.Util;

namespace Hookshot.Client.Api
{
    using Requests;
    using Responses;

    public class ApiClient
    {
        static readonly string TAG = "ApiClient";

        public string Address { get; private set; }

        public ApiClient(string address)
        {
            Address = address;
        }

        async Task<HttpResponseMessage> GetResponse(IRequest request, CancellationToken cancel)
        {
            var r = request.GetRequestInfo();
            var method = r.Method;
            string url = r.GetUrl(Address);

            Log.Debug(TAG, $"{method.Method}: {url}");

            var c = new HttpClient();
            if (method == HttpMethod.Get)
                return await c.GetAsync(url, cancel);
            else if (method == HttpMethod.Delete)
                return await c.DeleteAsync(url, cancel);
            else if (method == HttpMethod.Post)
                return await c.PostAsync(url, r.Content, cancel);
            else if (method == HttpMethod.Put)
                return await c.PutAsync(url, r.Content, cancel);
            else throw new ArgumentException($"Unsupported http method {method}");
        }

        public async Task<TResponse> Call<TResponse>(IRequest request, CancellationToken cancel)
            where TResponse : IResponse, new()
        {
            var r = await GetResponse(request, cancel);
            var response = new TResponse();
            await response.ParseAsync(r.Content);
            return response;
        }

        static string GetRegisteredRequests()
        {
            var requests = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => typeof(Requests.IRequest).IsAssignableFrom(p))
                .Where(p => !p.IsAbstract && !p.IsInterface)
                .Select(p =>
                {
                    return (IRequest)p
                        .GetConstructor(Type.EmptyTypes)
                        .Invoke(null);
                });
            var sb = new StringBuilder("Registered requests:");
            foreach (var r in requests)
            {
                var i = r.GetRequestInfo();
                sb.AppendLine($"{i.Method.Method} {i.Path}");
            }
            return sb.ToString();
        }
    }
}