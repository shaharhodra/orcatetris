using System.Linq;

namespace Modesto.UniToDo
{
    /// <summary>
    /// Utils class containing useful methods
    /// </summary>
    public static class Utils
    {
        private static System.Random random = new System.Random();
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static string GetCode(int length)
        {
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
