using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace GarageGroup.Infra;

partial class AccessTokenRemoteCache
{
    public async Task<IReadOnlyCollection<TokenRequestContext>> GetContextsAsync(CancellationToken cancellationToken)
    {
        using var httpClient = BuildHttpClient();

        var allContexts = new HashSet<TokenRequestContext>(TokenRequestContextEqualityComparer.Instance);
        string? nextMarker = null;

        while (true)
        {
            var contextSet = await ReadContextsAsync(httpClient, nextMarker, cancellationToken).ConfigureAwait(false);

            foreach (var context in contextSet.Contexts)
            {
                allContexts.Add(context);
            }

            if (string.IsNullOrWhiteSpace(contextSet.NextMarker))
            {
                break;
            }

            nextMarker = contextSet.NextMarker;
        }

        return allContexts;
    }

    private async Task<TokenContextSetOut> ReadContextsAsync(HttpClient httpClient, string? nextMarker, CancellationToken cancellationToken)
    {
        var urlParamsBuilder = new StringBuilder("restype=container&comp=list");
        if (string.IsNullOrWhiteSpace(nextMarker) is false)
        {
            urlParamsBuilder = urlParamsBuilder.Append("&marker=").Append(Uri.EscapeDataString(nextMarker));
        }

        using var httpRequest = new HttpRequestMessage(
            method: HttpMethod.Get,
            requestUri: BuildSignedUrl(permissions: PermissionsList, urlParams: urlParamsBuilder.ToString()));

        var httpResponse = await SendAsync(
            httpClient: httpClient,
            httpRequest: httpRequest,
            operationName: "ReadContexts",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (httpResponse.IsSuccessStatusCode is false)
        {
            if (httpResponse.StatusCode is HttpStatusCode.NotFound)
            {
                return new()
                {
                    Contexts = [],
                    NextMarker = null
                };
            }

            var failureText = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogError(
                "Unable to read token contexts from remote cache. HTTP status: {HttpStatusCode}. Response: {ResponseText}",
                (int)httpResponse.StatusCode,
                failureText);

            return new()
            {
                Contexts = [],
                NextMarker = null
            };
        }

        string xml = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var xmlDocument = XDocument.Parse(xml);
        var xmlNamespace = xmlDocument.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var contexts = new List<TokenRequestContext>();

        foreach (var blob in xmlDocument.Descendants(xmlNamespace + "Blob"))
        {
            var name = blob.Element(xmlNamespace + "Name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var context = await ReadContextAsync(httpClient, name, cancellationToken).ConfigureAwait(false);
            if (context is not null)
            {
                contexts.Add(context.Value);
            }
        }

        return new()
        {
            Contexts = contexts,
            NextMarker = xmlDocument.Root?.Element(xmlNamespace + "NextMarker")?.Value
        };
    }

    private async Task<TokenRequestContext?> ReadContextAsync(HttpClient httpClient, string fileName, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(
            method: HttpMethod.Get,
            requestUri: BuildSignedUrl(permissions: PermissionsRead, fileName: fileName));

        var httpResponse = await SendAsync(
            httpClient: httpClient,
            httpRequest: httpRequest,
            operationName: "ReadContext",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (httpResponse.IsSuccessStatusCode is false)
        {
            if (httpResponse.StatusCode is HttpStatusCode.NotFound)
            {
                return null;
            }

            var failureText = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogError(
                "Unable to read token context from remote cache. HTTP status: {HttpStatusCode}. Response: {ResponseText}",
                (int)httpResponse.StatusCode,
                failureText);

            return null;
        }

        var json = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var context = JsonSerializer.Deserialize<TokenCacheItemJson>(json, SerializerOptions)?.Context;
        return context?.ToModel();
    }

    private sealed record class TokenContextSetOut
    {
        public required IReadOnlyCollection<TokenRequestContext> Contexts { get; init; }

        public string? NextMarker { get; init; }
    }
}