using System;
using Newtonsoft.Json.Linq;
using MgmtApi;

namespace AddHost
{
    class Program
    {
        #region Main Method

        /// <summary>
        /// Example of add host
        /// </summary>
        static void Main()
        {
            ApiClientArgs apiArgs = new ApiClientArgs { DebugFile = "debug.txt" };
            ApiClient client = new ApiClient(apiArgs);

            Console.WriteLine("Enter server ip or host name: ");
            string server = Console.ReadLine();

            try
            {
                ApiUtils.CheckFingerprint(client, server);
            }
            catch (ApiClientException)
            {
                ApiUtils.EndProgram();
            }

            Console.WriteLine("Enter username: ");
            string username = Console.ReadLine();

            Console.WriteLine("Enter password: ");
            string password = Console.ReadLine();

            // try to login
            ApiLoginResponse res = Login(client, server,username, password);

            Console.WriteLine("Enter a name for the new host object: ");
            string hostName = Console.ReadLine();
            Console.WriteLine("Enter host IP: ");
            string hostIp = Console.ReadLine();

            // make the API add-host call.
            ApiResponse addhostRes = client.ApiCall(res, "add-host", new JObject { { "name", hostName }, { "ip-address", hostIp } });

            if (addhostRes == null)
            {
                ApiUtils.WriteLineColored("Add-Host failed.", ConsoleColor.Red);
                Logout(res, client);
                ApiUtils.EndProgram();
            }

            if (!addhostRes.Success)
            {
                ApiUtils.WriteLineColored("Add-Host failed. Error: " + addhostRes.ErrorMessage, ConsoleColor.Red);
                Logout(res, client);
                ApiUtils.EndProgram();
            }

            ApiUtils.WriteLineColored("Add-Host succeeded.", ConsoleColor.Green);

            // try to publish changes and then logout.
            ApiResponse publishRes = client.ApiCall(res, "publish", "{}");
            if (!publishRes.Success)
            {
                ApiUtils.WriteLineColored("Publish failed. Error: " + publishRes.ErrorMessage, ConsoleColor.Red);
                Logout(res, client);
                ApiUtils.EndProgram();
            }

            ApiUtils.WriteLineColored("Publish succeeded.", ConsoleColor.Green);
            Logout(res, client);
            ApiUtils.EndProgram();
            Console.ReadKey();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function preforms a login to the server
        /// </summary>
        /// <param name="res">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="server">The IP address or name of the Check Point Management Server.</param>
        /// <param name="username">User name</param>
        /// <param name="password">Password</param>
        /// <returns></returns>
        private static ApiLoginResponse Login(ApiClient client, string server, string username, string password)
        {
            JObject payload = new JObject { { "user", username }, { "password", password } };

            ApiLoginResponse res = null;
            try
            {
                res = client.Login(server, payload);
            }
            catch (ApiClientException)
            {
                ApiUtils.WriteLineColored("Login failed.", ConsoleColor.Green);
                ApiUtils.EndProgram();
            }

            if (res == null)
            {
                ApiUtils.EndProgram();
            }

            if (res.Success)
            {
                ApiUtils.WriteLineColored("Login succeeded.", ConsoleColor.Green);
            }
            else
            {
                ApiUtils.WriteLineColored("Could not login. Error: " + res.ErrorMessage, ConsoleColor.Red);
                ApiUtils.EndProgram();
            }

            return res;
        }

        /// <summary>
        /// This function preforms a logout from the server
        /// </summary>
        /// <param name="res">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="clientToLogout"></param>
        private static void Logout(ApiLoginResponse res, ApiClient clientToLogout)
        {
            ApiResponse logoutRes = clientToLogout.ApiCall(res, "logout", "{}");
            if (logoutRes.Success)
            {
                ApiUtils.WriteLineColored("Logout succeeded.", ConsoleColor.Green);
            }
            else
            {
                ApiUtils.WriteLineColored("Logout failed. Error: " + logoutRes.ErrorMessage, ConsoleColor.Red);
            }
        }

        #endregion
    }
}
