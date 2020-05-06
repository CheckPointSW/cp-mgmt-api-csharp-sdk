using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace MgmtApi
{
    /// <summary>
    /// The ApiClient Class provides basic methods to utilize the REST Web Service of the Check Point Management server.
    /// 
    /// Summary of the principal methods:
    /// (1) Login                  - execute a login command
    /// (2) ApiCall                - execute a given command
    /// (3) ApiQuery               - return all the objects of a specific Query (should be used when there are a lot of objects)
    /// (4) HandleAsyncTasksAsSync - returns only when all of the processes and sub-processes of a given task have terminated
    /// (5) Exit                   - execute a logout command
    /// </summary>
    public class ApiClient
    {
        #region Constants

        private const string UrlProtocol             = "https";
        private const string UserAgent               = "csharp-api-wrapper";
        private const string Context                 = "/web_api/";
        private const string DefaultFingerprintFile  = "./fingerprints.txt";
        private const string InProgress              = "in progress";
        private const string SidHeader               = "X-chkp-sid";
        private const string LoginCmd                = "login";
        private const string ShowTaskCmd             = "show-task";
        private const string MgmtCientExec           = "CPDIR";
        private const string ContextLoginAsRoot      = "/bin/mgmt_cli";

        private const int Limit                      = 50;
        private const int AsyncTaskSleepSeconds      = 2;
        private const int OkResponseCode             = 200;

        #endregion

        #region Private Members

        /// <summary>
        ///Path to the debug file. Logs all of the API calls
        /// </summary>
        private string _debugFile;

        /// <summary>
        ///The settings for tunneling through a proxy
        /// </summary>
        private readonly ApiProxySettingsProcessor _proxySettings;

        /// <summary>
        /// Responsible for resolving the port
        /// </summary>
        private readonly ApiPortResolver _portResolver;

        #endregion

        #region Properties

        /// <summary>
        ///  Gets or sets a value indicating whether this <see cref="ApiClient"/> is using an insecure connection.
        ///  If set to True, will validate the server's fingerprint
        /// </summary>
        public bool CheckFingerprint { get; set; }

        /// <summary>
        /// Gets the fingerprint which is used for certificate validation.
        /// </summary>
        public FingerprintManager FingerprintManager { get; private set; }

        /// <summary>
        /// Paging size
        /// </summary>
        public int LimitQuery { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args"> Arguments for the constructor. Contains the fingerprint file name, the debug file name and the checkFingerprint status</param>
        public ApiClient(ApiClientArgs args)
        {
            if (args == null)
            {
                throw new ApiClientException("ERROR: input is invalid");
            }

            CheckFingerprint = args.CheckFingerprint;
            _proxySettings = new ApiProxySettingsProcessor(args.ProxySetting);
            if (args.Port != null) _portResolver = new ApiPortResolver(args.Port.Value, args.IsUserEnteredPort);
            FingerprintManager = new FingerprintManager(args.FingerprintFile, _proxySettings);
            LimitQuery = Limit;

            if (args.DebugFile != null)
            {
                SetDebugFile(args.DebugFile);
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ApiClient():
            this(new ApiClientArgs()){ }

        #endregion

        #region Public Methods

        #region Login Methods

        /// <summary>
        /// This function uses the login command to login into the management server.
        /// </summary>
        /// <param name="serverIpAddress">The IP address or name of the Check Point Management Server.</param>
        /// <param name="payload">JSON object containing the login command's arguments.</param>
        /// <returns><see cref="ApiLoginResponse"/> object</returns>
        public ApiLoginResponse Login(string serverIpAddress, JObject payload = null)
        {
            if (string.IsNullOrEmpty(serverIpAddress))
            {
                throw new ApiClientException("Error: server IP address argument is invalid");
            }

            string payloadAsString = (payload != null) ? payload.ToString() : string.Empty;
            return Login(serverIpAddress, payloadAsString);
        }

        /// <summary>
        /// This function uses the login command to login into the management server.
        /// </summary>
        /// <param name="serverIpAddress">The IP address or name of the Check Point Management Server.</param>
        /// <param name="payload">String representing a JSON object containing the login command's arguments.</param>
        /// <returns><see cref="ApiLoginResponse"/> object</returns>
        public ApiLoginResponse Login(string serverIpAddress, string payload) 
        {
            if (string.IsNullOrEmpty(serverIpAddress))
            {
                throw new ApiClientException("Error: server IP address argument is invalid");
            }

            int port = _portResolver.GetPort(false);
            ApiLoginResponse loginResponse = new ApiLoginResponse(serverIpAddress, OkResponseCode, port, new JObject());
            var apiCallResult    = ApiCall(loginResponse, "login", payload, false);
            var apiLoginResponse = apiCallResult as ApiLoginResponse;

            if (apiLoginResponse == null)
            {
                throw new ApiClientException("Failed to login to server");
            }

            return apiLoginResponse;
        }

        #endregion

        #region ApiCall Methods

        /// <summary>
        /// This command sends a web-service API request to the management server.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> of the login command.</param>
        /// <param name="command"> The command name to be invoked.</param>
        /// <param name="payload">String representing a JSON object containing the command's arguments.</param>
        /// <param name="handleAsyncTaskAsSync">Determines the behavior when the API server exec responds with a "task-id".
        ///                                     If TRUE, it will periodically checks the status of the task
        ///                                     and will not return until the task is completed.</param>
        /// <returns><see cref="ApiResponse"/> containing the server's answer for the request</returns>
        public ApiResponse ApiCall(ApiLoginResponse loginResponse, string command, string payload, bool handleAsyncTaskAsSync)
        {
            if (loginResponse == null)
            {
                throw new ApiClientException("Error: login response argument is null");
            }
            if (string.IsNullOrEmpty(command))
            {
                throw new ApiClientException("Error: 'command' argument is invalid");
            }

            // 1)  Establish Connection
            string urlString = string.Format("{0}://{1}:{2}{3}{4}", UrlProtocol, loginResponse.ServerIp, loginResponse.Port, Context, command);
            Uri url = new Uri(urlString);
            HttpWebRequest request = EstablishConnection(loginResponse, url);

            // Create a byte array of the data to be sent
            string data = payload ?? "{}";
            byte[] byteData = Encoding.UTF8.GetBytes(data);
            request.ContentLength = byteData.Length;

            ApiResponse res;

            try
            {
                // 2) Send request
                using (Stream requestStream = request.GetRequestStreamAsync().Result)
                {
                    requestStream.Write(byteData, 0, byteData.Length);
                }

                // 3) Get Response 
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    if (response == null)
                    {
                        throw new ApiClientException("Error: failed performing an I/O action, check that the server is up and " +
                                                     "running, and the certificate in the file is correct.\n If you use proxy " +
                                                     "server, define it in the ApiClientArgs object");
                    }

                    Stream stream = response.GetResponseStream();
                    if (stream == null)
                    {
                        throw new ApiClientException("Error: failed performing an I/O action, check that the server is up and " +
                                                     "running, and the certificate in the file is correct.\n If you use proxy " +
                                                     "server, define it in the ApiClientArgs object");
                    }

                    string responseJsonString = new StreamReader(stream).ReadToEnd();

                    // Creating ApiResponse
                    if (LoginCmd.Equals(command))
                    {
                        res = new ApiLoginResponse(loginResponse.ServerIp, (int)response.StatusCode, loginResponse.Port, JObject.Parse(responseJsonString));
                        // 4) When the command is 'login', hide the password so that it would not appear in the debug file.
                        data = ChangePasswordInData(data);
                    }
                    else
                    {
                        res = new ApiResponse(JObject.Parse(responseJsonString), (int)response.StatusCode);
                    }
                }
            }
            catch (WebException ex)
            {
                return HandleApiCallWebException(ex);
            }
            catch (AggregateException ex)
            {
                return new ApiResponse(ex.InnerExceptions[0]);
            }

            // 5) Store the request and the response (for debug purposes)
            if (_debugFile != null)
            {
                MakeApiCallLog(data, url, res);
            }

            // 6) If we want to wait for the task to end, wait for it
            if (handleAsyncTaskAsSync && res.Success  && ShowTaskCmd.Equals(command))
            {
                if (res.Data["task-id"] != null)
                {
                    res = HandleAsyncTaskAsSync(loginResponse, res.Data["task-id"].ToString());
                }
                else if (res.Data["tasks"] != null)
                {
                    res = HandleAsyncTasksAsSync(loginResponse, (JArray)res.Data["tasks"]);
                }
            }

            return res;
        }

        /// <summary>
        /// This command sends a web-service API request to the management server.
        /// </summary>
        /// <param name="loginResponse"> The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="command">The command name to be invoked.</param>
        /// <param name="payload">String representing a JSON object contains command's arguments.</param>
        /// <returns><see cref="ApiResponse"/> containing the server's answer for the request.</returns>
        public ApiResponse ApiCall(ApiLoginResponse loginResponse, string command, string payload)
        {
            return ApiCall(loginResponse, command, payload, true);
        }

        /// <summary>
        /// This command sends a web-service API request to the management server.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="command"> The command name to be invoked.</param>
        /// <param name="payload">JSON object contains command's arguments.</param>
        ///<returns><see cref="ApiResponse"/> containing the server's answer for the request.</returns>
        public ApiResponse ApiCall(ApiLoginResponse loginResponse, string command, JObject payload)
        {
            return ApiCall(loginResponse, command, payload.ToString(), true);
        }

        /// <summary>
        /// This command sends a web-service API request to the management server.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="command">The command name to be invoked.</param>
        /// <param name="payload">JSON object containing the command's arguments.</param>
        /// <param name="handleAsyncTaskAsSync">Determines the behavior when the API server responds with a "task-id".
        ///                                     by default, the function will periodically check the status of the task
        ///                                     and will not return until the task is completed.</param>
        ///<returns><see cref="ApiResponse"/> contains the server answer for the request.</returns>
        public ApiResponse ApiCall(ApiLoginResponse loginResponse, string command, JObject payload, bool handleAsyncTaskAsSync)
        {
            return ApiCall(loginResponse, command, payload.ToString(), handleAsyncTaskAsSync);
        }

        #endregion

        #region ApiQuery Methods

        /// <summary>
        /// This method receives a command query and returns the response - a list of all the desired objects.
        /// The method's purpose is to return a list with all of the desired objects, in contrast to the API's that return
        /// a list of a limited number of objects.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="command">Name of the API command. This command should be an API that returns an array of objects</param>
        /// <param name="key">The items that the function collects.</param>
        /// <param name="payload">String representing a JSON object containing the command's arguments.</param>
        /// <returns><see cref="ApiResponse"/> that contains all the objects </returns>
        public ApiResponse ApiQuery(ApiLoginResponse loginResponse, string command, string key, string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                throw new ApiClientException("Error: Payload argument is invalid, specify payload in json format");
            }

            JObject jsonPayload;
            try
            {
                jsonPayload = JObject.Parse(payload);
            }
            catch
            {
                throw new ApiClientException("Error: Payload argument is not in json format");
            }

            return ApiQuery(loginResponse, command, key, jsonPayload);
        }

        /// <summary>
        /// This method receives a command query and returns the response - a list of all the desired objects.
        /// The method's purpose is to return a list with all of the desired objects, in contrast to the API's that return
        /// a list of a limited number of objects.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="command">Name of the API command. This command should be an API that returns an array of objects</param>
        /// <param name="key">The items that the function collects.</param>
        /// <param name="payload">The payload for the Api call</param>
        /// <returns><see cref="ApiResponse"/> that contains all the objects </returns>
        public ApiResponse ApiQuery(ApiLoginResponse loginResponse, string command, string key, JObject payload)
        {
            bool finished           = false;               // Will become true after getting all of the data
            JArray allObjects       = new JArray();        // Accumulate all of the objects from all the API calls
            int iterations          = 0;                   // Number of times the API request has been called
            ApiResponse apiResponse = null;                // API call response object
            JObject offsetPayload   = new JObject(payload);

            // Did we get all the objects?
            while (!finished)
            {
                // Make the API call, offset should be increased by limit with each iteration
                offsetPayload.Remove("limit");
                offsetPayload.Remove("offset");
                offsetPayload.Add("limit", LimitQuery);
                offsetPayload.Add("offset", iterations * LimitQuery);

                apiResponse = ApiCall(loginResponse,command, offsetPayload);

                iterations++;

                if (!apiResponse.Success)
                {
                    return apiResponse;
                }

                JObject responsePayload = apiResponse.Data;
                if (responsePayload[key] == null || !responsePayload[key].HasValues || responsePayload["total"] == null)
                {
                    return apiResponse;
                }

                // Total number of objects
                int totalObjects = int.Parse(responsePayload["total"].ToString());

                if (totalObjects == 0)
                {
                    return apiResponse;
                }

                // Number of objects received so far
                int receivedObjects = int.Parse(responsePayload["to"].ToString());

                // Add the new objects to the objects list
                allObjects.Add(responsePayload[key]);

                // Were all the desired objects received
                if (receivedObjects == totalObjects)
                {
                    finished = true;
                }
            }

            // Creating a result list of all the objects
            apiResponse.Data.Remove("from");
            apiResponse.Data.Remove("to");

            // Replace the data from the last API call with the array of all objects.
            if (apiResponse.Data[key] != null)
            {
                apiResponse.Data.Remove(key);
            }
            apiResponse.Data.Add(key, allObjects);

            return apiResponse;
        }

        #endregion

        /// <summary>
        /// When the Management Server executes a time consuming command e.g: run-script, install-policy, publish,
        /// it will perform it asynchronously. In this case, a task-id will be returned to the user.
        /// The show-task command is used to receive the progress status and the result of the executed command.
        /// </summary>
        /// <param name="loginResponse"> The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="taskId">The tasks identifiers.</param>
        /// <returns>ApiResponse Result of show-task command</returns>
        public ApiResponse HandleAsyncTaskAsSync(ApiLoginResponse loginResponse, string taskId)
        {
            bool taskComplete      = false;
            ApiResponse taskResult = null;

            // As long as there is a task in progress
            while (!taskComplete)
            {
                // Check the status of the task
                taskResult = ApiCall(loginResponse, ShowTaskCmd, "{\"task-id\": \"" + taskId + "\", \"details-level\": \"full\"}", false);
                if (taskResult == null )
                {
                    throw new ApiClientException("Error: failed to handle asynchronous task as synchronous, task result is undefined");
                }

                // Counts the number of tasks that are not in-progress
                int completedTasks = CountTaskNotInProgress(taskResult);

                // Get the total number of tasks
                int totalTasks = ((JArray)taskResult.Data["tasks"]).Count;

                // Are we done?
                if (completedTasks == totalTasks)
                {
                    taskComplete = true;
                }
                else
                {
                    try
                    {
                          Thread.Sleep(AsyncTaskSleepSeconds * 1000);
                    }
                    catch (Exception e)
                    {
                        throw new ApiClientException("Error: failed while 'sleep' function. Message: " + e.Message);
                    }
                }
            }

            //Check that the status of the tasks are not 'failed'
            CheckTasksStatus(taskResult);
            return taskResult;
        }

        /// <summary>
        /// This function logs out from the server.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/>- the response of the login command.</param>
        /// <returns>The <see cref=" ApiResponse"/> of the logout call.</returns>
        public ApiResponse Exit(ApiLoginResponse loginResponse)
        {
            if (loginResponse == null)
            {
                throw new ApiClientException("Error: login response argument is null");
            }

            ApiResponse response = ApiCall(loginResponse, "logout", "{}");
            return response;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This method allows the user to login to the management server with Root permissions.
        /// In order to use this method, the application must be run directly on the Management Server
        /// and to own super-user privileges.
        /// </summary>
        /// <param name="serverIpAddress">The IP address or name of the Check Point Management Server</param>
        /// <param name="payload">JSON object contains login command's arguments.</param>
        /// <returns><see cref="ApiLoginResponse"/> object</returns>
        private ApiLoginResponse LoginAsRoot(string serverIpAddress, JObject payload)
        {
            string systemEnvironment = Environment.GetEnvironmentVariable(MgmtCientExec);

            //checks
            if (systemEnvironment == null)
            {
                throw new ApiClientException("Failed to login as root, you are not running on the Management Server");
            }

            string pathFile = Path.Combine(systemEnvironment, ContextLoginAsRoot);

            if (!File.Exists(pathFile))
            {
                throw new ApiClientException("Failed to login as root, you are not running on the Management Server");
            }

            int port = _portResolver.GetPort(true);
            Process process = InvokeLoginAsRoot(pathFile, payload, port);
            string responseBodyString = GetResponseFromProcess(process);

            //Creating ApiResponse
            ApiLoginResponse loginResponse = new ApiLoginResponse(serverIpAddress, OkResponseCode, port, JObject.Parse(responseBodyString));
            return loginResponse;
        }

        /// <summary>
        /// This method reads the response from a given process and returns a string containing the response.
        /// </summary>
        /// <param name="process"></param>
        /// <returns> The response.</returns>
        private string GetResponseFromProcess(Process process)
        {
            StreamReader reader = process.StandardOutput;
            if (reader == null)
            {
                throw new ApiClientException("Error: failed receive the response from the server");
            }
            process.Close();
            return reader.ReadToEnd();
        }

        /// <summary>
        /// This method executes the login command.
        /// </summary>
        /// <param name="mgmtCliPath">The path of the system environment and command.</param>
        /// <param name="payload">JSON object containing the login command's arguments.</param>
        /// <param name="port">The port number of the management server</param>
        /// <returns>Process that is created after the execution of the login command.</returns>
        private Process InvokeLoginAsRoot(string mgmtCliPath, JObject payload, int port)
        {
            try
            {
                StringBuilder command = new StringBuilder();
                command.Append(" login -r true -f json --port ").Append(port);

                //Adding the domain to the command if it exist
                foreach (JProperty property in payload.Properties())
                {
                    command.Append(" ").Append(property.Name);
                    command.Append(" ").Append(property.Value);
                }

                //Executing the command
                ProcessStartInfo psi = new ProcessStartInfo(mgmtCliPath)
                {
                    Arguments = command.ToString(),
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                Process exeProcess;
                using (exeProcess = Process.Start(psi))
                {
                    if (exeProcess == null)
                    {
                        throw new ApiClientException("Error: Failed to login as root");
                    }
                    exeProcess.WaitForExit();
                }
                return exeProcess;
            }
            catch (IOException e)
            {
                throw new ApiClientException("Error: Failed to login as root, " + e.Message);
            }
        }

        /// <summary>
        /// This function is for logging purposes.
        /// Creates a new log from the response and sends the data and url.
        /// Store the new log to the api call list.
        /// </summary>
        /// <param name="data">The data of the request.</param>
        /// <param name="url">The url of the connection.</param>
        /// <param name="response">The response.</param>
        private void MakeApiCallLog(string data, Uri url, ApiResponse response)
        {
            try
            {
                //Creates a new log
                string headers = "{" + string.Format("\"User-Agent\":\"{0}\",\"Accept\":\"application/json\",\"Content-Type\":\"application/json\",\"Content-Length\":{1}", UserAgent, data.Length) + "}";
                string req = "{" + string.Format("\"url\":" + "\"{0}\"," + "\"payload\":{1},\"headers\":{2}", url, data, headers) + "}";

                JObject res = new JObject
                {
                    {"data", response.Data.ToString()},
                    {"status", response.ResponseCode.ToString()}
                };

                //Contains the log of the api call
                JObject apiLog = JObject.Parse("{" + string.Format("\"request\":{0},\"response\":{1}",req,res) + "}");

                //Saving the logs to a debug file, if they exist.
                SaveDataToDebugFile(apiLog);
            }
            catch (Exception)
            {
                throw new ApiClientException("Error: failed to store the logs of the api call, check that the payload is in JSON format.");
            }
        }

        /// <summary>
        /// This function creates an ApiRepsonse in case of a WebExcception
        /// </summary>
        /// <param name="ex"><see cref="WebException"/> That was throw</param>
        /// <returns></returns>
        private ApiResponse HandleApiCallWebException(WebException ex)
        {
            if (ex.Response != null)
            {
                Stream stream = ex.Response.GetResponseStream();
                if (stream == null)
                {
                    throw new ApiClientException("Error: failed performing an I/O action, check that the server is up and running");
                }

                // create an API_Response object with the error data in case the request fails
                string responseJsonString = new StreamReader(stream).ReadToEnd();
                try
                {
                    return new ApiResponse(JObject.Parse(responseJsonString), (int)((HttpWebResponse)ex.Response).StatusCode);
                }
                catch (Newtonsoft.Json.JsonReaderException e)
                {
                    return new ApiResponse(e);
                }
            }

            return new ApiResponse(ex);
        }

        /// <summary>
        /// This function is for secure purpose (i.e so the password in the debug file will be "****" and not the real one)
        /// receives a data and change the password
        /// </summary>
        /// <param name="data">The data that we want to change the password.</param>
        /// <returns>The data after the change.</returns>
        private static string ChangePasswordInData(string data)
        {
            JObject jsonObject = JObject.Parse(data);
            if (jsonObject["password"] != null)
            {
                jsonObject.Remove("password");
                jsonObject.Add("password", "****");
            }
            return jsonObject.ToString();
        }

        /// <summary>
        /// This function establishes an HttpsURL Connection.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="url">The url.</param>
        /// <returns><see cref="HttpWebRequest"/></returns>
        private HttpWebRequest EstablishConnection(ApiLoginResponse loginResponse, Uri url)
        {
            if (!CheckFingerprint)
            {
                // no need to validate certification fingerprint, always accept
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; };
            }
            else
            {
                ServicePointManager.ServerCertificateValidationCallback = delegate (object obj,
                System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors errors)
                {
                    // validate fingerprint hash
                    return CompareFingerPrint(loginResponse.ServerIp, certificate.GetCertHashString());
                };
            }

            // Create the web request  
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            if (_proxySettings.ProxySettingExist)
            {
                WebProxy myproxy = new WebProxy(_proxySettings.Host, _proxySettings.Port);
                myproxy.BypassProxyOnLocal = false;
                if (!string.IsNullOrEmpty(_proxySettings.UserName))
                {
                    myproxy.Credentials = new NetworkCredential(_proxySettings.UserName, _proxySettings.Password);
                }
                request.Proxy = myproxy;
            }

            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "*/*";
            request.UserAgent = UserAgent;

            //In all API calls (except for login) the header containing the Check Point session-id is required.
            if (loginResponse.Sid != null)
            {
                request.Headers[SidHeader] = loginResponse.Sid;
            }

            return request;
        }

        /// <summary>
        /// Checks if the server's fingerprint, which is stored in the local fingerprint storage file, equals to the
        /// fingerprint received in the current API call.
        /// If server's fingerprint is not found in the file, a {@link CertificateException} is thrown.
        /// If the fingerprint is found in the file, the function extracts the server's fingerprint from the
        /// certificate and compares them.
        /// If they are not equal, a <see cref="WebException"/> is thrown
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server.</param>
        /// <param name="cert"> Server certificate.</param>
        private bool CompareFingerPrint(string server, string cert)
        {
            if (cert == null)
            {
                throw new ApiClientException("Failed to get certificate info: " + cert);
            }

            //Reading fingerprint from file
            string fingerprintFile = FingerprintManager.GetFingerprintFromFile(server);
            if (fingerprintFile == null)
            {
                throw new WebException();
            }

            //Check if fingerprint from server's certificate 
            if (!fingerprintFile.Equals(cert))
            {
                throw new WebException();
            }

            return true;
        }

        /// <summary>
        /// When the Management Server executes a time consuming command e.g: run-script, install-policy, publish,
        /// server will perform it asynchronously. In this case a task-id is returned to the user.
        /// show--task command is used to receive the progress status and the result of the executed command.
        /// This method calls "show-task" in intervals of {<see cref="AsyncTaskSleepSeconds"/>} to check the
        /// status of the executed task.
        /// The function returns when the task (and all its sub - tasks) are no longer in-progress.
        /// </summary>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="tasksId">The task identifier.</param>
        /// <returns><see cref="ApiResponse"/></returns>
        private ApiResponse HandleAsyncTasksAsSync(ApiLoginResponse loginResponse, JArray tasksId)
        {
            JArray tasks = new JArray();

            foreach (var task in tasksId)
            {
                string taskId = ((JObject)task)["task-id"].ToString();
                HandleAsyncTaskAsSync(loginResponse, taskId);
                tasks.Add(taskId);
            }

            ApiResponse taskResult = ApiCall(loginResponse, ShowTaskCmd, "{" + string.Format("\"task-id\":{0}, \"details-level\": \"full\"", tasks) + "}", false);
            if (taskResult == null)
            {
                throw new ApiClientException("Error: failed to handle asynchronous tasks as synchronous, tasks result is undefined");
            }

            //Check that the status of the tasks are not 'failed'
            CheckTasksStatus(taskResult);
            return taskResult;
        }

        #region Task Methods

        /// <summary>
        /// Returns the number of tasks that are no longer in progress.
        /// </summary>
        /// <param name="taskResult">contains a list of tasks.</param>
        /// <returns>The number of tasks that are not in progress.</returns>
        private int CountTaskNotInProgress(ApiResponse taskResult)
        {
            int count = 0;
            foreach (var task in (JArray)taskResult.Data["tasks"])
            {
                if (!InProgress.Equals(((JObject)task)["status"].ToString()))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// This function iterates over all the tasks in the given ApiResponse and checks if the status of either of them
        /// is 'failed'. In that case, the ApiResponse status will be changed to false.
        /// </summary>
        /// <param name="taskResult"><see cref="ApiResponse"/>returned from 'show-task' command</param>
        private void CheckTasksStatus(ApiResponse taskResult)
        {
            // Go over all the tasks
           JArray tasks = (JArray) taskResult.Data["tasks"];
            foreach (var task in tasks)
            {
                if (!"failed".Equals(((JObject) task)["status"].ToString()) &&
                    !"partially succeeded".Equals(((JObject) task)["status"].ToString())) continue;
                taskResult.Success = false;
                break;
            }
        }

        #endregion

        #endregion

        #region Debug Methods

        /// <summary>
        /// Sets the debugger file.
        /// </summary>
        /// <param name="fileName">The new debugger file.</param>
        private void SetDebugFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ApiClientException("Error: file name is invalid");
            }

            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (IOException)
                {
                    throw new ApiClientException("Error: couldn't delete the debugger file");
                }
            }

            try
            {
                // Create new debug file
                using (FileStream fs =  File.Create(fileName))
                {
                    //Do noting
                }
            }
            catch (IOException)
            {
                throw new ApiClientException("Error: failed creating the debugger file");
            }

            _debugFile = fileName;
        }

        /// <summary>
        /// This method saves the logs to a debug file, if it exist.
        /// </summary>
        public void SaveDataToDebugFile(JObject apiCalls)
        {
            string data;
            if (_debugFile != null)
            {
                if (new FileInfo(_debugFile).Length == 0)
                {
                    data = apiCalls.ToString();
                }
                else
                {
                    data = "," + apiCalls.ToString();
                }
                try
                {
                    File.AppendAllText(_debugFile, data);
                }
                catch (IOException)
                {
                    throw new ApiClientException("Error: Failed to save log to debug file");
                }
            }
        }

        #endregion
    }
}
