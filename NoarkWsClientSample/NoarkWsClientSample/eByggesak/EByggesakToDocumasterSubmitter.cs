using CommandLine;
using Documaster.WebApi.Client.IDP;
using Documaster.WebApi.Client.IDP.Oauth2;
using Documaster.WebApi.Client.Noark5;
using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample.eByggesak
{
    public class EByggesakToDocumasterSubmitter
    {
        private const string EXTERNAL_SYSTEM = "eByggesak";

        private readonly DocumasterClients documasterClients;
        private string filePath;

        public EByggesakToDocumasterSubmitter(DocumasterClients documasterClients, string filePath)
        {
            this.documasterClients = documasterClients;
            this.filePath = filePath;
        }

        /// <summary>
        /// Submits data to Documaster. Searches for an existing case file by the eByggesak id or creates a new one.
        /// Creates a registry entry with document description and a document version.
        /// </summary>
        /// <param name="seriesTitle"></param>
        public void SubmitToDocumaster(string seriesTitle)
        {
            NoarkClient noarkClient = this.documasterClients.getNoarkClient();

            Arkivdel series = FindSeriesByTitle(seriesTitle);

            if (series == null)
            {
                Console.WriteLine($"Did not find series with title '{seriesTitle}'!");
                return;
            }

            Saksmappe caseFile = GetOrCreateCaseFile(series.Id, series.RefPrimaerKlassifikasjonssystem);

            Skjerming screeningCode = GetOrCreateScreeningCode("N1");

            Dokumenttype documentType = GetOrCreateDocumentType("Tilbud");

            // Note that all code list values can be fetched with a single request like this:
            // client.CodeLists();

            // Create a registry entry with a document description and upload a document:
            Journalpost registryEntry =
                CreateRegistryEntry(
                    caseFile.Id,
                    "Registry entry", 
                    "registryEntryExternalId", 
                    screeningCode,
                    documentType);
        }

        private Arkivdel FindSeriesByTitle(string title)
        {
            var noarkClient = documasterClients.getNoarkClient();

            QueryResponse<Arkivdel> queryResponse = noarkClient.Query<Arkivdel>("tittel=@title", 1)
                .AddQueryParam("@title", title)
                .Execute();

            if (!queryResponse.Results.Any())
            {
                return null;
            }

            if (queryResponse.HasMore)
            {
                Console.WriteLine($"Warning: found more than one series with title '{title}'!");
            }

            return queryResponse.Results.First();
        }

        private Saksmappe GetOrCreateCaseFile(string seriesId, string primaryClassificationSystemId)
        {
            // Search for an existing case file by the external id from eByggesak 
            Saksmappe caseFile = FindCaseFileByExternalId("caseFileExternalId", seriesId);

            if (caseFile == null)
            {
                //Setting a primary class and an organizational unit is required when creating a case file!

                AdministrativEnhet organizationalUnit = GetOrCreateOrganizationalUnit("organizationalUnitCode");

                Klasse primaryClass = GetOrCreateClass("01 Tilbud", "Tilbud om plass", primaryClassificationSystemId);

                caseFile = CreateCaseFile(
                    seriesId,
                    "Case file",
                    primaryClass.Id,
                    organizationalUnit,
                    "caseFileExternalId");
            }

            return caseFile;
        }

        private Saksmappe FindCaseFileByExternalId(string externalId, string seriesId)
        {
            var noarkClient = documasterClients.getNoarkClient();

            QueryResponse<Saksmappe> queryResponse =
                noarkClient.Query<Saksmappe>("refArkivdel.id=@seriesId && refEksternId.eksternID=@externalId", 1)
                    .AddQueryParam("@seriesId", seriesId)
                    .AddQueryParam("@externalId", externalId)
                    .Execute();

            if (!queryResponse.Results.Any())
            {
                return null;
            }

            if (queryResponse.HasMore)
            {
                Console.WriteLine($"Warning: found more than one case files with external ID '{externalId}'!");
            }

            return queryResponse.Results.First();
        }

        private Klasse GetOrCreateClass(string classId, string title, string classificationSystemId)
        {
            var noarkClient = documasterClients.getNoarkClient();

            QueryResponse<Klasse> queryResponse = noarkClient
                .Query<Klasse>("klasseIdent=@classId && refKlassifikasjonssystem.id=@classificationSystemId", 1)
                .AddQueryParam("@classId", classId)
                .AddQueryParam("@classificationSystemId", classificationSystemId)
                .Execute();

            if (queryResponse.Results.Any())
            {
                return queryResponse.Results.First();
            }
            else
            {
                Klasse klass = new Klasse(classId, title);
                TransactionResponse transactionResponse = noarkClient.Transaction()
                    .Save(klass)
                    .Link(klass.LinkKlassifikasjonssystem(classificationSystemId))
                    .Commit();

                return transactionResponse.Saved[klass.Id] as Klasse;
            }
        }

        private AdministrativEnhet GetOrCreateOrganizationalUnit(string organizationalUnitCode)
        {
            var noarkClient = documasterClients.getNoarkClient();

            CodeList organizationalUnitCodeList = noarkClient.CodeLists("Saksmappe", "administrativEnhet").First();

            var codeExists =
                organizationalUnitCodeList.Values.Exists(codeValue => codeValue.Code.Equals(organizationalUnitCode));

            if (!codeExists)
            {
                AdministrativEnhet newOrganizationalUnit =
                    new AdministrativEnhet(organizationalUnitCode, $"name for {organizationalUnitCode}");
                return noarkClient.PutCodeListValue(newOrganizationalUnit);
            }
            else
            {
                return new AdministrativEnhet(organizationalUnitCode);
            }
        }

        private Dokumenttype GetOrCreateDocumentType(string documentType)
        {
            var noarkClient = documasterClients.getNoarkClient();

            CodeList documentTypeCodeList = noarkClient.CodeLists("Dokument", "dokumenttype").First();

            var codeExists =
                documentTypeCodeList.Values.Exists(codeValue => codeValue.Code.Equals(documentType));

            if (!codeExists)
            {
                Dokumenttype newDocumentType =
                    new Dokumenttype(documentType, $"name for {documentType}");
                return noarkClient.PutCodeListValue(newDocumentType);
            }
            else
            {
                return new Dokumenttype(documentType);
            }
        }

        private Skjerming GetOrCreateScreeningCode(string screeningCode)
        {
            var noarkClient = documasterClients.getNoarkClient();

            CodeList screeningCodeList = noarkClient.CodeLists("Journalpost", "skjerming").First();

            var codeExists =
                screeningCodeList.Values.Exists(codeValue => codeValue.Code.Equals(screeningCode));

            if (!codeExists)
            {
                Skjerming newDocumentType =
                    new Skjerming(screeningCode, $"name for {screeningCode}");
                return noarkClient.PutCodeListValue(newDocumentType);
            }
            else
            {
                return new Skjerming(screeningCode);
            }
        }

        private Saksmappe CreateCaseFile(
            string seriesId,
            string caseFileTitle,
            string primaryClassId,
            AdministrativEnhet organizationalUnit,
            string externalId)
        {
            var noarkClient = documasterClients.getNoarkClient();

            Saksmappe caseFile = new Saksmappe(caseFileTitle, organizationalUnit);

            EksternId extarnalIdObj = new EksternId(EXTERNAL_SYSTEM, externalId);

            // Commit all objects and link them
            TransactionResponse response = noarkClient.Transaction()
                .Save(caseFile)
                .Link(caseFile.LinkArkivdel(seriesId))
                .Link(caseFile.LinkPrimaerKlasse(primaryClassId))
                .Save(extarnalIdObj)
                .Link(extarnalIdObj.LinkMappe(caseFile))
                .Commit();

            // TransactionResponse is a dictionary and it's keys are the id's that are temporarily assigned to the objects when they are first initialized.
            // The values of the dictionary are the save objects with their permanent id's.

            return response.Saved[caseFile.Id] as Saksmappe;
        }

        private Journalpost CreateRegistryEntry(
            string caseFileId,
            string title,
            string externalId,
            Skjerming screeningCode,
            Dokumenttype documentType)
        {
            var noarkClient = documasterClients.getNoarkClient();

            Journalpost registryEntry = new Journalpost(title, Journalposttype.UTGAAENDE_DOKUMENT);
            registryEntry.Skjerming = screeningCode;

            // Link registry entry to an external id, a correspondence party and a sign off object.

            EksternId externalIdObj = new EksternId(EXTERNAL_SYSTEM, externalId);

            Korrespondansepart correspondenceParty =
                new Korrespondansepart(Korrespondanseparttype.AVSENDER, "John Smith");

            Avskrivning signoff = new Avskrivning(Avskrivningsmaate.TATT_TIL_ETTERRETNING);

            //Link the document descriptions to the registry entry as main document. (HOVEDDOKUMENT)
            //Subsequent document descriptions can be linked as attachments (VEDLEGG).

            Dokument documentDescription =
                new Dokument("Document description", TilknyttetRegistreringSom.HOVEDDOKUMENT);
            documentDescription.Dokumenttype = documentType;

            Dokumentfil uploadedFile = noarkClient.Upload(this.filePath);

            Dokumentversjon documentVersion = new Dokumentversjon(Variantformat.ARKIVFORMAT, ".pdf", uploadedFile);

            TransactionResponse transactionResponse = noarkClient.Transaction()
                .Save(registryEntry)
                .Link(registryEntry.LinkMappe(caseFileId))
                .Save(externalIdObj)
                .Link(externalIdObj.LinkRegistrering(registryEntry))
                .Save(correspondenceParty)
                .Link(correspondenceParty.LinkRegistrering(registryEntry))
                .Save(signoff)
                .Link(registryEntry.LinkAvskrivning(signoff))
                .Save(documentDescription)
                .Link(documentDescription.LinkRegistrering(registryEntry))
                .Save(documentVersion)
                .Link(documentVersion.LinkDokument(documentDescription))
                .Commit();

            return transactionResponse.Saved[registryEntry.Id] as Journalpost;
        }
    }
}
