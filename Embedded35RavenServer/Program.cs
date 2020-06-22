using Raven.Client.Embedded;
using System;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

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
                FileName = "Select Directory",
            };
            var openFileResult = openFile.ShowDialog();

            if(openFileResult != DialogResult.OK)
            {
                Console.WriteLine("Cancelled");
                return;
            }

            var directory = Path.GetDirectoryName(openFile.FileName);

            if(!Directory.Exists(directory) || !File.Exists(Path.Combine(directory, "Data")))
            {
                Console.WriteLine("Not a RavenDB database. Exiting.");
                return;
            }

            Console.Write("Enter port / click enter for 8080: ");
            if(!int.TryParse(Console.ReadLine(), out int port))
            {
                port = 8080;
            }

            var documentStore = new EmbeddableDocumentStore();
            documentStore.DataDirectory = directory;
            documentStore.Configuration.CompiledIndexCacheDirectory = directory;
            documentStore.UseEmbeddedHttpServer = true;
            documentStore.EnlistInDistributedTransactions = false;
            documentStore.Configuration.Settings["Raven/License"] = ReadLicense();
            documentStore.Configuration.Settings["Raven/AnonymousAccess"] = "Admin";
            documentStore.Configuration.Settings["Raven/Licensing/AllowAdminAnonymousAccessForCommercialUse"] = "true";
            documentStore.Configuration.DisableClusterDiscovery = true;
            documentStore.Configuration.ResetIndexOnUncleanShutdown = true;
            documentStore.Configuration.Port = port;
            documentStore.Configuration.HostName = "localhost";
            documentStore.Conventions.SaveEnumsAsIntegers = true;
            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(Program).Assembly));

            try
            {
                using (documentStore.Initialize())
                {

                    Console.WriteLine($"Database initialized. Serving at http://localhost:{port}");
                    Console.WriteLine("Press enter to exit...");
                    Console.ReadLine();
                    Console.WriteLine("Shutting down...");
                }
            }
            catch(TargetInvocationException tie)
            {
                if (tie.InnerException is HttpListenerException)
                {
                    Console.WriteLine("HttpListenerException == Probably need a URLACL.");
                    Console.WriteLine("Enter this from an elevated prompt:");
                    Console.WriteLine();
                    Console.WriteLine($"   netsh http add urlacl url=http://*:{port}/ user=\"{Environment.UserName}\"");
                }
                throw;
            }
            catch(Exception x)
            {
                documentStore.Dispose();

                Console.WriteLine();
                Console.WriteLine(x);
                Console.WriteLine();
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();

            }
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(Program).Assembly.GetManifestResourceStream("EmbeddedRavenServer.RavenLicense.xml"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
