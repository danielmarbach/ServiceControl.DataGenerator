using System;
using System.Threading.Tasks;
using NServiceBus;

namespace ServiceControl.DataGenerator.Audit
{
    class Program
    {
        static async Task Main()
        {
            var config = new EndpointConfiguration("servicecontrol-datagenerator-audit");
            config.SendOnly();

            var transport = config.UseTransport<MsmqTransport>();
            transport.DisablePublishing();

            config.EnableFeature<AuditGenerator>();

            var endpoint = await Endpoint.Start(config);

            await Console.Error.WriteLineAsync("Press any key to exit.");
            Console.ReadKey();

            await endpoint.Stop();
        }
    }
}
