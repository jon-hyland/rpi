using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rpi.Gpio.Output
{
    public static class StatsToHtml
    {
        /// <summary>
        /// Converts JSON text from 'getstats' command to human-friendly HTML.
        /// </summary>
        public static string GetStatsHtml(string json, string serviceName = null, string version = null)
        {
            dynamic data = JsonConvert.DeserializeObject(json);
            if (serviceName == null)
                serviceName = data.service.name;
            if (version == null)
                version = data.service.version;

            JObject root = null;
            JItem currentParent = null;

            using (StringReader stringReader = new StringReader(json))
            {
                using (JsonTextReader reader = new JsonTextReader(stringReader))
                {
                    bool done = false;
                    JsonToken token = JsonToken.Undefined;
                    object value = null;
                    JsonToken prevToken = JsonToken.Undefined;
                    object prevValue = null;

                    do
                    {
                        done = !reader.Read();
                        if (!done)
                        {
                            prevToken = token;
                            prevValue = value;
                            token = reader.TokenType;
                            value = reader.Value;

                            if (token == JsonToken.StartObject)
                            {
                                if (currentParent == null)
                                {
                                    root = new JObject(null, serviceName + " v" + version);
                                    currentParent = root;
                                }
                                else
                                {
                                    string name = prevToken == JsonToken.PropertyName ? prevValue.ToString() : "";
                                    JObject obj = new JObject(currentParent, name);
                                    currentParent.Items.Add(obj);
                                    currentParent = obj;
                                }
                            }

                            else if (token == JsonToken.EndObject)
                            {
                                currentParent = currentParent.Parent;
                            }

                            else if (token == JsonToken.StartArray)
                            {
                                string name = prevToken == JsonToken.PropertyName ? prevValue.ToString() : "";
                                JArray arr = new JArray(currentParent, name);
                                currentParent.Items.Add(arr);
                                currentParent = arr;
                            }

                            else if (token == JsonToken.EndArray)
                            {
                                currentParent = currentParent.Parent;
                            }

                            else if ((token == JsonToken.Boolean) || (token == JsonToken.Date) || (token == JsonToken.Float) || (token == JsonToken.Integer) || (token == JsonToken.String) || (token == JsonToken.Raw))
                            {
                                if (prevToken == JsonToken.PropertyName)
                                {
                                    string name = prevToken == JsonToken.PropertyName ? prevValue.ToString() : "";
                                    JProperty prop = new JProperty(currentParent, name, value);
                                    currentParent.Items.Add(prop);
                                }
                                else if (prevToken == JsonToken.StartArray)
                                {
                                    JObject val = new JObject(currentParent, value.ToString());
                                    currentParent.Items.Add(val);
                                }
                            }
                        }
                    }
                    while (!done);
                }
            }

            string body = root.Render(null);
            return HTML.Replace("##TITLE##", "").Replace("##BODY##", body);
        }

        private enum JItemType
        {
            Object,
            Array,
            Property
        }

        private class JItem
        {
            protected JItem _parent;
            protected string _name;
            protected JItemType _type;
            protected List<JItem> _items;

            public JItem Parent { get { return _parent; } }
            public string Name { get { return _name; } }
            public List<JItem> Items { get { return _items; } }

            public JItem(JItem parent, string name, JItemType type)
            {
                _parent = parent;
                _name = name;
                _type = type;
                _items = new List<JItem>();
            }

            public int Level
            {
                get
                {
                    JItem p = _parent;
                    int level = 0;
                    while (p != null)
                    {
                        level++;
                        p = p.Parent;
                    }
                    return level;
                }
            }

            public string Spacing
            {
                get
                {
                    string spacing = "";
                    for (int i = 0; i < Level; i++)
                        spacing += "    ";
                    return spacing;
                }
            }

            public string FullName
            {
                get
                {
                    JItem p = _parent;
                    string name = !String.IsNullOrEmpty(_name) ? _name : "";
                    while (p != null)
                    {
                        if ((p.Parent != null) && (!String.IsNullOrEmpty(p.Name)))
                        {
                            if (String.IsNullOrEmpty(name))
                                name = p.Name;
                            else
                                name = p.Name + "." + name;
                        }
                        p = p.Parent;
                    }
                    return name;
                }
            }

            public virtual string Render(int? Index)
            {
                return null;
            }

            public override string ToString()
            {
                return (_name ?? "");
            }
        }

        private class JObject : JItem
        {
            public JObject(JItem parent, string name)
                : base(parent, name, JItemType.Object)
            {
            }

            public override string Render(int? index)
            {
                string html = "";
                if (FullName == "tegraStats.cpu")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>percent</th><th class='mh w150'>temperature</th></tr>\r\n";

                    html += "<tr>";
                    foreach (JProperty p in _items.OfType<JProperty>())
                    {
                        html += "<td class='ml even'>" + p.Value + "</td>";
                    }
                    html += "</tr>\r\n";

                    foreach (JArray a in _items.OfType<JArray>())
                    {
                        html += a.Render(0);
                    }

                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "tegraStats.gpu")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>percent</th><th class='mh w150'>temperature</th><th class='mh w150'>frequency</th><th class='mh w150'>maxFrequency</th></tr>\r\n";

                    html += "<tr>";
                    foreach (JProperty p in _items.OfType<JProperty>())
                    {
                        html += "<td class='ml even'>" + p.Value + "</td>";
                    }
                    html += "</tr>\r\n";
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "tegraStats.emc")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>percent</th><th class='mh w150'>frequency</th><th class='mh w150'>maxFrequency</th></tr>\r\n";

                    html += "<tr>";
                    foreach (JProperty p in _items.OfType<JProperty>())
                    {
                        html += "<td class='ml even'>" + p.Value + "</td>";
                    }
                    html += "</tr>\r\n";
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "tegraStats.ram")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>used</th><th class='mh w150'>total</th><th class='mh w150'>percent</th></tr>\r\n";

                    html += "<tr>";
                    foreach (JProperty p in _items.OfType<JProperty>())
                    {
                        html += "<td class='ml even'>" + p.Value + "</td>";
                    }
                    html += "</tr>\r\n";
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else
                {
                    if (Level == 0)
                        html += Spacing + "<div class='object level-" + Level + "'>\r\n";
                    else
                        html += Spacing + "<tr><td class='object' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    if (!String.IsNullOrWhiteSpace(_name))
                        html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    for (int i = 0; i < _items.Count; i++)
                        html += _items[i].Render(i);
                    html += Spacing + "</table>\r\n";
                    if (Level == 0)
                        html += Spacing + "</div>\r\n";
                    else
                        html += Spacing + "</td></tr>\r\n";
                }
                return html;
            }
        }

        private class JArray : JItem
        {
            public JArray(JItem parent, string name)
                : base(parent, name, JItemType.Object)
            {
            }

            public override string Render(int? index)
            {
                string html = "";
                if (FullName == "serviceState.alerts")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w75'>type</th><th class='mh w400'>message</th><th class='mh w150'>time</th><th class='mh w75'>expiration</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? ((((Int32)(i++) % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if ((FullName == "serviceStats.operations") || (FullName == "serviceStats.commands"))
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w250'>name</th><th class='mh w75'>count</th><th class='mh w75'>elapsedAvg</th><th class='mh w75'>elapsedMin</th><th class='mh w75'>elapsedMax</th><th class='mh w75'>elapsedSum</th><th class='mh w75'>cps</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? ((((Int32)(i++) % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "serviceStats.completedTasks")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w300'>name</th><th class='mh w200'>ips</th><th class='mh w200'>elapsed</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? ((((Int32)(i++) % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "serviceStats.runningTasks")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w300'>name</th><th class='mh w100'>ips</th><th class='mh w100'>percent</th><th class='mh w100'>eta</th><th class='mh w100'>elapsed</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? ((((Int32)(i++) % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "errorStats")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w50'>count</th><th class='mh w50'>cpm</th><th class='mh w75'>type</th><th class='mh w200'>message</th><th class='mh w200'>stackTrace</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? (((i++ % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }                          
                else if (FullName == "videoHandler.streams")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>name</th><th class='mh w75'>codec</th><th class='mh w75'>width</th><th class='mh w75'>height</th><th class='mh w75'>quality</th><th class='mh w75'>port</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? (((i++ % 2) == 0) ? "even" : "odd") : "";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else if (FullName == "tegraStats.cpu.cores")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += Spacing + "<tr><th class='mh w150'>index</th><th class='mh w150'>percent</th><th class='mh w150'>frequency</th><th class='mh w150'>maxFrequency</th></tr>\r\n";
                    int i = 0;
                    foreach (JObject o in Items)
                    {
                        html += "<tr>";
                        string c = index != null ? (((i++ % 2) == 0) ? "even" : "odd") : "";
                        html += "<td class='ml " + c + "'>" + i + "</td>";
                        foreach (JProperty p in o.Items)
                        {
                            html += "<td class='ml " + c + "'>" + p.Value + "</td>";
                        }
                        html += "</tr>\r\n";
                    }
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else
                {
                    if (Level == 0)
                        html += Spacing + "<div class='array level-" + Level + "'>\r\n";
                    else
                        html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    if (!String.IsNullOrWhiteSpace(_name))
                        html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    for (int i = 0; i < _items.Count; i++)
                        html += _items[i].Render(i);
                    html += Spacing + "</table>\r\n";
                    if (Level == 0)
                        html += Spacing + "</div>\r\n";
                    else
                        html += Spacing + "</td></tr>\r\n";
                }
                return html;
            }
        }

        private class JProperty : JItem
        {
            private readonly object _value;

            public object Value { get { return _value; } }

            public JProperty(JItem parent, string name, object value)
                : base(parent, name, JItemType.Property)
            {
                _value = value;
            }

            public override string Render(int? Index)
            {
                string html = "";
                if (FullName == "tegraStats.line")
                {
                    html += Spacing + "<tr><td class='array' colspan='100'>\r\n";
                    html += Spacing + "<table class='table level-" + Level + "'>\r\n";
                    html += Spacing + "<tr><th colspan='100' class='header'>" + _name + "</th></tr>\r\n";
                    html += "<tr>";
                    html += "<td class='ml even'>" + _value + "</td>";
                    html += "</tr>\r\n";
                    html += Spacing + "</table>\r\n";
                    html += Spacing + "</td></tr>\r\n";
                }
                else
                {
                    string c = Index != null ? ((((int)Index % 2) == 0) ? "even" : "odd") : "";
                    html += Spacing + "<tr class='" + c + "'><td class='name'>" + _name + "</td><td class='value'>" + _value + "</td></tr>\r\n";
                }

                return html;
            }
        }

        private static readonly string HTML = @"<!DOCTYPE html>
<html>
<head>
<title>##TITLE##</title>
<style>    
html { font-size:100%; }
body { font-size:75%; color:#222; background:#333; font-family:'Helvetica Neue', Arial, Helvetica, sans-serif; }
.page { width:900px; margin: auto; background:#fff; }
.object {  }
.array {  }
.table { width:100%; }
.table .even { background:#eee; }
.table .odd { background:#ddd; }
.table .name { width:25%; padding:3px; }
.table .value { width:75%; padding:3px; }
.level-0 { margin: auto; padding-top:8px; padding-left: 10px; padding-right:10px; padding-bottom: 8px; }
.level-0 .header { font-size:150%; background:#000; color:#fff; text-align:left; padding:5px; }
.level-1 { margin-top:10px; outline:1px solid black; }
.level-1 .header { font-size:135%; background:#333; color:#fff text-align:left; }
.level-2 { margin-top:0px; }
.level-2 .header { font-size:120%; background:#555; color:#fff text-align:left; }
.boldname { font-weight:bold; }
.mh { font-size:100%; background:#aaa; color:#222; text-align:left; padding:4px; }
.ml { padding:3px; }
.w50 { width:50px; }
.w75 { width:75px; }
.w100 { width:100px; }
.w125 { width:125px; }
.w150 { width:150px; }
.w175 { width:175px; }
.w200 { width:200px; }
.w225 { width:225px; }
.w250 { width:250px; }
.w275 { width:275px; }
.w300 { width:300px; }
.w325 { width:325px; }
.w350 { width:350px; }
.w375 { width:375px; }
.w400 { width:400px; }
.w425 { width:425px; }
.w450 { width:450px; }
.w475 { width:475px; }
.w500 { width:500px; }

</style>
</head>
<body>
<div class='page'>
##BODY##
</div>
</body>
</html>";

    }
}
