namespace QuickInfoPlus.Core
{
    public static class CrefFormatter
    {
        public static string ToDisplayName(string cref)
        {
            var name = cref;
            if (name.Length > 1 && name[1] == ':')
            {
                name = name.Substring(2);
            }

            var parenthesis = name.IndexOf('(');
            if (parenthesis >= 0)
            {
                name = name.Substring(0, parenthesis);
            }

            var segments = name.Split('.');
            var last = segments[segments.Length - 1];
            if (last == "#ctor" && segments.Length > 1)
            {
                last = segments[segments.Length - 2];
            }

            var backtick = last.IndexOf('`');
            return backtick >= 0 ? last.Substring(0, backtick) : last;
        }
    }
}
