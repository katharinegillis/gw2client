using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Text.RegularExpressions;

namespace MistUtilsClient
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Validate if a session id came in to the application.
            string[] args = Environment.GetCommandLineArgs();
            string sessionId = ValidateArguments(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Gw2ClientForm(sessionId));
        }

        private static string ValidateArguments(string[] args)
        {
            if (args.Length == 0 || args.Length == 1)
            {
                return null;
            }

            string sessionId = args[1].Replace("tichigw2client:", string.Empty).Replace("/", string.Empty);
            Regex pattern = new Regex(@"^[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}$", RegexOptions.IgnoreCase);
            if (pattern.IsMatch(sessionId))
            {
                return sessionId;
            }

            return null;
        }
    }
}
