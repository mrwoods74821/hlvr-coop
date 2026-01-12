using HLVRCoOpModCore;
using Microsoft.Win32;
using System;
using System.Windows.Forms;
using System.Configuration;

namespace HLVRCoOpMod
{
    internal static class Program
    {
        public static readonly UserInterface userInterface = new(); // WORKAROUND SPECIFICALLY FOR LUA
        const string UriScheme = "hlvrcoop";
        const string FriendlyName = "HLVR Co-Op";
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);

            RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme);
            RegistryKey defaultIcon = key.CreateSubKey("DefaultIcon");
            RegistryKey commandKey = key.CreateSubKey(@"shell\open\command");
            string applicationLocation = Application.ExecutablePath;

            key.SetValue("", "URL:" + FriendlyName);
            key.SetValue("URL Protocol", "");
            commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
            defaultIcon.SetValue("", applicationLocation + ",1");

            if (args.Length > 0)
            {
                if (Uri.TryCreate(args[0], UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, UriScheme, StringComparison.OrdinalIgnoreCase))
                {
                    userInterface.LogToOutputGeneric("Received the following URI scheme:", uri);
                    userInterface.UseUriScheme(uri);
                }
                else
                {
                    userInterface.LogToOutputGeneric("Received the following command line arguments:", string.Join(" ", args));
                    userInterface.UseArgs(args);
                }
            }
            Application.Run(userInterface);
        }
    }
}