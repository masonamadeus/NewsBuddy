﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Xml;
using System.Linq;
//using System.Windows.Input;

namespace NewsBuddy
{

    public static class DuplicateCheck
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }

    public class APingestor
    {


        public string apiKey { get; set; }
        public EntityTagHeaderValue previousETag { get; set; }
        public EntityTagHeaderValue nextETag { get; set; }

        public bool isAuthorized { get; set; }
        public List<APObject> Items { get { return apFeedItems; } }

        private const string reqUrl = "https://api.ap.org/media/v/content/";
        private string feedNextPage = "null";
        private HttpClient client;
        private List<APTopic> activeTopics = new List<APTopic>();
        private List<APObject> apFeedItems = new List<APObject>();

        public APingestor()
        {
            apiKey = Settings.Default.APapiKey;
        }

        public List<APTopic> GetFollowedTopics()
        {
            List<APTopic> topics = new List<APTopic>();
            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(reqUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            HttpResponseMessage response = client.GetAsync("/media/v/account/followedtopics?apikey=" + apiKey).Result;
            if (response.IsSuccessStatusCode)
            {
                string reply = response.Content.ReadAsStringAsync().Result;
                dynamic js = JsonConvert.DeserializeObject(reply);

                if (js.error != null)
                {
                    Trace.WriteLine(js.error.ToString());
                    return null;
                }

                var dataBlock = js.data;
                if (dataBlock == null)
                {
                    Trace.WriteLine("datablock was null on feed processing");
                    return null;
                }

                var topicBlock = dataBlock.followedtopics;
                var topicCount = (topicBlock != null && topicBlock.Count != null) ? topicBlock.Count : 0;
                for (int i = 0; i < topicCount; i++)
                {
                    var topic = topicBlock[i];
                    topics.Add(new APTopic()
                    {
                        topicID = Int32.Parse(topic.id.ToString()),
                        topicName = topic.name.ToString()
                    });
                }
                return topics;
                Trace.WriteLine(reply);
            }
            else
            {
                Trace.WriteLine(response.RequestMessage + "    " + response.ReasonPhrase);
                return null;
            }
        }

        public XmlDocument GetItem(string itemID)
        {
            if (itemID == null || itemID == "")
            {
                return null;
            }
            Trace.WriteLine("getting ap item");
            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(reqUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            HttpResponseMessage response = client.GetAsync(String.Format("{0}?apikey={1}&in_my_plan=true", itemID, apiKey)).Result;
            if (response.IsSuccessStatusCode)
            {
                string reply = response.Content.ReadAsStringAsync().Result;
                XmlDocument story = DownloadStory(reply);
                return story == null ? null : story;
            }
            else
            {
                return null;
            }

        }

        private XmlDocument DownloadStory(string json)
        {
            dynamic js = JsonConvert.DeserializeObject(json);
            if (js.error != null)
            {
                Trace.WriteLine(js.error);
                return null;
            }

            var downloadUrl = js.data.item.renditions.nitf.href;
            if (downloadUrl == null)
            {
                Trace.WriteLine("Download URL was null");
                return null;
            }
            else
            {
                HttpResponseMessage response = client.GetAsync(downloadUrl.ToString() + "&apikey=" + apiKey).Result;
                if (response.IsSuccessStatusCode)
                {
                    string rawXml =
                    response.Content.ReadAsStringAsync().Result;
                    XmlDocument story = new XmlDocument();

                    story.LoadXml(rawXml);
                    return story;
                }
                else
                {
                    Trace.WriteLine("HttpRequest was unsuccessful - downloadStory()");
                    return null;
                }
            }

        }

        public APObject WorkerGetTopicFeed(APTopic topic, bool isAuto=false)
        {
            Trace.WriteLine("getting followed topic: " + topic.topicName);

            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(reqUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            if (activeTopics.Contains(topic) && isAuto)
            {
                HttpResponseMessage response = client.GetAsync("/" + topic.nextPageLink + "&apikey=" + apiKey).Result;
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    Trace.WriteLine("Bad Request " + topic.topicName);
                    return new APObject(new object(), this)
                    {
                        isTopic = true,
                        isBadRequest = true
                    };
                }
                else if (response.IsSuccessStatusCode)
                {
                    topic.APIresponse = response.Content.ReadAsStringAsync().Result;

                    return WorkerProcessTopicFeed(topic);
                }
                else if (response.ReasonPhrase.Contains("qt is expired"))
                {
                    topic.nextPageLink = "null";
                   
                    activeTopics.Remove(topic);

                    return null;
                }
                else
                {
                    Trace.WriteLine(response.RequestMessage + "    " + response.ReasonPhrase);
                    activeTopics.Remove(topic);
                    return null;
                }
            }
            else
            {
                HttpResponseMessage response = client.GetAsync(String.Format("feed?q=followedtopicid:{0}&apikey={1}&in_my_plan=true&versions=latest&text_links=plain", topic.topicID, apiKey)).Result;
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    Trace.WriteLine("Bad Request " + topic.topicName);
                    return new APObject(new object(), this)
                    {
                        isTopic = true,
                        isBadRequest = true
                    };
                }
                else if (response.IsSuccessStatusCode)
                {
                    topic.APIresponse = response.Content.ReadAsStringAsync().Result;
                    
                    if (isAuto && !activeTopics.Contains(topic))
                    {
                        activeTopics.Add(topic);
                    }
                    return WorkerProcessTopicFeed(topic);
                }
                else
                {
                    Trace.WriteLine(response.RequestMessage + "    " + response.ReasonPhrase);
                    return null;
                }
            }

        }

        private APObject WorkerProcessTopicFeed(APTopic topic)
        {
            APObject topicParent = new APObject(topic.APIresponse, this, true)
            {
                headline = topic.topicName,
                isTopic = true,
                altID = topic.topicID.ToString()
            };

            dynamic js = JsonConvert.DeserializeObject(topic.APIresponse);

            if (js.error != null)
            {
                Trace.WriteLine(js.error.ToString());
                return null;
            }

            var dataBlock = js.data;
            if (dataBlock == null)
            {
                Trace.WriteLine("datablock was null on feed processing");
                return null;
            }

            string rawNextPageLink = dataBlock.next_page;

            topic.nextPageLink = rawNextPageLink.Replace(@"https://api.ap.org/", "");

            var itemsBlock = dataBlock.items;
            var itemsCount = (itemsBlock != null && itemsBlock.Count != null) ? itemsBlock.Count : 0;

            for (int entry = 0; entry < itemsCount; entry++)
            {
                topicParent.associations.Add(ProcessTopicEntry(itemsBlock[entry], entry));
            }

            if (topicParent.associations.Count == 0)
            {
                return null;
            }
            else
            {
                return topicParent;
            }

        }

        private APObject ProcessTopicEntry(dynamic d_item, int index)
        {
            List<APObject> textAssociations = new List<APObject>();
            var item = d_item.item;
            var associations_len = item.associations != null ? ((JContainer)item.associations).Count : 0;

            // if there are associations at all, check them to see if they are text.
            if (associations_len != 0)
            {
                foreach (dynamic association in item.associations)
                {
                    if (String.Equals(association.Value.type.ToString(), "text"))
                    {
                        // if it is text, add it to the list of text associations
                        dynamic currentAssoc = association.Value;
                        textAssociations.Add(NewAPObject(currentAssoc, this));
                    }
                }
            }

            // if there ARE text associations, make a parent container with them in it.
            if (textAssociations.Count > 0)
            {
                APObject assocParent = NewAPObject(item, this, true);

                foreach (APObject textAssociation in textAssociations)
                {
                    assocParent.associations.Add(textAssociation);
                }
                return assocParent;
            }
            else // if there are not text associations, make a new story and strip out the associations.
            {
                return NewAPObject(item, this);
            }
        }

        public void GetFeed(bool isAuto = false)
        {


            Trace.WriteLine("Getting Feed");
            if (activeTopics.Count > 0 && !Settings.Default.APShowTopics)
            {
                activeTopics.Clear();
            }
            // Make a new httpclient if you need one
            if (client == null)
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(reqUrl);
                client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            }

            // set up the request message to the proper address, insert the API Key, and the other variables.
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(client.BaseAddress + "feed?apikey=" + apiKey + "&in_my_plan=true&versions=latest&text_links=plain", UriKind.Absolute),
                Method = HttpMethod.Get
            };

            // Add the application/json header so that it gets accepted
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // if this isn't the first request, send along the ET tag
            if (nextETag != null)
            {
                request.Headers.IfNoneMatch.Add(nextETag);
            }

            if (Debugger.IsAttached)
            {
                Trace.WriteLine(request.Headers.ToString());
            }

            HttpResponseMessage response;
            // send the API request out and get a response.
            if (isAuto && feedNextPage != "null")
            {
                Trace.WriteLine("Getting AUTO Feed");
                response = client.GetAsync("/" + feedNextPage + "&apikey=" + apiKey).Result;
            }
            else
            {
                Trace.WriteLine("Getting CONDITIONAL Feed");
                response = client.SendAsync(request).Result;
            }

            if (response.IsSuccessStatusCode)
            {
                apFeedItems.Clear();

                isAuthorized = true;

                string reply = response.Content.ReadAsStringAsync().Result;

                ProcessFeed(reply);

                Trace.WriteLine(reply);

                SortList();

                // ETag Stuff, Don't worry about it.
                if (nextETag != null & response.Headers.ETag != null)
                {
                    previousETag = new EntityTagHeaderValue(nextETag.ToString());
                }

                if (response.Headers.ETag != null)
                {
                    nextETag = new EntityTagHeaderValue(response.Headers.ETag.ToString());
                }

                if (Debugger.IsAttached)
                {
                    Trace.WriteLine(response.StatusCode.ToString());
                }
            }
            else if (response.StatusCode == HttpStatusCode.NotModified)
            {
                Trace.WriteLine("Not Modified. Same ETag");
            }
            else if (response.ReasonPhrase.Contains("qt is expired"))
            {
                feedNextPage = "null";
                return;
            }
            else
            {
                isAuthorized = false;

                Trace.WriteLine(response.RequestMessage.ToString());

                Trace.WriteLine(response.StatusCode.ToString());
            }

        }

        private void ProcessFeed(string body)
        {
            dynamic js = JsonConvert.DeserializeObject(body);

            if (js.error != null)
            {
                Trace.WriteLine(js.error.ToString());
                return;
            }

            var dataBlock = js.data;
            if (dataBlock == null)
            {
                Trace.WriteLine("datablock was null on feed processing");
                return;
            }

            string rawNextPageLink = dataBlock.next_page;

            feedNextPage = rawNextPageLink.Replace(@"https://api.ap.org/", "");

            var itemsBlock = dataBlock.items;
            var itemsCount = (itemsBlock != null && itemsBlock.Count != null) ? itemsBlock.Count : 0;

            for (int entry = 0; entry < itemsCount; entry++)
            {
                ProcessEntry(itemsBlock[entry], entry);
            }

        }

        private void ProcessEntry(dynamic d_item, int index)
        {
            List<APObject> textAssociations = new List<APObject>();
            var item = d_item.item;
            var associations_len = item.associations != null ? ((JContainer)item.associations).Count : 0;

            // if there are associations at all, check them to see if they are text.
            if (associations_len != 0)
            {
                foreach (dynamic association in item.associations)
                {
                    if (String.Equals(association.Value.type.ToString(), "text"))
                    {
                        // if it is text, add it to the list of text associations
                        dynamic currentAssoc = association.Value;
                        textAssociations.Add(NewAPObject(currentAssoc, this));
                    }
                }
            }

            // if there ARE text associations, make a parent container with them in it.
            if (textAssociations.Count > 0)
            {
                APObject assocParent = NewAPObject(item, this, true);

                foreach (APObject textAssociation in textAssociations)
                {
                    assocParent.associations.Add(textAssociation);
                }

                apFeedItems.Add(assocParent);
            }
            else // if there are not text associations, make a new story and strip out the associations.
            {
                apFeedItems.Add(NewAPObject(item, this));
            }
        }

        private APObject NewAPObject(dynamic _item, APingestor _ingestor, bool isParent = false)
        {
             

            APObject newObject = new APObject(_item, _ingestor, isParent)
            {
                headline = _item.headline != null ? _item.headline.ToString() : "",
                uri = _item.uri != null ? _item.uri.ToString() : "",
                altID = _item.altids.itemid != null ? _item.altids.itemid.ToString() : "",
                version = _item.version != null ? Int32.Parse(_item.version.ToString()) : null
            };

            return newObject;
        }

        private void SortList()
        {
            List<APObject> assocParents = new List<APObject>();
            for (int o = 0; o < apFeedItems.Count; o++)
            {
                APObject item = apFeedItems[o];
                if (item.isAssocParent)
                {
                    assocParents.Add(item);
                }
            }

            for (int pp = assocParents.Count - 1; pp >= 0; pp--)
            {
                APObject parent = assocParents[pp];
                apFeedItems.Remove(parent);
                apFeedItems.Insert(0, parent);
            }

        }

        public void Dispose()
        {
            if (client != null)
            {
                client.Dispose();
                Trace.WriteLine("Disposing feed client");
                client = null;
            }
        }

    }
}

