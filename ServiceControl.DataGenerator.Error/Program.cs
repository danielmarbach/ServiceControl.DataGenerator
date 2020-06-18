using System;
using System.Threading.Tasks;
using NServiceBus;

namespace Error
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var endpointConfiguration = new EndpointConfiguration("servicecontrol-datagenerator-error");
            endpointConfiguration.SendOnly();

            var transport = endpointConfiguration.UseTransport<MsmqTransport>();
            transport.DisablePublishing();

            endpointConfiguration.EnableFeature<ErrorGenerator>();

            var endpoint = await Endpoint.Start(endpointConfiguration);

            await Console.Error.WriteLineAsync("Press any key to exit.");
            Console.ReadLine();

            await endpoint.Stop();
        }
    }
}
