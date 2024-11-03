using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Orleans.Sagas
{
    [GenerateSerializer]
    class SagaPropertyBag : ISagaPropertyBag
    {
        [Id(0)]
        private readonly Dictionary<string, string> _existingProperties;

        [Id(1)]
        public Dictionary<string, string> ContextProperties { get; }

        public SagaPropertyBag() : this(new Dictionary<string, string>())
        {
        }

        public SagaPropertyBag(Dictionary<string, string> existingProperties)
        {
            this._existingProperties = existingProperties;
            ContextProperties = new Dictionary<string, string>();
        }

        public void Add<T>(string key, T value)
        {
            if (typeof(T) == typeof(string))
            {
                ContextProperties[key] = (string)(dynamic)value;
                return;
            }

            ContextProperties[key] = JsonConvert.SerializeObject(value);
        }

        public T Get<T>(string key)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(dynamic)_existingProperties[key];
            }

            return JsonConvert.DeserializeObject<T>(_existingProperties[key]);
        }

        public bool ContainsKey(string key)
        {
            return _existingProperties.ContainsKey(key);
        }

        public bool Remove<T>(string key, out T value)
        {
            if (typeof(T) == typeof(string))
            {
                var result = _existingProperties.Remove(key, out string v);
                value = (T)(dynamic)v;
                return result;
            }
            
            var r = _existingProperties.Remove(key, out string v1);
            value = JsonConvert.DeserializeObject<T>(v1);
            return r;
        }
    }
}
