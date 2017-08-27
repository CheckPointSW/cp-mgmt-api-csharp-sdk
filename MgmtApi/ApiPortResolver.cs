using System;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;


namespace MgmtApi
{
    /// <summary>
    /// This class is responsible for resolving the port number for the login command according to
    /// the origin of the port and the type of login command.
    /// </summary>
    internal class ApiPortResolver
    {
        #region Constants

        private const string ApiGetPortScript = "api_get_port.py";
        private const string Environmant      = "MDS_FWDIR";
        private const string ClishPath        = "/bin/clish";

        #endregion

        #region Private Members

        /// <summary>
        ///port on the management server
        /// </summary>
        private int _port;

        /// <summary>
        ///set to true if the port entered by the user
        /// </summary>
        private bool _isUserEnterPort;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="port">The port number that was given in {@link ApiClientArgs}</param>
        /// <param name="isUserEnterPort">True if the user entered the port number (not the default value)</param>
        internal ApiPortResolver( int port, bool isUserEnterPort)
        {
            _port = port;
            _isUserEnterPort = isUserEnterPort;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// This function returns the relevant port number according to the following:
        /// If a user entered a certain port this port will be returned, otherwise
        /// if the login is done to a local server, the port will be resolved by calling the script
        /// 'api_get_port.py' if the script exists on the server.
        /// If the script doesn't exist, it tries to resolve the port by calling clish command.
        /// </summary>
        /// <param name="isRunningOnManagementServer">True if the login is to local server, otherwise false</param>
        /// <returns> The port number</returns>
        internal int GetPort(bool isRunningOnManagementServer)
        {
            // In case that user entered the port, or the login command is not running on the management server,
            // resolve the port from the arg
            if (_isUserEnterPort || !isRunningOnManagementServer)
            {
                return _port;
            }

            int resolvedPort;

            //In case of a default port and login as root, try resolve from the get_port script
            int? portResolvedFromScript = ResolvePortFromGetPortScript();

            //The correct way is to use the api_get_port script. However we want to handle the situation where
            //this library is used on the machine, that api_get_port script doesn't exist yet.
            if (portResolvedFromScript == null)
            {
                //Script doesn't exist try resolve the port from clish
                int? resolvePortFromClish = ResolvePortFromClish();

                if (resolvePortFromClish == null)
                {
                    //Failed resolving port from clish return the default port
                    return _port;
                }
                resolvedPort = resolvePortFromClish.Value;
            }
            else
            {
                resolvedPort = portResolvedFromScript.Value;
            }

            return resolvedPort;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function resolves the port from clish by running the command
        /// '/bin/clish -c 'show web ssl-port' -o 'structured'
        /// </summary>
        /// <returns>On success port number, otherwise null.</returns>
        private int? ResolvePortFromClish()
        {
            string clishPath = Path.Combine(ClishPath);
            if (!File.Exists(clishPath))
            {
                return null;
            }

            //Run the get_port script
            ProcessStartInfo psi = new ProcessStartInfo(ClishPath)
            {
                Arguments = "-c show web ssl-port -o structured",
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            Process exeProcess;
            try
            {
                using (exeProcess = Process.Start(psi))
                {
                    if (exeProcess == null)
                    {
                        throw new ApiClientException("Error: failed running the script");
                    }
                    exeProcess.WaitForExit();
                }

            }
            catch (IOException e)
            {
                throw new ApiClientException("Error occurred while running 'api_get_port.py'" + e.Message);
            }

            StreamReader reader = exeProcess.StandardOutput;
            if (reader == null)
            {
                throw new ApiClientException("Can't read API external port, input stream is undefined");
            }

            exeProcess.Close();

            string output = reader.ReadToEnd();

            string[] portNumber = output.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (portNumber.Length != 2)
            {
                return null;
            }

            string portString = portNumber[1];
            try
            {
                _port = int.Parse(portString);
            }
            catch (FormatException e)
            {
                throw new ApiClientException("Can't read API external port" + e.Message);
            }

            return _port;
        }

        /// <summary>
        /// This function resolves the port number using the api_get_port.py script
        /// </summary>
        /// <returns>On success port number, otherwise null.</returns>
        private int? ResolvePortFromGetPortScript()
        {
            string mdsFwdir = Environment.GetEnvironmentVariable(Environmant);
            //MDS_FWDIR environment variable not defined
            if (string.IsNullOrEmpty(mdsFwdir))
            {
                return null;
            }

            //find the get_port script
            string workingDirPath = Path.Combine(mdsFwdir, "Python", "bin");
            //Python working directory was not found at mdsFwdir/Python/bin
            if (!File.Exists(workingDirPath))
            {
                return null;
            }

            string pythonExec = Path.Combine(workingDirPath, "python");
            // Python executable was not found at mdsFwdir/Python/bin/python
            if (!File.Exists(pythonExec))
            {
                return null;
            }

            string getPortScript = Path.Combine(mdsFwdir, "scripts", ApiGetPortScript);
            //The script wasn't find
            if (!File.Exists(getPortScript))
            {
                return null;
            }

            return RunGetPortScript(pythonExec, getPortScript, workingDirPath);
        }

        /// <summary>
        /// This function runs the get port scripts and parse the result in order to get the port number
        /// </summary>
        /// <param name="pythonExec">Python executable path</param>
        /// <param name="getPortScript">Get port script path</param>
        /// <param name="workingDirPath">Working directory path</param>
        /// <returns>On success port number, otherwise null.</returns>
        private int? RunGetPortScript(string pythonExec, string getPortScript, string workingDirPath)
        {
            int? port;

            //Run the get_port script
            ProcessStartInfo psi = new ProcessStartInfo(pythonExec) { Arguments = getPortScript };
            psi.Arguments = "-f json";
            psi.WorkingDirectory = workingDirPath;
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;

            Process exeProcess;
            try
            {
                using (exeProcess = Process.Start(psi))
                {
                    if (exeProcess == null)
                    {
                        throw new ApiClientException("Error: failed running the script");
                    }
                    exeProcess.WaitForExit();
                }
            }
            catch (IOException e)
            {
                throw new ApiClientException("Error occurred while running 'api_get_port.py'" + e.Message);
            }

            StreamReader reader = exeProcess.StandardOutput;
            if (reader == null)
            {
                throw new ApiClientException("Can't read API external port, input stream is undefined");
            }

            exeProcess.Close();

            string output = reader.ReadToEnd();

            JObject json = JObject.Parse(output);
            string portString = (string)json["external_port"];

            try
            {
                port = int.Parse(portString);
            }
            catch (FormatException e)
            {
                throw new ApiClientException("Can't read API external port " + e.Message);
            }

            return port;
        }

        #endregion
    }
}
