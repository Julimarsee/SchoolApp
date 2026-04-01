using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp2
{
    public static class SaveData
    {
        public static string role;
        public static string name;
        public static string id;
        public static List<string> subjects { get; set; } = new List<string>();
        public static string login;
        public static string password;
        public static string currentSub;
        public static double currentWidth;

        public static bool isChange = false;
        public static bool isNewProfile;
    }
}
