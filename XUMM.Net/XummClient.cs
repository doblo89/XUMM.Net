﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XUMM.Net.Clients;
using XUMM.Net.Clients.Interfaces;
using XUMM.Net.Extensions;
using XUMM.Net.Models;

namespace XUMM.Net;

public class XummClient : IXummClient, IDisposable
{
    private readonly JsonSerializerOptions _serializerOptions;

    internal readonly ILogger? Logger;

    public XummClient(XummClientOptions options) : this(options, default)
    {
    }

    public XummClient(XummClientOptions options, ILoggerFactory? loggerFactory)
    {
        Misc = new XummMiscClient(this);
        Payload = new XummPayloadClient(this);
        XApps = new XummXAppClient(this);

        ClientOptions =
            options ?? throw new ArgumentNullException(nameof(options), $"{nameof(options)} cannot be null");
        Logger = loggerFactory?.CreateLogger<XummClient>();

        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }

    public XummClientOptions ClientOptions { get; }

    public virtual void Dispose()
    {
        ClientOptions.Credentials.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public IXummMiscClient Misc { get; }

    /// <inheritdoc />
    public IXummPayloadClient Payload { get; }

    /// <inheritdoc />
    public IXummXAppClient XApps { get; }

    internal async Task<T> GetAsync<T>(string endpoint, bool isPublicEndpoint = false)
    {
        return await SendAsync<T>(HttpMethod.Get, endpoint, !isPublicEndpoint, default);
    }

    internal async Task<T> PostAsync<T>(string endpoint, object content)
    {
        return await PostAsync<T>(endpoint, JsonSerializer.Serialize(content, _serializerOptions));
    }

    internal async Task<T> PostAsync<T>(string endpoint, string json)
    {
        return await SendAsync<T>(HttpMethod.Post, endpoint, true, json);
    }

    internal async Task<T> DeleteAsync<T>(string endpoint)
    {
        return await SendAsync<T>(HttpMethod.Delete, endpoint, true, default);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string endpoint, bool setCredentials, string? json)
    {
        try
        {
            using var client = GetHttpClient(setCredentials);
            using var requestMessage = new HttpRequestMessage(method, $"{ClientOptions.BaseUrl}{endpoint}");

            if (json != null)
            {
                requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var response = await client.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                throw await GetHttpRequestExceptionAsync(response);
            }

            var result = (T?)await response.Content.ReadFromJsonAsync(typeof(T));
            if (result == null)
            {
                throw new Exception($"Unexpected response for {endpoint} response.");
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, $"Unexpected response from XUMM API [{method}:{endpoint}]");
            throw;
        }
    }

    private async Task<HttpRequestException> GetHttpRequestExceptionAsync(HttpResponseMessage response)
    {
        HttpRequestException? exception = null;
        try
        {
            if (await response.Content.ReadFromJsonAsync(typeof(XummFatalApiError)) is XummFatalApiError fatalApiError)
            {
                if (!string.IsNullOrWhiteSpace(fatalApiError.Message))
                {
                    exception = new HttpRequestException(fatalApiError.Message, null, response.StatusCode);
                }
            }

            if (exception == null &&
                await response.Content.ReadFromJsonAsync(typeof(XummApiError)) is XummApiError apiError)
            {
                exception = new HttpRequestException(
                    $"Error code: '{apiError.Error.Code}' with message: '{apiError.Error.Message}', see XUMM Dev Console, reference: '{apiError.Error.Reference}'.",
                    null, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogTrace(ex,
                $"No {nameof(XummFatalApiError)} available in unsuccessful response body of request: {response.RequestMessage?.RequestUri}");
        }

        return exception ??= new HttpRequestException(response.ReasonPhrase, null, response.StatusCode);
    }

    private HttpClient GetHttpClient(bool setCredentials)
    {
        var client = new HttpClient();
        if (setCredentials)
        {
            client.DefaultRequestHeaders.Add("X-API-Key", ClientOptions.Credentials.ApiKey.GetString());
            client.DefaultRequestHeaders.Add("X-API-Secret", ClientOptions.Credentials.ApiSecret.GetString());
        }

        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "XUMM-Net");
        return client;
    }
}
