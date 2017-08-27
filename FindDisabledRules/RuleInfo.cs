using System;
using Newtonsoft.Json.Linq;

namespace FindDisabledRules
{
    internal class RuleInfo
    {
        #region Private Members

        private int _ruleNumber;
        private DateTime? _expirationDate;
        private bool _enabled       = true;
        private bool _active        = true;
        private string _sectionName = string.Empty;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        public RuleInfo(JToken obj, JArray dictionary)
        {
            Init(obj, dictionary, "-");
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public RuleInfo(JToken obj, JArray dictionary, string section)
        {
            Init(obj, dictionary, section);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// This function returns a string representing a rule and its expiration status
        /// </summary>
        /// <returns>The string represent the rule</returns>
        public override string ToString()
        {
            string status = null;

            if (!_enabled)
            {
                status = "Disabled";
            }
            else if (_expirationDate != null)
            {
                status = string.Format("Expired on {0}", _expirationDate.Value.ToShortDateString());
            }
            else if (_active)
            {
                status = "Enabled";
            }

            return string.Format("Rule number {0} (section '{1}') : {2}", _ruleNumber, _sectionName, status);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function searches an object in a given dictionary which has uid equals to that supplied.
        /// </summary>
        /// <param name="uid">The uid</param>
        /// <param name="dictionary">The dictionary</param>
        /// <returns>The object with the same uid as the given uid </returns>
        private JToken GetObjectFromDictionary(string uid, JArray dictionary)
        {
            foreach (var obj in dictionary)
            {
                if ((string)obj["uid"] == uid) return obj;
            }
            return null;
        }

        /// <summary>
        /// This function checks the expiration date of a given object
        /// </summary>
        /// <param name="timeObject">The time object need to be checked</param>
        /// <returns>The expiration date of the object, null in case the object relevant forever </returns>
        private DateTime? CheckExpirationDateForObject(JToken timeObject)
        {
            string objectName = (string)timeObject["name"];
            if (objectName.ToLower() == "any") return null;
            if (bool.Parse((string)timeObject["end-never"])) return null;
            return DateTime.Parse((string)timeObject["end"]["iso-8601"]);
        }

        /// <summary>
        /// This function checks the expiration date of a given rule
        /// </summary>
        /// <param name="timeObjects">The time object from the rule object</param>
        /// <param name="dictionary">The dictionary of the rule </param>
        /// <returns>The expiration date of the rule or null in case of rule without an expiration date</returns>
        private DateTime? CheckExpirationDateForRule(JArray timeObjects, JArray dictionary)
        {
            DateTime? res = null;

            foreach (JToken uid in timeObjects)
            {
                JToken timeObj = GetObjectFromDictionary((string)uid, dictionary);
                DateTime? expDate = CheckExpirationDateForObject(timeObj);
                if (expDate == null)
                {
                    continue;
                }
                if (res == null)
                {
                    res = expDate;
                }
                else if (expDate.Value.Ticks > res.Value.Ticks)
                {
                    res = expDate;
                }
            }

            return res;
        }

        /// <summary>
        /// This method updates the field with the rule's information.
        /// </summary>
        /// <param name="obj">The rule object</param>
        /// <param name="dictionary">The object dictionary of the given rule.</param>
        /// <param name="section">The rule's section name</param>
        private void Init(JToken obj, JArray dictionary, string section)
        {
            _sectionName = section;
            _ruleNumber  = int.Parse(obj["rule-number"].ToString());
            _enabled     = bool.Parse(obj["enabled"].ToString());

            if (!_enabled)
            {
                _active = false;
            }

            JArray timeObjects = (JArray)obj["time"];
            _expirationDate = CheckExpirationDateForRule(timeObjects, dictionary);

            if (_expirationDate != null && _expirationDate.Value < DateTime.Now)
            {
                _active = false;
            }
        }

        #endregion
    }
}
