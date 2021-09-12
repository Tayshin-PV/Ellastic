using System;
using System.Collections.Generic;
using System.Linq;
using Nest;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using ESearch.Models;
using elasticsearch_nest_webapi.Services;

namespace ESearch.Services
{
    public class ElasticSearchService : ISearchService<Doc>
    {
        private readonly ElasticClient client;
        public ElasticSearchService()
        {
            client = ElasticConfig.GetClient();
        }
        //public HttpResponseMessage dictonarylist()
        //{
        //    List<string> innerItem = new List<string> { };
        //    AppConfig.ConfigurationTableSection sqlTables = new AppConfig.ConfigurationTableSection();
        //    foreach (AppConfig.Element e in sqlTables.Tables)
        //    {
        //        innerItem.Add(e.dictonaryName);
        //    }
        //    var responseObj = new { message = "Successful", tableList = innerItem };
        //    HttpResponseMessage resp = new HttpResponseMessage
        //    {
        //        Content = new StringContent(JsonConvert.SerializeObject(responseObj), System.Text.Encoding.UTF8, "application/json")
        //    };
        //    return resp;
        //}
        public IEnumerable<string> Dictonarylist()
        {
            //Список источников данных
            List<string> innerItem = new List<string> { };
            AppConfig.ConfigurationTableSection sqlTables = new AppConfig.ConfigurationTableSection();
            //Добавление первого элемента списка для поиска по всем источникам
            //innerItem.Add("Все источники");
            foreach (AppConfig.Element e in sqlTables.Tables)
            {
                if (e.Fill)
                    innerItem.Add(e.DictonaryName);
            }
            //var responseObj = new { message = "Successful", tableList = innerItem };
            //HttpResponseMessage resp = new HttpResponseMessage
            //{
            //    Content = new StringContent(JsonConvert.SerializeObject(responseObj), System.Text.Encoding.UTF8, "application/json")
            //};
            return innerItem;
        }
        #region поиск с использованием категорий
        public SearchResult<Doc> SearchByCategory(string query, IEnumerable<Category> esFilters, int page, int pageSize)
        {
            return Search(query, esFilters, page, pageSize);
        }
        #endregion
        #region автозавершение строки по введенным символам
        public IEnumerable<string> Autocomplete(string query)
        {
            try
            {
                var result = client.Search<Doc>(s => s
                 .Index(ElasticConfig.IndexName)
                 .Suggest(x => x.Term("tag-suggestions", c => c
                 .Text(query)
                 .Field(f => f.Content)
                 .Size(6))));
                if (result.OriginalException != null)
                {
                    string[] res = { result.OriginalException.InnerException.Message };
                    return res;
                }
                if (result.Suggest != null)
                {
                    if (result.Suggest != null)
                    {
                        return result.Suggest["tag-suggestions"].SelectMany(x => x.Options).Select(y => y.Text);
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch //(Exception ex)
            {
                string[] res = { "Нет соединения с Ellasticsearch" };
                return res;
            }
        }
        #endregion
        public IEnumerable<string> Suggest(string query)
        {
            var result = client.Search<Doc>(s => s
               //               .Index(ElasticConfig.IndexName)
               .Suggest(c => c
                      .Term("post-suggestions", t => t.Text(query)
                          //.Field(f => f.file.extension)
                          //.Field(f => f.file.created)
                          //.Field(f => f.file.url)
                          .Size(6)
                )));
            if (result.Suggest != null)
            {
                if (result.Suggest != null)
                {
                    return null;
                }
                else
                {
                    return result.Suggest["post-suggestions"].SelectMany(x => x.Options).Select(y => y.Text);
                }
            }
            else
            {
                return null;
            }
        }
        public SearchResult<Doc> FindMoreLikeThis(string id, int pageSize)
        {
            var result = client.Search<Doc>(x => x
                .Index(ElasticConfig.IndexName)
                .Query(y => y
                    .MoreLikeThis(m => m
                        .Like(l => l.Document(d => d.Id(id)))
                        .Fields(new[] { "Content" })
                        .MinTermFrequency(1)
                        )).Size(pageSize));

            return new SearchResult<Doc>
            {
                Total = (int)result.Total,
                Page = 1,
                Results = result.Documents
            };
        }
        public HttpResponseMessage Get(string id)
        {
            //            var result = client.Get<_Doc>(new DocumentPath<_Doc>(id));
            //            return result.Source.file.url.ToLower().Replace("file://\\", "file:///\\");
            var docInfo = client.Get(new DocumentPath<Doc>(id));
            string url = "";
            WebRequest request;

            if (docInfo.Source.File != null)// документ получен обходчиком fsCrawler и содержит путь к файлу
            { url = docInfo.Source.File.Url.ToLower().Replace("file://\\", "file:///\\"); }
            else if (docInfo.Source.Description != null)
            {
                //Документ получен из описания
                if (docInfo.Source.Description.ContainsKey("IDX500a") == true)
                {
                    //Описание документа содержит путь к файлу
                    if (docInfo.Source.Description.TryGetValue("IDX500a", out url))
                    {
                    }
                }
            }
            if (url == "")
            {
                var responseObj = new { message = "Successful", text = "" };
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(responseObj), System.Text.Encoding.UTF8, "application/json")
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html;  charset=utf-8");
                resp.Content.Headers.Clear();
                return resp;
            }
            string FileName = Uri.EscapeUriString(Path.GetFileName(url));
            request = WebRequest.Create(url);
            if (request.Proxy != null)
            {
                request.UseDefaultCredentials = true;
                request.Proxy.Credentials = request.Credentials;
            }
            request.Timeout = 30 * 60 * 1000;
            try
            {
                WebResponse response = request.GetResponse();
                HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
                Stream s = response.GetResponseStream();
                result.Content = new StreamContent(s);
                result.Content.Headers.Add("Content-Disposition", "attachment; filename=\"" + FileName + "\"");
                //result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = FileName }.ToString());
                //result.Content.Headers.ContentDisposition.FileName = Path.GetFileName(url);
                result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                //            result.Content.Headers.ContentLength = s.Length;
                return result;
            }
            catch //(Exception ex)
            {
                //HttpResponseMessage resp = new HttpResponseMailMessage { mailto = "", subject = "", body = ex.Message };
                HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.NoContent)
                {
                    Content = null
                };
                return resp;
            }
        }
        public SearchResult<Doc> Search(string query, IEnumerable<Category> esFilters, int page, int pageSize)
        {
            List<Category> esfilledFilters = new List<Category>();
            esfilledFilters.Clear();
            string dictonaryName = "";
            //string emailForOrder = "";
            //var item = esFilters.FirstOrDefault(i => i.NameWeb == "dictonaryName");
            if (query == "undefined") return null;
            foreach (Category c in esFilters)
                if (c.NameWeb == "dictonaryName")
                    if (c.Values != null)
                    {
                        esfilledFilters.Add(new Category { NameES = c.NameWeb, NameWeb = c.NameWeb, Values = c.Values });
                        dictonaryName = c.Values[0];
                        break;
                    }
            AppConfig.ConfigurationTableSection sqlTables = new AppConfig.ConfigurationTableSection();
            foreach (AppConfig.Element e in sqlTables.Tables)
            {
                if (System.Web.HttpUtility.HtmlDecode(e.DictonaryName) == System.Web.HttpUtility.HtmlDecode(dictonaryName))
                {
                    foreach (Category c in esFilters)
                        if (c.NameES == null)
                        {
                            foreach (AppConfig.Field f in e.Fieldslist)
                                if (f.LabelFieldName == c.NameWeb)
                                {
                                    c.NameES = f.MdbFieldName;
                                    esfilledFilters.Add(new Category { NameES = c.NameES, NameWeb = c.NameWeb, Values = c.Values });
                                    break;
                                }
                        }
                    esFilters = esfilledFilters;
                    break;
                }
            }
            IEnumerable<Func<QueryContainerDescriptor<Doc>, QueryContainer>> filters = ClassFilters.Filters(esFilters);
            AggregationContainer aggregationContainer = ClassAggregationContainer.GetAggregationContainer(esFilters);
            //query = IQueryString(query);
            SearchDescriptor<Doc> searchDescriptor = new SearchDescriptor<Doc>();
            searchDescriptor.Index(ElasticConfig.IndexName);
            if (esFilters.ElementAt(0).NameES == null)
            {
                searchDescriptor.Query(q => q
                                      .Bool(b => b
                                           .Must(m => m
                                                .QueryString(qs => qs
                                                            .DefaultField("content")
                                                                                   .Query(query)
                                                            )
                                                )
                                           )
                                      )
                                      .Highlight(h => h
                                                .Encoder(HighlighterEncoder.Default)
                                                .PreTags("<b>")
                                                .PostTags("</b>")
                                                .Fields(f => f
                                        .Field("filename")
                                        .Field("content")
                                        .Type(HighlighterType.Plain)
                                        .ForceSource(true)
                                        .FragmentSize(150)
                                        .Fragmenter(HighlighterFragmenter.Span)
                                        .NumberOfFragments(3)
                                        .NoMatchSize(150)
                                        )
                                        )
                                    .From(page - 1)
                                    .Size(pageSize);
            }
            else
            {
                searchDescriptor.Query(q => q
                                      .Bool(b => b
                                           .Must(m => m
                                                .QueryString(qs => qs
                                                            .DefaultField("content")
                                                                                   .Query(query)
                                                            )
                                                )
                                                .Filter(f => f
                                                       .Bool(b1 => b1
                                                            .Must(filters ?? null)
                                                            )
                                                        )
                                           )
                                      )
                                      .Highlight(h => h
                                                .Encoder(HighlighterEncoder.Default)
                                                .PreTags("<b>")
                                                .PostTags("</b>")
                                                .Fields(f => f
                                        .Field("filename")
                                        .Field("content")
                                        .Type(HighlighterType.Plain)
                                        .ForceSource(true)
                                        .FragmentSize(150)
                                        .Fragmenter(HighlighterFragmenter.Span)
                                        .NumberOfFragments(3)
                                        .NoMatchSize(150)
                                        )
                                        )
                                    .From(page - 1)
                                    .Size(pageSize);
            }
            //if (filters != null)
            //{
            //    searchDescriptor.Query(q => q
            //    .Bool(b => b
            //            .Filter(f => f
            //                .Bool(b1 => b1
            //                    .Must(filters)))
            //                    ));
            //}
            if (aggregationContainer.Aggregations != null)
            {
                foreach (KeyValuePair<string, IAggregationContainer> item in aggregationContainer.Aggregations)
                {
                    item.Value.Terms.Size = 100;
                }
                searchDescriptor.Aggregations(aggregationContainer.Aggregations);
            }
            ClassLog web_log = new ClassLog
            {
                Page = page,
                PageSize = pageSize,
                Query = query
            };
            var result = client.Search<System.Dynamic.ExpandoObject>(searchDescriptor);
            SearchResult<Doc> searchResult = new SearchResult<Doc>
            {
                Total = (int)result.Total < 10000 ? (int)result.Total : 10000,
                Page = page,
                //                Results = result.Documents,
                //Hits = result.Hits,
                ElapsedMilliseconds = result.Took,
            };
            if (result.OriginalException != null)
                searchResult.Message = result.OriginalException.InnerException.Message;
            searchResult.Aggregations = new Dictionary<string, List<string>> { };
            foreach (KeyValuePair<string, IAggregate> ta in result.Aggregations)
            {
                List<string> v = new List<string> { };
                foreach (KeyedBucket<object> b in ((BucketAggregate)ta.Value).Items.ToList())
                    v.Add(b.Key.ToString());
                searchResult.Aggregations.Add(ta.Key, v);
            }
            foreach (IHit<System.Dynamic.ExpandoObject> hit in result.Hits)
            {
                Doc doc = new Doc
                {
                    Id = hit.Id,
                    Content = hit.Highlight.Count > 0 ? hit.Highlight.ElementAt(0).Value.ElementAt(0) : null
                };
                if (hit.Source.ToDictionary(s => s.Key, v => v.Value).ContainsKey("file"))
                {
                    doc.Content_type = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["file"]).ToDictionary(s => s.Key, v => v.Value)["content_type"].ToString();
                    doc.Created = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["file"]).ToDictionary(s => s.Key, v => v.Value)["created"].ToString()/*.ToString("dd.MM.yyyy")*/;
                    doc.Extension = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["file"]).ToDictionary(s => s.Key, v => v.Value)["extension"].ToString();
                    doc.Filename = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["file"]).ToDictionary(s => s.Key, v => v.Value)["filename"].ToString();
                    doc.Url = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["file"]).ToDictionary(s => s.Key, v => v.Value)["url"].ToString();

                }
                if (hit.Source.ToDictionary(s => s.Key, v => v.Value).ContainsKey("description"))
                {
                    object description = hit.Source.ToDictionary(s => s.Key, v => v.Value)["description"];
                    Type objectType = description.GetType();
                    int iCount = 0;
                    foreach (PropertyInfo propertyInfo in objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.Name.ToLower() == "count"))
                    {
                        iCount = (int)propertyInfo.GetValue(description, null);
                        break;
                    }
                    try
                    {
                        if (iCount > 0)
                        {
                            doc.Description = ((IDictionary<string, object>)hit.Source.ToDictionary(s => s.Key, v => v.Value)["description"]).ToDictionary(s => s.Key, v => v.Value.ToString());
                            if (doc.Description.ContainsKey("dictonaryName"))
                            {
                                doc.Description.TryGetValue("dictonaryName", out string dictonaryNameFromHit);
                                doc.DescriptionToRus(dictonaryNameFromHit);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                if (searchResult.Results != null)
                    searchResult.Results = searchResult.Results.Concat(new[] { doc });
                else
                    searchResult.Results = new[] { doc };
            }
            searchResult.AggregationsToRus(dictonaryName);
            searchResult.SearchID = "";
            //Лог
            IndexResponse indexResponse = client.Index(web_log, p => p
            .Index("web_log"));
            return searchResult;
        }
        public SearchResult<Doc> Search(string query, string dictonaryName, int page, int pageSize)
        {
            List<Category> esFilters = new List<Category> { };
            Category category = new Category { };
            category.NameWeb = "dictonaryName";
            category.Values = new List<string> { };
            category.Values.Add(dictonaryName);
            esFilters.Add(category);
            if (query == "undefined") return null;
            return Search(query, esFilters, page, pageSize);
        }

        //private string IQueryString(string query)
        //{
        //    if (query != null)
        //        if (!(query.Contains('"') || query.Contains('*') || query.Contains('~') || query.Contains('+') || query.Contains('-') || query.Contains('=')))
        //        {
        //            string newitem = "";
        //            foreach (string item in query.Trim().Split(' '))
        //            {
        //                newitem += item + "* ";
        //            }
        //            query = newitem;
        //        }
        //    return query;
        //}
    }
}
