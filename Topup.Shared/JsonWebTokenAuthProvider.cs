using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RestSharp;
using ServiceStack;
using ServiceStack.Auth;
using ServiceStack.Web;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace ZP.HABO.Shared
{


    //public class CustomRequestFilterAttribute : RequestFilterAttribute
    //{
    //  //  private Logger _log = LogManager.GetLogger("CustomRequestFilterAttribute");
    //    public override void Execute(IRequest req, IResponse res, object requestDto)
    //    {
    //        string userEndpoint = "";// ConfigurationManager.AppSettings["IdentityServer"];// ConfiguationManager "http://uat.zopost.vn:2119/identity";
    //        try
    //        {
    //            var token = req.Headers.Get("ZP-Authorization");
    //            if (token != null)
    //            {
    //                //validate token

    //                var client = new RestClient(userEndpoint);
    //                var request = new RestRequest("connect/userinfo", Method.POST);
    //                request.AddHeader("Authorization", "Bearer " + token);
    //                IRestResponse response = client.Execute(request);
    //                if (response.StatusCode == HttpStatusCode.OK)
    //                {
    //                    var content = response.Content;
                        
    //                    req.Headers.Add("ZP-UserInfo", content);
    //                    return;
    //                }
    //            }
    //            res.WriteError(req, requestDto, "Unauthorized exception!");
    //            res.End();
    //        }
    //        catch (Exception e)
    //        {
    //           // _log.Error("Validate ex" + e);
    //            res.WriteError(req, requestDto, e.Message);
    //            res.End();

    //        }


    //    }


    //    private  bool ValidateToken(string authToken)
    //    {
    //        var tokenHandler = new JwtSecurityTokenHandler();
    //        var validationParameters = GetValidationParameters();

    //        SecurityToken validatedToken;
    //        IPrincipal principal = tokenHandler.ValidateToken(authToken, validationParameters, out validatedToken);
    //        return true;
    //    }

    //    private static TokenValidationParameters GetValidationParameters()
    //    {
    //        return new TokenValidationParameters()
    //        {
    //            ValidateLifetime = true, // Because there is no expiration in the generated token
    //            ValidateAudience = true, // Because there is no audiance in the generated token
    //            ValidateIssuer = true,   // Because there is no issuer in the generated token
    //            ValidIssuer = "Sample",
    //            ValidAudience = "Sample",
    //            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)) // The same key as the one that generate the token
    //        };
    //    }


    //    private static string GenerateToken()
    //    {
    //        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    //        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    //        var secToken = new JwtSecurityToken(
    //            signingCredentials: credentials,
    //            issuer: "Sample",
    //            audience: "Sample",
    //            claims: new[]
    //            {
    //            new Claim(JwtRegisteredClaimNames.Sub, "meziantou")
    //            },
    //            expires: DateTime.UtcNow.AddDays(1));

    //        var handler = new JwtSecurityTokenHandler();
    //        return handler.WriteToken(secToken);
    //    }


    //    private static JwtSecurityToken ValidateAndDecode(string jwt, IEnumerable<SecurityKey> signingKeys)
    //    {
    //        var validationParameters = new TokenValidationParameters
    //        {
    //            // Clock skew compensates for server time drift.
    //            // We recommend 5 minutes or less:
    //            ClockSkew = TimeSpan.FromMinutes(5),
    //            // Specify the key used to sign the token:
    //            IssuerSigningKeys = signingKeys,
    //            RequireSignedTokens = true,
    //            // Ensure the token hasn't expired:
    //            RequireExpirationTime = true,
    //            ValidateLifetime = true,
    //            // Ensure the token audience matches our audience value (default true):
    //            ValidateAudience = true,
    //            ValidAudience = "api://default",
    //            // Ensure the token was issued by a trusted authorization server (default true):
    //            ValidateIssuer = true,
    //            ValidIssuer = "https://{yourOktaDomain}/oauth2/default"
    //        };

    //        try
    //        {
    //            var claimsPrincipal = new JwtSecurityTokenHandler()
    //                .ValidateToken(jwt, validationParameters, out var rawValidatedToken);

    //            return (JwtSecurityToken)rawValidatedToken;
    //            // Or, you can return the ClaimsPrincipal
    //            // (which has the JWT properties automatically mapped to .NET claims)
    //        }
    //        catch (SecurityTokenValidationException stvex)
    //        {
    //            // The token failed validation!
    //            // TODO: Log it or display an error.
    //            throw new Exception($"Token failed validation: {stvex.Message}");
    //        }
    //        catch (ArgumentException argex)
    //        {
    //            // The token was not well-formed or was invalid for some other reason.
    //            // TODO: Log it or display an error.
    //            throw new Exception($"Token was invalid: {argex.Message}");
    //        }
    //    }

    //}


    public class JsonWebTokenAuthProvider : AuthProvider, IAuthWithRequest
    {
        private static string Name = "JWT";
        private static string Realm = "/auth/JWT";
        private const string MissingAuthHeader = "Missing Authorization Header";
        private const string InvalidAuthHeader = "Invalid Authorization Header";

        private string Audience { get; }
        private string Issuer { get; }
        private X509Certificate2 Certificate { get; }


        /// <summary>
        /// Creates a new JsonWebToken Auth Provider
        /// </summary>
        /// <param name="discoveryEndpoint">The url to get the configuration informaion from.. (er "http://localhost:22530/" + ".well-known/openid-configuration")</param>
        /// <param name="audience">The client for openID (eg js_oidc)</param>

        public JsonWebTokenAuthProvider(string discoveryEndpoint, string audience = null)
        {
            Provider = Name;
            AuthRealm = Realm;
            Audience = audience;

              var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(discoveryEndpoint, 
                  new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var config = configurationManager.GetConfigurationAsync().Result;

            Certificate = new X509Certificate2(Convert.FromBase64String(config.JsonWebKeySet.Keys.First().X5c.First()));
            Issuer = config.Issuer;
        }


        public void PreAuthenticate(IRequest req, IResponse res)
        {
            var header = req.Headers["Authorization"];
            var authService = req.TryResolve<AuthenticateService>();
            authService.Request = req;

            // pass auth header in as oauth token to authentication
            authService.Post(new Authenticate
            {
                provider = Name,
                oauth_token = header
            });
        }

        public override bool IsAuthorized(IAuthSession session, IAuthTokens tokens, Authenticate request = null)
        {
            return true; //HttpContext.Current.User.Identity.IsAuthenticated && session.IsAuthenticated && string.Equals(session.UserName, HttpContext.Current.User.Identity.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override object Authenticate(IServiceBase authService, IAuthSession session, Authenticate request)
        {
            var header = request.oauth_token;

            // if no auth header, 401
            if (string.IsNullOrEmpty(header))
            {
                throw HttpError.Unauthorized(MissingAuthHeader);
            }

            var headerData = header.Split(' ');

            // if header is missing bearer portion, 401
            if (string.Compare(headerData[0], "BEARER", StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw HttpError.Unauthorized(InvalidAuthHeader);
            }

            try
            {

                // set current principal to the validated token principal
                //Thread.CurrentPrincipal = JsonWebToken.ValidateToken(headerData[1], Certificate, Audience, Issuer);

                //if (HttpContext.Current != null)
                //{
                //    // set the current request's user the the decoded principal
                //    HttpContext.Current.User = Thread.CurrentPrincipal;
                //}

                // set the session's username to the logged in user
                session.UserName = Thread.CurrentPrincipal.Identity.Name;

                return OnAuthenticated(authService, session, new AuthTokens(), new Dictionary<string, string>());
            }
            catch (Exception ex)
            {
                throw new HttpError(HttpStatusCode.Unauthorized, ex);
            }
        }
    }

}
