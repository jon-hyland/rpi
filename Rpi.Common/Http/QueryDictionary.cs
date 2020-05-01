using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rpi.Common.Http
{
    /// <summary>
    /// Similar to HttpListenerRequest.QueryString object, but values can be written to.
    /// </summary>
    public class QueryDictionary
    {
        //private
        private readonly IDictionary<string, string[]> _dictionary = null;

        /// <summary>
        /// Class constructor.
        /// </summary>
        public QueryDictionary()
        {
            _dictionary = new Dictionary<string, string[]>();
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public QueryDictionary(HttpContext context)
        {
            string query = context.Request.QueryString.ToString();
            _dictionary = ParseValues(query);
        }

        /// <summary>
        /// Class constructor.
        /// </summary>
        public QueryDictionary(string query)
        {
            _dictionary = ParseValues(query);
        }

        /// <summary>
        /// Parses values from specificed URL into new collection.
        /// </summary>
        private IDictionary<string, string[]> ParseValues(string query)
        {
            Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(query);
            IDictionary<string, string[]> dictionary = parsed.ToDictionary(x => x.Key, x => x.Value.ToArray());
            return dictionary;
        }

        /// <summary>
        /// Returns first value of matching key, or null if doesn't exist.
        /// </summary>
        public string Get(string key)
        {
            if ((_dictionary.ContainsKey(key)) && (_dictionary[key].Length > 0))
                return _dictionary[key][0];
            return null;
        }

        /// <summary>
        /// Returns copy of all values of matching key, or null if doesn't exist.
        /// </summary>
        public string[] GetAll(string key)
        {
            if ((_dictionary.ContainsKey(key)) && (_dictionary[key].Length > 0))
                return _dictionary[key].ToArray();
            return null;
        }

        /// <summary>
        /// Returns true if specified key exists in colletion.
        /// </summary>
        public bool Exists(string key)
        {
            return _dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Returns a copy of the dictionary.
        /// </summary>
        public IDictionary<string, string[]> GetAll()
        {
            return _dictionary.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Returns query in '?item1=value1&item2=value2&item3=value3a,value3b' format.
        /// </summary>
        public override string ToString()
        {
            string str = String.Empty;
            if (_dictionary.Keys.Count > 0)
            {
                foreach (string key in _dictionary.Keys)
                {
                    str += String.IsNullOrEmpty(str) ? $"?={key}" : $"&={key}";
                    int count = 0;
                    foreach (string value in _dictionary[key])
                    {
                        if (count++ == 0)
                            str += value;
                        else
                            str += $",{value}";
                    }
                }
            }
            return str;
        }

    }
}
