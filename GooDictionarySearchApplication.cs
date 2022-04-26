using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

using Blast.API.Core.Processes;
using Blast.API.Processes;
using Blast.Core;
using Blast.Core.Interfaces;
using Blast.Core.Objects;
using Blast.Core.Results;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using static GooDic.Fluent.Plugin.Extensions.FuncExtensions;

namespace GooDic.Fluent.Plugin
{
    public class GooDictionarySearchApplication : ISearchApplication
    {
        public static string ApplicationName { get; } = "Goo Dictionary Search";
        public static string GooDictionaryHost { get; } = "https://dictionary.goo.ne.jp";
        public static IList<ISearchOperation> SupportedOprations => new List<ISearchOperation>(applicationInfo.SupportedOperations);
        public static ICollection<SearchTag> SearchTags => applicationInfo.DefaultSearchTags;

        private static readonly Dictionary<string, string> especialKeywords = new()
        {
            { "%26", "%252526" },
            { "%2F", "%25252F" },
        };

        private static readonly SearchApplicationInfo applicationInfo = new(ApplicationName, "EN => JA / JA => EN", new[] { new ConcreteSearchOperation() })
        {
            MinimumSearchLength = 1,
            IsProcessSearchEnabled = false,
            IsProcessSearchOffline = false,
            ApplicationIconGlyph = "\uE82D",
            SearchAllTime = ApplicationSearchTime.Moderate,
            DefaultSearchTags = new[] { new SearchTag { Name = "GooDictionary", IsMainSearchTag = true, SaveInHistory = true, UseIconGlyph = false, Value = "goodic" } },
        };

        private readonly HttpClient httpClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
        private readonly HtmlParser htmlParser = new();

        public SearchApplicationInfo GetApplicationInfo() => applicationInfo;

        public ValueTask<IHandleResult> HandleSearchResult(ISearchResult searchResult)
        {
            if (searchResult is not GooDictionarySearchResult result)
            {
                throw new InvalidCastException(nameof(GooDictionarySearchResult));
            }
            if (result.SelectedOperation is not ConcreteSearchOperation operation)
            {
                throw new InvalidCastException(nameof(ConcreteSearchOperation));
            }

            IProcessManager manager = ProcessUtils.GetManagerInstance();
            manager.StartNewProcess(result.Url.AbsoluteUri);

            return new ValueTask<IHandleResult>(new HandleResult(true, false));
        }

        public async IAsyncEnumerable<ISearchResult> SearchAsync(SearchRequest searchRequest, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || searchRequest.SearchType == SearchType.SearchProcess) yield break;

            string text = searchRequest.SearchedText;
            string query = especialKeywords.Aggregate(HttpUtility.UrlEncode(text), (s, pair) => s.Replace(pair.Key, pair.Value));
            Log(text);

            HttpResponseMessage response = await httpClient.GetAsync($"{GooDictionaryHost}/srch/en/{query}/m0u/", cancellationToken);

            string html = await response.Content.ReadAsStringAsync(cancellationToken);
            IHtmlDocument document = await htmlParser.ParseDocumentAsync(html, cancellationToken);
            string url = response.RequestMessage?.RequestUri?.AbsoluteUri ?? "";
            Log(url);

            // true if response is redirected to word page directly
            if (url[GooDictionaryHost.Length..].StartsWith("/word/"))
            {
                IHtmlCollection<IElement> parents = document.GetElementsByClassName("meanging");
                Log(parents.Length);

                // remove all unnecessary elements
                foreach (IElement element in parents.SelectMany(p => p.QuerySelectorAll("script, div.examples")))
                {
                    element.Remove();
                }

                static string trimAndMergeLines(string s)
                {
                    var lines = s.Split('\n').Select(s => s.Trim()).Where(Not<string?>(string.IsNullOrWhiteSpace));
                    return string.Join(' ', lines);
                }
                string[] titles = parents.SelectMany(p => p.GetElementsByClassName("basic_title")).Select(el => el.TextContent.Trim()).ToArray();
                string[] contents = parents.SelectMany(p => p.GetElementsByClassName("content-box-ej")).Select(el => trimAndMergeLines(el.TextContent)).ToArray();

                for (int i = 0, len = titles.Length; i < len; ++i)
                {
                    string title = titles[i];
                    string content = contents[i];
                    Log(title);
                    Log(content);

                    yield return new GooDictionarySearchResult(text, title, content, new Uri(url), 2);
                }
            }
            else
            {
                // list of meanings
                IHtmlCollection<IElement> listItems = document.QuerySelectorAll(".search-list .content_list > li");
                Log(listItems.Length);

                foreach (IElement listItem in listItems)
                {
                    string title = listItem.QuerySelector(".title")?.TextContent?.Trim() ?? "";
                    string content = listItem.QuerySelector(".text")?.TextContent?.Trim() ?? "";
                    string href = GooDictionaryHost + listItem.QuerySelector("a[href]")?.GetAttribute("href") ?? "";
                    Log(title);
                    Log(content);
                    Log(href);

                    yield return new GooDictionarySearchResult(text, title, content, new Uri(href), 1);
                }
            }
        }

#if DEBUG
        private static readonly string executingPath = Assembly.GetExecutingAssembly().Location;
        private static readonly string logPath = Path.Combine(Path.GetDirectoryName(executingPath) ?? throw new DllNotFoundException(executingPath), "./log/output.txt");
#endif

        [Conditional("DEBUG")]
        private static void Log(object arg, [CallerArgumentExpression("arg")] string? argumentName = null)
        {
#if DEBUG
            if (Path.GetDirectoryName(logPath) is string directory)
            {
                Directory.CreateDirectory(directory);
            }

            using var sw = new StreamWriter(logPath, append: true, Encoding.UTF8);
            var now = DateTime.Now.ToString("G");

            sw.WriteLine($"[{now}] {argumentName} = <{arg}>");
#endif
        }
    }
}
