using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace Rpi.Common.Json
{
    /// <summary>
    /// Designed as a simpler wrapper around the Newtonsoft.Json.JsonWriter.
    /// Allows for less lines of code (write property/value in single statement) as well as some formatting options.
    /// </summary>
    public class SimpleJsonWriter : IDisposable
    {
        //private
        private readonly string _filename = null;
        private readonly StreamWriter _streamWriter = null;
        private readonly StringBuilder _stringBuilder = null;
        private readonly StringWriter _stringWriter = null;
        private readonly JsonWriter _jsonWriter = null;
        private int _noBreakLevel = 0;

        //public
        public Formatting Formatting { get { return _jsonWriter.Formatting; } set { _jsonWriter.Formatting = value; } }

        /// <summary>
        /// Writes to the specified file, overwriting an existing file.
        /// </summary>
        public SimpleJsonWriter(string filename, bool indented = true)
        {
            _filename = filename;
            _streamWriter = new StreamWriter(_filename, false, new System.Text.UTF8Encoding(false));
            _stringBuilder = null;
            _stringWriter = null;
            _jsonWriter = new JsonTextWriter(_streamWriter)
            {
                Formatting = indented ? Formatting.Indented : Formatting.None
            };
        }

        /// <summary>
        /// Writes to the specified stream.
        /// </summary>
        public SimpleJsonWriter(Stream stream, bool indented = true)
        {
            _filename = null;
            _streamWriter = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            _stringBuilder = null;
            _stringWriter = null;
            _jsonWriter = new JsonTextWriter(_streamWriter)
            {
                Formatting = indented ? Formatting.Indented : Formatting.None
            };
        }

        /// <summary>
        /// Writes to the specified StringBuilder.
        /// </summary>
        public SimpleJsonWriter(StringBuilder stringBuilder, bool indented = true)
        {
            _filename = null;
            _streamWriter = null;
            _stringBuilder = stringBuilder;
            _stringWriter = new StringWriter(_stringBuilder);
            _jsonWriter = new JsonTextWriter(_stringWriter)
            {
                Formatting = indented ? Formatting.Indented : Formatting.None
            };
        }

        /// <summary>
        /// Writes the specified property name followed by the start of an object.
        /// </summary>
        public void WriteStartObject(string propertyName)
        {
            _jsonWriter.WritePropertyName(propertyName);
            _jsonWriter.WriteStartObject();
        }

        /// <summary>
        /// Writes the start of an object without a property identifier.
        /// </summary>
        public void WriteStartObject()
        {
            _jsonWriter.WriteStartObject();
        }

        /// <summary>
        /// Writes the end of an object.
        /// </summary>
        public void WriteEndObject()
        {
            _jsonWriter.WriteEndObject();
        }

        /// <summary>
        /// Writes the specified property name followed by the start of an array.
        /// </summary>
        public void WriteStartArray(string propertyName)
        {
            _jsonWriter.WritePropertyName(propertyName);
            _jsonWriter.WriteStartArray();
        }

        /// <summary>
        /// Writes the start of an array without a property identifier.
        /// </summary>
        public void WriteStartArray()
        {
            _jsonWriter.WriteStartArray();
        }

        /// <summary>
        /// Writes the end of an array.
        /// </summary>
        public void WriteEndArray()
        {
            _jsonWriter.WriteEndArray();
        }

        /// <summary>
        /// Writes the specified property name.
        /// </summary>
        public void WritePropertyName(string propertyName)
        {
            _jsonWriter.WritePropertyName(propertyName);
        }

        /// <summary>
        /// Write the specified object as a value.
        /// Only works with predefined simple types.
        /// </summary>
        public void WriteValue(object value)
        {
            _jsonWriter.WriteValue(value);
        }

        /// <summary>
        /// Writes the specified property name, followed by a simple value.
        /// Only works with predefined simple types.
        /// </summary>
        public void WritePropertyValue(string propertyName, object value)
        {
            _jsonWriter.WritePropertyName(propertyName);
            _jsonWriter.WriteValue(value);
        }

        /// <summary>
        /// Writes the specified JSON string with no regard to structure.
        /// </summary>
        public void WriteRaw(string json)
        {
            _jsonWriter.WriteRaw(json);
        }

        /// <summary>
        /// Writes the specified JSON value with little regard to structure.
        /// </summary>
        public void WriteRawValue(string json)
        {
            _jsonWriter.WriteRawValue(json);
        }

        /// <summary>
        /// Writes null.
        /// </summary>
        public void WriteNull()
        {
            _jsonWriter.WriteNull();
        }

        public void StartNoBreak()
        {
            if (_noBreakLevel == 0)
                _jsonWriter.Formatting = Formatting.None;
            _noBreakLevel++;
        }

        public void EndNoBreak()
        {
            _noBreakLevel--;
            if (_noBreakLevel < 0)
                _noBreakLevel = 0;
            if (_noBreakLevel == 0)
                _jsonWriter.Formatting = Formatting.Indented;
        }

        /// <summary>
        /// Signals the runtime to flush the underlying streams and write the data currently in the buffer.
        /// </summary>
        public void Flush()
        {
            _jsonWriter.Flush();
        }

        /// <summary>
        /// Closes and disposes of internal objects.
        /// </summary>
        public void Dispose()
        {
            _jsonWriter.Close();
            if (_streamWriter != null)
            {
                _streamWriter.Close();
                _streamWriter.Dispose();
            }
            if (_stringWriter != null)
            {
                _stringWriter.Close();
                _stringWriter.Dispose();
            }
        }
    }
}
