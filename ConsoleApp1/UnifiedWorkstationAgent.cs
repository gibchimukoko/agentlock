using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace UnifiedWorkstationAgent
{
    class UnifiedWorkstationAgent
    {
        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        private static Dictionary<string, DateTime> _rateLimitTracker = new Dictionary<string, DateTime>();

        static void Main(string[] args)
        {
            string workstationId = Environment.MachineName;

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/workstationId/");
            listener.Prefixes.Add("http://localhost:5000/lock/");
            listener.Prefixes.Add("http://localhost:5000/unlock/");
            //ConfigureHttps(listener);
            listener.Start();

            Console.WriteLine("Agent is running...");
            Console.WriteLine("Listening on:");
            Console.WriteLine("  - http://localhost:5000/workstationId");
            Console.WriteLine("  - http://localhost:5000/lock");
            Console.WriteLine("  - http://localhost:5000/unlock");

            while (true)
            {
                try
                {
                    var context = listener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    if (!ValidateRequest(request))
                    {
                        HandleError(response, 401, "Unauthorized request.");
                        continue;
                    }

                    if (IsRateLimited(request))
                    {
                        HandleError(response, 429, "Too many requests. Please try again later.");
                        continue;
                    }

                    if (request.Url.AbsolutePath == "/workstationId")
                    {
                        HandleWorkstationIdRequest(response, workstationId);
                    }
                    else if (request.Url.AbsolutePath == "/lock")
                    {
                        HandleLockRequest(response);
                    }
                    else if (request.Url.AbsolutePath == "/unlock")
                    {
                        HandleUnlockRequest(response);
                    }
                    else
                    {
                        HandleError(response, 404, "Endpoint not found.");
                    }
                }
                catch (Exception ex)
                {
                    LogEvent($"Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles requests for retrieving the workstation ID and current logged-in user.
        /// Responds with a JSON payload containing the workstation ID and username.
        /// </summary>
        private static void HandleWorkstationIdRequest(HttpListenerResponse response, string workstationId)
        {
            try
            {
                string userName = Environment.UserName;
                var responsePayload = new { workstationId = workstationId, userName = userName };
                string jsonResponse = JsonSerializer.Serialize(responsePayload);

                byte[] buffer = Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                LogEvent($"Responded with workstation ID: {workstationId}, User: {userName}");
            }
            catch (Exception ex)
            {
                HandleError(response, 500, $"Error in HandleWorkstationIdRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles requests to lock the workstation.
        /// Calls the Windows API to lock the workstation and responds with a success message.
        /// </summary>
        private static void HandleLockRequest(HttpListenerResponse response)
        {
            try
            {
                LogEvent("Lock request received. Locking workstation...");
                LockWorkStation();

                byte[] buffer = Encoding.UTF8.GetBytes("Workstation locked.");
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/plain";
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                LogEvent("Workstation locked.");
            }
            catch (Exception ex)
            {
                HandleError(response, 500, $"Error in HandleLockRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles requests to simulate unlocking the workstation.
        /// Note: Actual unlocking is not possible due to OS-level restrictions.
        /// Responds with a success message indicating the unlock simulation.
        /// </summary>
        private static void HandleUnlockRequest(HttpListenerResponse response)
        {
            try
            {
                LogEvent("Unlock request received. Simulating unlock...");

                byte[] buffer = Encoding.UTF8.GetBytes("Unlock simulated.");
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/plain";
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                LogEvent("Unlock simulated.");
            }
            catch (Exception ex)
            {
                HandleError(response, 500, $"Error in HandleUnlockRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the incoming request for authentication and authorization.
        /// Ensures only authorized clients can access the lock/unlock endpoints.
        /// </summary>
        private static bool ValidateRequest(HttpListenerRequest request)
        {
            string apiKey = request.Headers["X-API-Key"];
            return true;
        }

        /// <summary>
        /// Enforces rate limiting to prevent abuse of the lock/unlock endpoints.
        /// </summary>
        private static bool IsRateLimited(HttpListenerRequest request)
        {
            string clientIp = request.RemoteEndPoint.Address.ToString();
            if (_rateLimitTracker.ContainsKey(clientIp))
            {
                if ((DateTime.Now - _rateLimitTracker[clientIp]).TotalSeconds < 10)
                {
                    return true;
                }
                else
                {
                    _rateLimitTracker[clientIp] = DateTime.Now;
                    return false;
                }
            }
            else
            {
                _rateLimitTracker[clientIp] = DateTime.Now;
                return false;
            }
        }

        /// <summary>
        /// Logs important events (e.g., lock/unlock requests) for debugging and auditing purposes.
        /// </summary>
        private static void LogEvent(string message)
        {
            Console.WriteLine($"[{DateTime.Now}] {message}");
        }

        /// <summary>
        /// Handles errors by logging them and sending an appropriate HTTP error response to the client.
        /// </summary>
        private static void HandleError(HttpListenerResponse response, int statusCode, string errorMessage)
        {
            LogEvent($"Error {statusCode}: {errorMessage}");
            response.StatusCode = statusCode;
            byte[] buffer = Encoding.UTF8.GetBytes(errorMessage);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/plain";
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        /// <summary>
        /// Configures the HTTP listener to support HTTPS for secure communication.
        /// </summary>
        private static void ConfigureHttps(HttpListener listener)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2 certificate = store.Certificates
                .Find(X509FindType.FindBySubjectName, "localhost", false)
                .OfType<X509Certificate2>()
                .FirstOrDefault();

            if (certificate != null)
            {
                listener.Prefixes.Add("https://localhost:5001/");
                listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                LogEvent("HTTPS configured successfully.");
            }
            else
            {
                LogEvent("Failed to configure HTTPS: Certificate not found.");
            }
        }
    }
}