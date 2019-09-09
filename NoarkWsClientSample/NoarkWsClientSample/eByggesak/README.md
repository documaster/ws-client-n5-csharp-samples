## Documaster-eByggesak integration sample code

This sample code uses the Documaster's NoarkClient (a client library available through NuGet) to demonstrate searching for and submitting data in a Documaster instance.  
The **EByggesakToDocumasterSubmitter** class shows how to search for a series and a case file, how to create a new case file with a registry entry and a document in it.  
The **DocumasterToEByggesakSubmitter** class shows how to search for registry entries created in the last N days and check their external id's.  

### Setup instructions

The sample is contained in a .NET 4.7 Console application. You need to restore the NuGet packages in this project to be able to compile it.
The Main method in Program.cs runs both the generic Noark sample as well as the eByggesak sample code. Feel free to comment or delete the lines that run the generic sample.  

The following arguments are required:

* --idpaddr: The address of the identity provider services for the particular RMS instances (ex: "http://client.documaster.tech/idp/oauth2/" 
* --clientid: The OAuth client_id value.
* --clientsecret: The OAuth client_secret value.
* --username: The RMS username.
* --password The RMS user password.
* --addr: The address of Documaster's web services (ex: "http://client.documaster.tech:8083")
* --testdoc A valid path to a test file to be uploaded to the RMS. 

The following arguments are optional:

* --cert: Path to a client .p12 certificate file if authentication in the RMS is configured to require certificates
* --certpass: Certificate password.

### Requirements

To run this sample code you need a Documaster's RMS installation with version 2.11.0 or higher. In the sample, we authenticate to the RMS with the Oauth2HttpClient which is another public NuGet client library.
The Oauth2HttpClient is a small OAuth2 client and we use it to obtain an authentication token from Documaster's own Identity Provider services. By default, the token expires in 60 minutes and
needs to refreshed after that. This is why the NoarkClient and the Oauth2HttpClient instances in this sample are kept in a separate class called
DocumasterClients which is responsible for getting and refreshing the token.

This sample code expects to find an existing series with title  "eByggesak" in the RMS.

### Miscellaneous

Some of the values used for creating RMS objects are dummy strings and need to be replaced with real-world values.
Such strings are:

* the name of the series "eByggesak"
* the external id's of the case file and the registry entry - "caseFileExternalId" and "registryEntryExternalId"
* the class id and class name for the primary class of the case file - "01 Tilbud" and "Tilbud om plass" respectively
* the organizational unit code required for the creation of the case file - "organizationalUnitCode"
* the document type "Tilbud"
* the screening code "N1"


