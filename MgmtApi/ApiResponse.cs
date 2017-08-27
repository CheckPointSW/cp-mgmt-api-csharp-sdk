using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MgmtApi
{
    /// <summary>
    /// A class which includes all the data that is returned from an API call.
    /// </summary>
    public class ApiResponse
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether the API called succeeded or failed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the API response indicates success; otherwise, <c>false</c>.
        /// </value>
        public bool Success { get; internal set;}

        /// <summary>
        /// Gets the JSON data Contains the JSON data returned from the HTTP API request.
        /// </summary>
        /// <value>
        /// The data as a JSON token object.
        /// </value>
        public JObject Data { get; private set; }

        /// <summary>
        /// Gets the HTTP response code which the Web API returned.
        /// </summary>
        /// <value>
        /// The response code (200, 401, 404, 500).
        /// </value>
        public int ResponseCode { get; private set; }

        /// <summary>
        /// Gets the API error message.
        /// </summary>
        /// <value>
        /// The API error message.
        /// </value>
        public string ErrorMessage { get; private set; }

        /// <summary>
        /// Gets the API errors if exist.
        /// </summary>
        /// <value>
        /// The API errors
        /// </value>
        public string Errors { get; private set; }

        /// <summary>
        /// Gets the API warnings if exist.
        /// </summary>
        /// <value>
        /// The API warnings
        /// </value>
        public string Warnings { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiResponse"/> class.
        /// </summary>
        /// <param name="responseData">The response data coming from the HTTP request.</param>
        /// <param name="statusCode">The HTTP status code. (200, 403, 500, etc)</param>
        internal ApiResponse(JObject responseData, int statusCode)
        {
            ResponseCode = statusCode;
            Data = responseData;

            if (statusCode == 200)
            {
                // api call succeeded
                Success       = true;
                ErrorMessage  = null;
                Warnings      = null;
                Errors        = null;
            }
            else
            {
                Success = false;
                if(Data["message"] != null)
                {
                    ErrorMessage = Data["message"].ToString();
                }
                else
                {
                    ErrorMessage = null;
                }
                if (Data["warnings"] != null)
                {
                    Warnings = Data["warnings"].ToString();
                }
                else
                {
                    Warnings = null;
                }
                if (Data["errors"] != null)
                {
                    Errors = Data["errors"].ToString();
                }
                else
                {
                    Errors = null;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiResponse"/> class. Constructor for responses which failed before the API request was sent.
        /// </summary>
        /// <param name="ex">The exception to contain within.</param>
        internal ApiResponse(Exception ex)
        {
            string json = ExceptionToJSON(ex);
            Data = JObject.Parse(json);
            ErrorMessage = (string)Data["message"];
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> of the Data field (raw JSON string).
        /// </returns>
        public override string ToString()
        {
            return Data.ToString();
        }

        /// <summary>
        /// Translates exceptions to a JSON message and code.
        /// </summary>
        /// <param name="ex">The exception object to translate.</param>
        /// <returns>JSON string with an error and message code.</returns>
        private string ExceptionToJSON(Exception ex)
        {
            Dictionary<string, string> exceptionData = new Dictionary<string, string>();

            var exception = ex as WebException;
            if (exception != null)
            {
                var newEx = exception;
                if (newEx.Status == WebExceptionStatus.TrustFailure)
                {
                    exceptionData.Add("message", "Failed to verify server fingerprint");
                    exceptionData.Add("code", "fingerprint_verify_failure");
                }
                else if (newEx.Status == WebExceptionStatus.Timeout)
                {
                    exceptionData.Add("message", "HTTPS request timed out");
                    exceptionData.Add("code", "communication_timeout");
                }
                else if (newEx.Status == WebExceptionStatus.SendFailure)
                {
                    exceptionData.Add("message", "Could not connect with the Check Point management server. Please check that the server name is spelled correctly and verify that the API server running and can accept connections from this machine (SmartConsole > Manage & settings > Blades > Management API > Advanced settings)");
                    exceptionData.Add("code", "communication_error");
                }
            }
            else
            {
                exceptionData.Add("message", ex.Message);
                exceptionData.Add("code", ex.GetType().Name);
            }

            return JsonConvert.SerializeObject(exceptionData);
        }

        #endregion
    }
}
