using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample.eByggesak
{
    public class EByggesakIntegrationSample
    {
        private readonly DocumasterToEByggesakSubmitter eByggesakSubmitter;
        private readonly EByggesakToDocumasterSubmitter documasterSubmitter;

        public EByggesakIntegrationSample(Options options)
        {
            DocumasterClients documasterClients = new DocumasterClients(options);
            this.eByggesakSubmitter = new DocumasterToEByggesakSubmitter(documasterClients);
            this.documasterSubmitter = new EByggesakToDocumasterSubmitter(documasterClients, options.TestDoc);
        }

        public void ExecuteSample()
        {
            var seriesTitle = "eByggesak";
            this.documasterSubmitter.SubmitToDocumaster(seriesTitle);
            //var registryEntries =
            //    this.eByggesakSubmitter.SearchForRegistryEntriesInSeries(DateTime.Now.AddDays(-5), seriesTitle);
        }
    }
}
