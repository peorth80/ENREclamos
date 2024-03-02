using System.Net;

namespace ENREclamos.Tests;

public class RealClientFactory: IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient();
    }
    
    public HttpClient CreateProxiedClient(string name) {
        var proxyAddress = "http://localhost:8000";
        var proxy = new WebProxy(proxyAddress);

        var httpClientHandler = new HttpClientHandler()
        {
            Proxy = proxy,
            UseProxy = true,
        };

        var httpClient = new HttpClient(httpClientHandler);

        return httpClient;
    }
}

public class ProxiedClientFactory: IHttpClientFactory
{
    public HttpClient CreateClient(string name) {
        var proxyAddress = "http://localhost:8000";
        var proxy = new WebProxy(proxyAddress);

        var httpClientHandler = new HttpClientHandler()
        {
            Proxy = proxy,
            UseProxy = true,
            ServerCertificateCustomValidationCallback = (message, certificate2, arg3, arg4) => true 
        };

        var httpClient = new HttpClient(httpClientHandler);

        return httpClient;
    }
}
