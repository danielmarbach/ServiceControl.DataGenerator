using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations.Expiration;
using Raven.Embedded;

namespace EmbeddedRavenServer
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Select RavenDB data directory from the open file dialog...");

            var openFile = new OpenFileDialog
            {
                Title = "Select Raven Database Directory",
                Multiselect = false,
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = false,
                FileName = "Select Directory"
            };
            var openFileResult = openFile.ShowDialog();

            if(openFileResult != DialogResult.OK)
            {
                Console.WriteLine("Cancelled");
                return;
            }

            var directory = Path.GetDirectoryName(openFile.FileName);

            var dbDirectory = Path.Combine(directory, "DB");
            Directory.CreateDirectory(dbDirectory);
            var logsDirectory = Path.Combine(directory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            if(!Directory.Exists(directory) || !Directory.Exists(dbDirectory) || !Directory.Exists(logsDirectory))
            {
                Console.WriteLine("Not a RavenDB database. Exiting.");
                return;
            }

            Console.Write("Enter port / click enter for 8080: ");
            if(!int.TryParse(Console.ReadLine(), out int port))
            {
                port = 8080;
            }

            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = dbDirectory,
                LogsPath = logsDirectory,
                ServerUrl = $"http://localhost:{port}",
                CommandLineArgs = new List<string>
                {
                    $"--License={CommandLineArgumentEscaper.EscapeSingleArg(ReadLicense())}",
                }
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
            var databaseOptions = new DatabaseOptions("Embedded")
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                },
                DatabaseRecord =
                {
                    Expiration = new ExpirationConfiguration
                    {
                        Disabled = false
                    }
                }
            };
            using (var documentStore = EmbeddedServer.Instance
                .GetDocumentStore(databaseOptions))
            {
                Console.WriteLine($"Database initialized. Serving at {serverOptions.ServerUrl}");
                Console.WriteLine("Press enter to exit...");

                EmbeddedServer.Instance.OpenStudioInBrowser();
                Console.ReadLine();
                Console.WriteLine("Shutting down...");

                documentStore.Dispose();

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
            }
        }


        public static string ReadLicense()
        {
            using (var resourceStream = typeof(Program).Assembly.GetManifestResourceStream("EmbeddedRavenServer.license.json"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
