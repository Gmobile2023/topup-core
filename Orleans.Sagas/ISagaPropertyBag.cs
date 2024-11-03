namespace Orleans.Sagas
{
    public interface ISagaPropertyBag
    {
        void Add<T>(string key, T value);
        T Get<T>(string key);
        bool ContainsKey(string key);
        bool Remove<T>(string key, out T value);
    }
}