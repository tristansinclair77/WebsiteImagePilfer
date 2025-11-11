using System;
using System.Net.Http;
using WebsiteImagePilfer.Constants;
using static WebsiteImagePilfer.Constants.AppConstants;

namespace WebsiteImagePilfer.Services
{
    /// <summary>
    /// Provides a singleton HttpClient instance for the entire application.
  /// Ensures proper connection pooling and resource management.
    /// </summary>
 public static class HttpClientFactory
    {
        private static readonly Lazy<HttpClient> _instance = new Lazy<HttpClient>(CreateHttpClient);

        /// <summary>
    /// Gets the singleton HttpClient instance.
        /// </summary>
        public static HttpClient Instance => _instance.Value;

   private static HttpClient CreateHttpClient()
     {
  // Configure SocketsHttpHandler for optimal performance
  var handler = new SocketsHttpHandler
       {
 // Recycle connections every 2 minutes to avoid DNS staleness
       PooledConnectionLifetime = TimeSpan.FromMinutes(2),
  // Allow up to 10 concurrent connections per server
     MaxConnectionsPerServer = 10,
       // Enable automatic decompression
 AutomaticDecompression = System.Net.DecompressionMethods.All
  };

      var client = new HttpClient(handler)
   {
       // Set reasonable default timeout (30 seconds from AppConstants)
    // Individual operations can override with CancellationTokenSource for finer control
     Timeout = TimeSpan.FromSeconds(Network.HttpTimeoutSeconds)
    };

// Set default headers
      client.DefaultRequestHeaders.Add("User-Agent", "WebsiteImagePilfer/1.0 (Mozilla/5.0 compatible)");
  client.DefaultRequestHeaders.Add("Accept", "image/*, text/html, application/xhtml+xml, */*");

         return client;
}
    }
}
