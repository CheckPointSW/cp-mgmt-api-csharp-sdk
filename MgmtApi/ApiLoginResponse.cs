using Newtonsoft.Json.Linq;

namespace MgmtApi
{
    public class ApiLoginResponse : ApiResponse
    {
        #region Properties

        /// <summary>
        /// Gets the Management server name or IP-address
        /// </summary>
        /// <returns>The server IP.</returns>
        public string ServerIp { get; private set; }

        /// <summary>
        /// Gets the session identifier
        /// </summary>
        /// <returns>The sid</returns>
        public string Sid { get; private set; }

        /// <summary>
        /// Gets the port number of the connection
        /// </summary>
        /// <returns>The port</returns>
        public int Port { get; private set; }

        /// <summary>
        /// Gets the api version of server
        /// </summary>
        /// <returns>Api version</returns>
        public string ApiVersion { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// This class represents a response object that contains data, status code, errors, session identifier, port number
        /// and server IP address of a checkpoint server's response to a login command that has been invoked.
        /// </summary>
        /// <param name="serverIp">The server IP address</param>
        /// <param name="statusCode">HTTP status  code</param>
        /// <param name="port">The port number</param>
        /// <param name="responseBody">The response body</param>
        public ApiLoginResponse(string serverIp, int statusCode, int port, JObject responseBody) : base(responseBody, statusCode)
        {
            ServerIp = serverIp;
            Port = port;

            if (Data["sid"] != null)
            {
                Sid = (string)Data["sid"];
            }
            else
            {
                Sid = null;
            }

            if (responseBody["api-server-version"] != null)
            {
                ApiVersion = (responseBody["api-server-version"]).ToString();
            }
            else
            {
                ApiVersion = null;
            }
        }

        #endregion
    }
}
