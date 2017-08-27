using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using MgmtApi;

namespace FindDisabledRules
{
    class Program
    {
        #region Main Method

        /// <summary>
        /// Example of find disabled rules
        /// </summary>
        static void Main()
        {
            //Asks The user for the arguments
            Console.WriteLine("Enter server IP Address or host name (for a multi-domain environment, provide the IP address of the MDS): ");
            string server = Console.ReadLine();

            Console.WriteLine("Enter user name: ");
            string username = Console.ReadLine();

            Console.WriteLine("Enter password: ");
            string password = GetPassword();

            Console.WriteLine("Domain name (leave empty for a SmartCenter environment): ");
            string domain = Console.ReadLine();
            
            Console.WriteLine("Policy package name (default value is 'Standard'): ");
            string package = Console.ReadLine();

            if (string.IsNullOrEmpty(package)) package = "standard";

            ApiClient client = new ApiClient();

            //Validity Fingerprint
            try
            {
                ApiUtils.CheckFingerprint(client, server);
            }
            catch (ApiClientException)
            {     
                EndProgram(client, null, false);
            }

            JObject payload = new JObject
            {
                {"user", username},
                {"password", password},
                {"domain", domain},
                {"continue-last-session", false},
                {"read-only", true}
            };

            //Login to Management server
            ApiLoginResponse loginRes = null;

            try
            {
                loginRes = client.Login(server, payload);
            }
            catch (ApiClientException)
            {
                ApiUtils.WriteLineColored("Login failed.", ConsoleColor.Green);
                EndProgram(client, loginRes, false);
            }

            if (loginRes == null)
            {
                ApiUtils.WriteLineColored("Login failed.", ConsoleColor.Green);
                EndProgram(client, loginRes, false);
            }

            if (!loginRes.Success)
            {
                ApiUtils.WriteLineColored(loginRes.ErrorMessage, ConsoleColor.Red);
                EndProgram(client, loginRes, false);
            }

            //Go over all the access layer in the given package and  print all the rules and there status
            Dictionary<string, string> layers = GetAccessLayers(client, loginRes, package);
            foreach (string layerUid in layers.Keys)
            {
                Console.WriteLine("Layer name: " + layers[layerUid]);
                List<RuleInfo> rules = GetRules(client, loginRes, layerUid);
                foreach (RuleInfo rule in rules)
                {
                    Console.WriteLine(rule.ToString());
 
                }
                Console.WriteLine("");
            }
            Console.WriteLine("done.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function collects the information about all the rules in a given layer unique identifier
        /// </summary>
        /// <param name="client"> The <see cref="ApiClient"/>. </param>
        /// <param name="loginRes">The <see cref="ApiLoginResponse"/> the response of the login command. </param>
        /// <param name="layerUid"> The layer unique identifier</param>
        /// <returns> List contains all the rules information </returns>
        private static List<RuleInfo> GetRules(ApiClient client, ApiLoginResponse loginRes, string layerUid)
        {
            List<RuleInfo> res = new List<RuleInfo>();
            int limit     = 5;
            int iteraions = 0;
            bool done     = false;

            while (!done)
            {
                int offset = limit * iteraions;

                JObject payloadData = new JObject
                {
                    {"uid", layerUid},
                    {"details-level", "full"},
                    {"offset", offset.ToString()},
                    {"limit", limit.ToString()},
                };

                ApiResponse layerData = client.ApiCall(loginRes, "show-access-rulebase", payloadData);

                if (layerData.Success && layerData.Data["rulebase"] != null)
                {
                    //Go over all the access layer's rules
                    foreach (JToken ruleOrSection in layerData.Data["rulebase"])
                    {
                        if ((string)ruleOrSection["type"] == "access-rule")
                        {
                            RuleInfo r = new RuleInfo(ruleOrSection, (JArray)layerData.Data["objects-dictionary"]);
                            res.Add(r);
                        }
                        else if ((string)ruleOrSection["type"] == "access-section")
                        {
                            string sectionName = (string)ruleOrSection["name"];
                            foreach (var rule in ruleOrSection["rulebase"])
                            {
                                RuleInfo r = new RuleInfo(rule, (JArray)layerData.Data["objects-dictionary"], sectionName);
                                res.Add(r);
                            }
                        }
                    }

                    //check if we passed over all the rules
                    if (!layerData.Data["rulebase"].HasValues ||  layerData.Data["total"].ToString() == layerData.Data["to"].ToString())
                    {
                        done = true;
                    }
                }
                else
                {
                    done = true;
                    ApiUtils.WriteLineColored("Cannot read rulebase details", ConsoleColor.Red);
                }

                iteraions++;
            }

            return res;
        }

        /// <summary>
        /// This function collects all the layer's name and uid of a given policy package 
        /// </summary>
        /// <param name="client"> The <see cref="ApiClient"/>. </param>
        /// <param name="loginRes">The <see cref="ApiLoginResponse"/> the response of the login command. </param>
        /// <param name="packageName"> The package name  </param>
        /// <returns>Dictionary contains the tuples (layer uid, Layer name)</returns>
        private static Dictionary<string, string> GetAccessLayers(ApiClient client, ApiLoginResponse loginRes, string packageName)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();

            JObject payloadData = new JObject
            {
                {"name", packageName},
                {"details-level", "full"}
            };

            //Call shoe-package
            ApiResponse pkgData = client.ApiCall(loginRes, "show-package", payloadData);

            //Collect the uid and name of the layers
            if (pkgData.Success)
            {
                foreach (var layer in pkgData.Data["access-layers"])
                {
                    res.Add((string) layer["uid"], (string) layer["name"]);
                }
            }
            else
            {
                ApiUtils.WriteLineColored("Cannot read policy package details", ConsoleColor.Red);
                EndProgram(client, loginRes, true);
            }

            return res;
        }

        /// <summary>
        /// This function preforms logout from the client and exit the program.
        /// </summary>
        /// <param name="client"> The <see cref="ApiClient"/>. </param>
        /// <param name="loginResponse">The <see cref="ApiLoginResponse"/> - the response of the login command.</param>
        /// <param name="logout">If set to true logout need to be done.</param>
        private static void EndProgram(ApiClient client, ApiLoginResponse loginResponse, bool logout)
        {
            if (logout)
            {
                client.Exit(loginResponse);
            }
            Console.WriteLine("Press any key to exit..");
            Console.ReadKey();
            Environment.Exit(0);
        }

        /// <summary>
        /// This function replaces the password sting on the screen with * 
        /// </summary>
        /// <returns>The password in shape of *</returns>
        private static string GetPassword()
        {
            string pwd= string.Empty;
            while (true)
            {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (i.Key == ConsoleKey.Backspace)
                {
                    if (pwd.Length > 0)
                    {
                        pwd = pwd.Remove(pwd.Length - 1,1);
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    pwd += i.KeyChar ;
                    Console.Write("*");
                }
            }
            return pwd;
        }

        #endregion
    }
}
