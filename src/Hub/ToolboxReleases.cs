using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Toolbox
{
    internal static class ToolboxReleases
    {
        private const string LatestEndpoint = "https://api.github.com/repos/thongteevee/thong-khong-toolbox/releases/latest";
        private const string ReleasePage = "https://github.com/thongteevee/thong-khong-toolbox/releases/tag/v";

        internal static Version CurrentVersion
        {
            get
            {
                var version = typeof(ToolboxReleases).Assembly.GetName().Version;
                return new Version(version.Major, version.Minor, version.Build);
            }
        }

        internal static string GetReleasePage(Version version) { return ReleasePage + version; }

        internal static async Task<Version> GetLatestVersion(CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var response = await Api.Send(LatestEndpoint, HttpMethod.Get, null,
                userAgent: "ThongKhongToolbox/" + CurrentVersion, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException("GitHub has no published Toolbox release.");
                if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                    throw new InvalidOperationException("GitHub refused the update check. Try again later.");

                var release = await Api.Json(response, "GitHub Releases").ConfigureAwait(false);
                var tag = Api.Text(release, "tag_name");
                Version version;
                if (Api.Flag(release, "draft") || Api.Flag(release, "prerelease") ||
                    !tag.StartsWith("v", StringComparison.Ordinal) || !Version.TryParse(tag.Substring(1), out version) ||
                    version.Build < 0 || version.Revision >= 0 || tag != "v" + version)
                    throw new InvalidOperationException("GitHub returned an unrecognized Toolbox release version.");
                return version;
            }
        }
    }
}
