using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace agent
{
    using System;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text.Json;

    class UnifiedWorkstationAgent
    {
        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        static void Main(string[] args)
        {
            string workstationId = Environment.MachineName;

            // Set up the HTTP listener
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/workstationId/"); // Get workstation ID
            listener.Prefixes.Add("http://localhost:5000/lock/"); // Lock the workstation
            listener.Prefixes.Add("http://localhost:5000/unlock/"); // Simulate unlocking
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
                    // Wait for a request
                    var context = listener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    // Route the request
                    if (request.Url.AbsolutePath == "/workstationId")
                    {
                        // Serve the workstation ID
                        HandleWorkstationIdRequest(response, workstationId);
                    }
                    else if (request.Url.AbsolutePath == "/lock")
                    {
                        // Handle the lock request
                        HandleLockRequest(response);
                    }
                    else if (request.Url.AbsolutePath == "/unlock")
                    {
                        // Handle the unlock request
                        HandleUnlockRequest(response);
                    }
                    else
                    {
                        // Handle unknown endpoints
                        response.StatusCode = 404;
                        response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private static void HandleWorkstationIdRequest(HttpListenerResponse response, string workstationId)
        {
            try
            {
                // Create the response payload
                var responsePayload = new { workstationId = workstationId };
                string jsonResponse = JsonSerializer.Serialize(responsePayload);

                // Write the response
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(jsonResponse);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "application/json";
                response.StatusCode = 200;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                Console.WriteLine($"Responded with workstation ID: {workstationId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleWorkstationIdRequest: {ex.Message}");
            }
        }

        private static void HandleLockRequest(HttpListenerResponse response)
        {
            try
            {
                Console.WriteLine("Lock request received. Locking workstation...");
                LockWorkStation();

                // Respond to the client
                response.StatusCode = 200;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes("Workstation locked.");
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/plain";
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                Console.WriteLine("Workstation locked.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleLockRequest: {ex.Message}");
            }
        }

        private static void HandleUnlockRequest(HttpListenerResponse response)
        {
            try
            {
                Console.WriteLine("Unlock request received. Simulating unlock...");

                // Simulate unlock behavior (e.g., log the event, notify admin, etc.)
                // Actual unlock cannot be implemented due to OS-level restrictions.

                // Respond to the client
                response.StatusCode = 200;
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes("Unlock simulated.");
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/plain";
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                Console.WriteLine("Unlock simulated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleUnlockRequest: {ex.Message}");
            }
        }
    }

}
