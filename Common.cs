namespace CSForth;

public static class Common
{
    internal static bool IsUpper(this string s)
    {
        foreach (char c in s)
            if (!char.IsUpper(c)) return false;
        return true;
    }

    internal static bool TryOutInt(this string s, out int result)
    {
        try
        {
            int i = Convert.ToInt32(s);
            result = i;
            return true;
        }
        catch (FormatException) { }
        result = 0;
        return false;
    }

    internal static bool IsInt(this string s) => s.TryOutInt(out var _);

    internal static Dictionary<K, V> Copy<K, V>(this Dictionary<K, V> dict) where K : notnull
    {
        if (dict.Count == 0) return [];
        Dictionary<K, V> copy = [];
        foreach (var (k, v) in dict) copy.Add(k, v);
        return copy;
    }

    public class Ref<T>(T value)
    {
        private T value = value;
        private bool set = false;

        public T Value
        {
            get => value;
            set
            {
                this.value = value;
                set = true;
            }
        }
        public bool Set { get => set; }
    }
}