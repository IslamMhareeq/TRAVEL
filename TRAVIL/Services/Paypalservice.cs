using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TRAVEL.Services
{
    public interface IPayPalService
    {
        Task<PayPalOrderResponse> CreateOrderAsync(decimal amount, string currency, string bookingReference, string returnUrl, string cancelUrl);
        Task<PayPalCaptureResponse> CaptureOrderAsync(string orderId);
        Task<PayPalOrderDetails> GetOrderDetailsAsync(string orderId);
    }

    public class PayPalService : IPayPalService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalService> _logger;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;
        private readonly bool _useSandbox;

        public PayPalService(IConfiguration configuration, ILogger<PayPalService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();

            // Get PayPal configuration
            _clientId = _configuration["PayPal:ClientId"] ?? "";
            _clientSecret = _configuration["PayPal:ClientSecret"] ?? "";
            _useSandbox = _configuration.GetValue<bool>("PayPal:UseSandbox", true);

            // Set base URL based on environment
            _baseUrl = _useSandbox
                ? "https://api-m.sandbox.paypal.com"
                : "https://api-m.paypal.com";

            _logger.LogInformation($"PayPal Service initialized. Sandbox mode: {_useSandbox}");
        }

        /// <summary>
        /// Get OAuth access token from PayPal
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"PayPal auth failed: {content}");
                throw new Exception("Failed to authenticate with PayPal");
            }

            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
            return tokenResponse.GetProperty("access_token").GetString() ?? "";
        }

        /// <summary>
        /// Create a PayPal order and get approval URL for redirection
        /// </summary>
        public async Task<PayPalOrderResponse> CreateOrderAsync(decimal amount, string currency, string bookingReference, string returnUrl, string cancelUrl)
        {
            try
            {
                // If no PayPal credentials configured, use simulation mode
                if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                {
                    _logger.LogWarning("PayPal credentials not configured. Using simulation mode.");
                    return CreateSimulatedOrder(amount, currency, bookingReference, returnUrl);
                }

                var accessToken = await GetAccessTokenAsync();

                var orderRequest = new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            reference_id = bookingReference,
                            description = $"TRAVIL Booking - {bookingReference}",
                            amount = new
                            {
                                currency_code = currency,
                                value = amount.ToString("F2")
                            }
                        }
                    },
                    application_context = new
                    {
                        brand_name = "TRAVIL Travel Agency",
                        landing_page = "LOGIN",
                        user_action = "PAY_NOW",
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(orderRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"PayPal create order failed: {content}");
                    throw new Exception("Failed to create PayPal order");
                }

                var orderResponse = JsonSerializer.Deserialize<JsonElement>(content);
                var orderId = orderResponse.GetProperty("id").GetString();

                // Find approval URL
                string approvalUrl = "";
                var links = orderResponse.GetProperty("links");
                foreach (var link in links.EnumerateArray())
                {
                    if (link.GetProperty("rel").GetString() == "approve")
                    {
                        approvalUrl = link.GetProperty("href").GetString() ?? "";
                        break;
                    }
                }

                _logger.LogInformation($"PayPal order created: {orderId}");

                return new PayPalOrderResponse
                {
                    Success = true,
                    OrderId = orderId ?? "",
                    ApprovalUrl = approvalUrl,
                    Status = orderResponse.GetProperty("status").GetString() ?? "CREATED"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayPal order");
                return new PayPalOrderResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Capture payment after user approves on PayPal
        /// </summary>
        public async Task<PayPalCaptureResponse> CaptureOrderAsync(string orderId)
        {
            try
            {
                // If simulated order, return simulated capture
                if (orderId.StartsWith("SIM-"))
                {
                    _logger.LogInformation($"Simulated PayPal capture for order: {orderId}");
                    return new PayPalCaptureResponse
                    {
                        Success = true,
                        OrderId = orderId,
                        TransactionId = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                        Status = "COMPLETED"
                    };
                }

                var accessToken = await GetAccessTokenAsync();

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/checkout/orders/{orderId}/capture");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"PayPal capture failed: {content}");
                    throw new Exception("Failed to capture PayPal payment");
                }

                var captureResponse = JsonSerializer.Deserialize<JsonElement>(content);
                var status = captureResponse.GetProperty("status").GetString();

                // Get transaction ID from capture
                string transactionId = "";
                try
                {
                    var purchaseUnits = captureResponse.GetProperty("purchase_units");
                    var captures = purchaseUnits[0].GetProperty("payments").GetProperty("captures");
                    transactionId = captures[0].GetProperty("id").GetString() ?? "";
                }
                catch { }

                _logger.LogInformation($"PayPal payment captured: {orderId}, Transaction: {transactionId}");

                return new PayPalCaptureResponse
                {
                    Success = status == "COMPLETED",
                    OrderId = orderId,
                    TransactionId = transactionId,
                    Status = status ?? "UNKNOWN"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error capturing PayPal order {orderId}");
                return new PayPalCaptureResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Get order details from PayPal
        /// </summary>
        public async Task<PayPalOrderDetails> GetOrderDetailsAsync(string orderId)
        {
            try
            {
                // If simulated order
                if (orderId.StartsWith("SIM-"))
                {
                    return new PayPalOrderDetails
                    {
                        Success = true,
                        OrderId = orderId,
                        Status = "APPROVED"
                    };
                }

                var accessToken = await GetAccessTokenAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v2/checkout/orders/{orderId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"PayPal get order failed: {content}");
                    throw new Exception("Failed to get PayPal order details");
                }

                var orderResponse = JsonSerializer.Deserialize<JsonElement>(content);

                return new PayPalOrderDetails
                {
                    Success = true,
                    OrderId = orderId,
                    Status = orderResponse.GetProperty("status").GetString() ?? "UNKNOWN"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting PayPal order details {orderId}");
                return new PayPalOrderDetails
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Create a simulated order when PayPal credentials are not configured
        /// </summary>
        private PayPalOrderResponse CreateSimulatedOrder(decimal amount, string currency, string bookingReference, string returnUrl)
        {
            var simulatedOrderId = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}";

            // Create a simulated approval URL that redirects back to our site
            var simulatedApprovalUrl = $"{returnUrl}?token={simulatedOrderId}&PayerID=SIMULATED";

            _logger.LogInformation($"Created simulated PayPal order: {simulatedOrderId}");

            return new PayPalOrderResponse
            {
                Success = true,
                OrderId = simulatedOrderId,
                ApprovalUrl = simulatedApprovalUrl,
                Status = "CREATED",
                IsSimulated = true
            };
        }
    }

    // Response models
    public class PayPalOrderResponse
    {
        public bool Success { get; set; }
        public string OrderId { get; set; } = "";
        public string ApprovalUrl { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public bool IsSimulated { get; set; } = false;
    }

    public class PayPalCaptureResponse
    {
        public bool Success { get; set; }
        public string OrderId { get; set; } = "";
        public string TransactionId { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }

    public class PayPalOrderDetails
    {
        public bool Success { get; set; }
        public string OrderId { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ErrorMessage { get; set; }
    }
}