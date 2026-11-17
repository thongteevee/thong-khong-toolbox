using System;
using System.IO;

namespace Toolbox
{
    internal sealed class HubAccounts
    {
        private readonly string path;
        private Connections current;

        public sealed class Connections
        {
            public GoogleCredential GoogleClient { get; set; }
            public string GoogleRefreshToken { get; set; }
            public SlingConnection Sling { get; set; }
        }

        internal HubAccounts(string folder)
        {
            path = Path.Combine(folder, "accounts.dpapi");
            current = LocalData.LoadSecret<Connections>(path);
            if (current != null) return;

            var legacy = Path.Combine(folder, "Sling");
            var google = LocalData.LoadSecret<GoogleCredential>(Path.Combine(legacy, "google.dpapi"));
            var client = LocalData.LoadSecret<GoogleCredential>(Path.Combine(legacy, "google-client.dpapi"));
            var sling = LocalData.LoadSecret<SlingConnection>(Path.Combine(legacy, "sling.dpapi"));
            current = new Connections();
            if (GoogleClient.IsClient(google) && !String.IsNullOrEmpty(google.RefreshToken))
            {
                Save(Registration(google), google.RefreshToken, sling);
            }
            else if (GoogleClient.IsClient(client) || sling != null)
            {
                Save(GoogleClient.IsClient(client) ? Registration(client) : null, null, sling);
            }
        }

        internal GoogleCredential SignInClient
        {
            get { return current.GoogleClient ?? GoogleClient.PackagedClient(); }
        }

        internal GoogleCredential Google
        {
            get
            {
                if (String.IsNullOrEmpty(current.GoogleRefreshToken)) return null;
                var credential = Registration(current.GoogleClient);
                credential.RefreshToken = current.GoogleRefreshToken;
                return credential;
            }
        }

        internal SlingConnection Sling { get { return current.Sling; } }

        internal void ConfigureGoogle(GoogleCredential client)
        {
            var active = current.GoogleClient;
            var keepToken = active != null && active.ClientId == client.ClientId && active.ClientSecret == client.ClientSecret;
            Save(Registration(client), keepToken ? current.GoogleRefreshToken : null, current.Sling);
        }

        internal void ConnectGoogle(GoogleCredential credential)
        {
            Save(Registration(credential), credential.RefreshToken, current.Sling);
        }

        internal void DisconnectGoogle()
        {
            Save(current.GoogleClient, null, current.Sling);
        }

        internal void SetSling(SlingConnection connection)
        {
            Save(current.GoogleClient, current.GoogleRefreshToken, connection);
        }

        private static GoogleCredential Registration(GoogleCredential source)
        {
            return new GoogleCredential { ClientId = source.ClientId, ClientSecret = source.ClientSecret };
        }

        private void Save(GoogleCredential client, string refreshToken, SlingConnection sling)
        {
            var updated = new Connections { GoogleClient = client, GoogleRefreshToken = refreshToken, Sling = sling };
            LocalData.SaveSecret(path, updated);
            current = updated;
        }
    }
}
