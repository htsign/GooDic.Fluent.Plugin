using System;

namespace GooDic.Fluent.Plugin.Extensions
{
    public static class FuncExtensions
    {
        public static Func<T, bool> Not<T>(Func<T, bool> func)
        {
            return x => !func(x);
        }

        public static Func<T, bool> Negate<T>(this Func<T, bool> func) => Not(func);
    }
}
