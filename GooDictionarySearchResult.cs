using Blast.Core.Results;

using System;
using System.Collections.Generic;

namespace GooDic.Fluent.Plugin
{
    public sealed class GooDictionarySearchResult : SearchResultBase
    {
        public string Query { get; }
        public string Title { get; }
        public string Content { get; }
        public Uri Url { get; }

        public GooDictionarySearchResult(string query, string title, string content, Uri url, double score)
            : base(GooDictionarySearchApplication.ApplicationName, $"{title}\n{content}", query, nameof(GooDic), score,
                  GooDictionarySearchApplication.SupportedOprations, GooDictionarySearchApplication.SearchTags)
        {
            Query = query;
            Title = title;
            Content = content;
            Url = url;

            MlFeatures = new Dictionary<string, string>
            {
                [nameof(Query)] = Query,
                [nameof(Title)] = Title,
                [nameof(Content)] = Content,
                [nameof(Url)] = Url.AbsoluteUri,
            };
        }

        protected override void OnSelectedSearchResultChanged() { }
    }
}
