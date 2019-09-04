using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample.eByggesak
{
    public class DocumasterToEByggesakSubmitter
    {
        private readonly DocumasterClients documasterClients;

        public DocumasterToEByggesakSubmitter(DocumasterClients documasterClients)
        {
            this.documasterClients = documasterClients;
        }

        /// <summary>
        /// Searches for registry entries created after fromDate in a series with title seriesTitle
        /// </summary>
        /// <returns>A dictionary. Each found registry entries is mapped to the list of its external id objects.</returns>
        public Dictionary<Journalpost, List<EksternId>> SearchForRegistryEntriesInSeries(DateTime fromDate, string seriesTitle)
        {
            Arkivdel series = FindSeriesByTitle(seriesTitle);
            return FetchRegistryEntries(fromDate, series.Id);
        }

        private Dictionary<Journalpost, List<EksternId>> FetchRegistryEntries(DateTime fromDate, string seriesId)
        {
            var noarkClient = documasterClients.getNoarkClient();

            // First build a paginated-query to fetch the required registry entries

            const int limit = 50;
            var offset = 0;
            var hasMore = true;
            Dictionary<string, Journalpost> regisitryEntriesById = new Dictionary<string, Journalpost>();

            while (hasMore)
            {
                QueryResponse<Journalpost> response =
                    noarkClient.Query<Journalpost>("refMappe.refArkivdel.id=@seriesId && opprettetDato=[@from:@to]", limit)
                        .AddQueryParam("@seriesId", seriesId)
                        .AddQueryParam("@from", fromDate)
                        .AddQueryParam("@to", DateTime.Now)
                        .SetOffset(offset)
                        .Execute();

                hasMore = response.HasMore;
                offset += limit;

                foreach(var registryEntry in response.Results)
                {
                    regisitryEntriesById.Add(registryEntry.Id, registryEntry);
                }
            }

            // Prepare the dictionary which is to be returned by the method.
            // We are going to find all external id objects related to each registry entry.
            var regisitryEntriesToExternalIds = new Dictionary<Journalpost, List<EksternId>>();
            foreach(var registryEntryId in regisitryEntriesById.Keys)
            {
                regisitryEntriesToExternalIds.Add(regisitryEntriesById[registryEntryId], new List<EksternId>());
            }

            // Build a query that finds the external id objects linked to the registry entries.

            if (regisitryEntriesById.Keys.Count > 0)
            {
                var queryStrings = new string[regisitryEntriesById.Keys.Count];
                var queryParams = new List<QueryParam>();

                for (var i = 0; i < regisitryEntriesById.Keys.Count; i++)
                {
                    var queryParamName = $"@regEntryId{i}";
                    queryParams.Add(new QueryParam(queryParamName, regisitryEntriesById.Keys.ElementAt(i)));
                    queryStrings[i] = $"refRegistrering.id={queryParamName}";
                }

                var queryString = string.Join(" || ", queryStrings);

                Query<EksternId> query = noarkClient.Query<EksternId>(queryString, limit);
                queryParams.ForEach(queryParam => query.AddQueryParams(queryParam));

                offset = 0;
                hasMore = true;
                while (hasMore)
                {
                    QueryResponse<EksternId> response = query.SetOffset(offset).Execute();
                    hasMore = response.HasMore;
                    offset += limit;

                    foreach (EksternId eksternId in response.Results)
                    {
                        var registryEntryId = eksternId.RefRegistrering;
                        Journalpost registryEntry = regisitryEntriesById[registryEntryId];
                        regisitryEntriesToExternalIds[registryEntry].Add(eksternId);
                    }
                }
            }

            return regisitryEntriesToExternalIds;
        }

        private Arkivdel FindSeriesByTitle(string title)
        {
            var noarkClient = this.documasterClients.getNoarkClient();

            QueryResponse<Arkivdel> queryResponse = noarkClient.Query<Arkivdel>("tittel=@title", 1)
                .AddQueryParam("@title", title)
                .Execute();

            if (!queryResponse.Results.Any())
            {
                return null;
            }

            if (queryResponse.HasMore)
            {
                Console.WriteLine($"Warning: Found more than one series with title '{title}'!");
            }

            return queryResponse.Results.First();
        }
    }
}
