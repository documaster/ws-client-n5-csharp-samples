using CommandLine;
using Documaster.WebApi.Client.IDP;
using Documaster.WebApi.Client.IDP.Oauth2;
using Documaster.WebApi.Client.Noark5.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample
{
    public class DocumasterClients
    {
        protected NoarkClient noarkClient;
        protected Oauth2HttpClient idpClient;

        private string refreshToken;
        private DateTime acccessTokenExpirationTime;
        private Options opts;

        public DocumasterClients(Options opts)
        {
            this.opts = opts;

            /*
             * Using the Noark 5 web services requires providing a valid access token.
             * The way this token is obtained depends on the system implementing the services.
             * This sample code obtains the token from the Documaster's identity provider service
             * with the help of a designated Documaster IDP client.
             * If the Noark client is used in the context of an application that has access to a web browser,
             * we strongly recommend choosing the Oauth2 Authorization Code Grant Flow supported for obtaining
             * access tokens.
             */

            //Initialize an IDP client and request an authorization token
            InitIdpClient(opts);      

            //Initialize a Noark client
            InitClientWithoutClientCertificate(opts);

            //Notice that it is also possible to initialize а ssl-based Noark client by providing a client certificate:
            //InitClient(opts);
        }

        public NoarkClient getNoarkClient()
        {
            RefreshAccessToken();
            return noarkClient;
        }

        private void RefreshAccessToken()
        {
            //access token expires in 60 minutes

            if (refreshToken == null)
            {
                PasswordGrantTypeParams passwordGrantTypeParams = new PasswordGrantTypeParams(opts.ClientId, opts.ClientSecret, opts.Username, opts.Password, OpenIDConnectScope.OPENID);
                var accessTokenResponse = idpClient.GetTokenWithPasswordGrantType(passwordGrantTypeParams);
                acccessTokenExpirationTime = DateTime.Now.AddSeconds(accessTokenResponse.ExpiresInMs);
                refreshToken = accessTokenResponse.RefreshToken;

                noarkClient.AuthToken = accessTokenResponse.AccessToken;

            }
            else if (DateTime.Now > acccessTokenExpirationTime)
            {
                RefreshTokenGrantTypeParams refreshTokenGrantTypeParams = new RefreshTokenGrantTypeParams(refreshToken, opts.ClientId, opts.ClientSecret, OpenIDConnectScope.OPENID);
                var accessTokenResponse = idpClient.RefreshToken(refreshTokenGrantTypeParams);
                acccessTokenExpirationTime = DateTime.Now.AddSeconds(accessTokenResponse.ExpiresInMs);
                refreshToken = accessTokenResponse.RefreshToken;

                noarkClient.AuthToken = accessTokenResponse.AccessToken;
            }
        }

        private void InitIdpClient(Options options)
        {
            idpClient = new Oauth2HttpClient(options.IdpServerAddress, true);
        }

        private void InitClient(Options options)
        {
            noarkClient = new NoarkClient(options.ServerAddress, true, options.CertificatePath, options.CertificatePass);
        }

        private void InitClientWithoutClientCertificate(Options options)
        {
            noarkClient = new NoarkClient(options.ServerAddress, true);
        }
    }
}
