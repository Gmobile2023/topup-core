using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;

namespace Topup.Shared;

public static class HttpHelper
{
    public static async Task<T> Post<T, TK>(string baseUrl, string url, TK contentValue, string auth = null,
        HttpClientHandler handler = null, TimeSpan? timeout = null)
    {
        if (handler == null)
        {
            using var client = new HttpClient {BaseAddress = new Uri(baseUrl)};
            if (auth != null)
                client.DefaultRequestHeaders.Add("Authorization", auth);
            client.Timeout = timeout ?? TimeSpan.FromMinutes(1);
            var content = new StringContent(contentValue.ToJson(), Encoding.UTF8, "application/json");
            var result = await client.PostAsync(url, content);
            result.EnsureSuccessStatusCode();
            var resultContentString = await result.Content.ReadAsStringAsync();
            var resultContent =
                resultContentString.FromJson<T>();
            return resultContent;
        }
        else
        {
            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl), Timeout = timeout ?? TimeSpan.FromMinutes(1)
            };
            if (auth != null)
                client.DefaultRequestHeaders.Add("Authorization", auth);
            var content = new StringContent(contentValue.ToJson(), Encoding.UTF8, "application/json");
            var result = await client.PostAsync(url, content);
            result.EnsureSuccessStatusCode();
            var resultContentString = await result.Content.ReadAsStringAsync();
            var resultContent =
                resultContentString.FromJson<T>();
            return resultContent;
        }
    }

    public static async Task<T> Put<T, TK>(string baseUrl, string url, TK stringValue, TimeSpan? timeout = null)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl), Timeout = timeout ?? TimeSpan.FromMinutes(1)
        };
        var content = new StringContent(stringValue.ToJson(), Encoding.UTF8, "application/json");
        var result = await client.PutAsync(url, content);
        result.EnsureSuccessStatusCode();
        var resultContentString = await result.Content.ReadAsStringAsync();
        var resultContent =
            resultContentString.FromJson<T>();
        return resultContent;
    }

    public static async Task<T> Get<T>(string baseUrl, string url, TimeSpan? timeout = null)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl), Timeout = timeout ?? TimeSpan.FromMinutes(1)
        };

        var result = await client.GetAsync(url);
        result.EnsureSuccessStatusCode();
        var resultContentString = await result.Content.ReadAsStringAsync();
        var resultContent =
            resultContentString.FromJson<T>();
        return resultContent;
    }

    public static async Task<T> Delete<T>(string baseUrl, string url, TimeSpan? timeout = null)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl), Timeout = timeout ?? TimeSpan.FromMinutes(1)
        };
        var result = await client.DeleteAsync(url);
        var resultContentString = await result.Content.ReadAsStringAsync();
        var resultContent =
            resultContentString.FromJson<T>();
        return resultContent;
    }
}