using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Mvc.Html;

namespace MyWebServer;

struct CacheData
{
    public string Hash { get; set; }
    public DateTime Time { get; set; }
    public long FileSize { get; set; }
    public long Requests { get; set; }
}

class Program
{

    private static Random Random = new Random();
    private static int MAX_NUMBER_OF_ITEMS_IN_CACHE = 3;
    private static object CacheLockObject = new object();
    private static AutoResetEvent ClearCacheSignal = new AutoResetEvent(false);
    private static SemaphoreSlim WorkingTasksSemaphore = new SemaphoreSlim(10);
    private static Dictionary<string, CacheData> Cache = new Dictionary<string, CacheData>();

    public static void Main(string[] args)
    {

        HttpListener Listener = new HttpListener();
        Listener.Prefixes.Add("http://localhost:8080/");
        Listener.Start();
        Console.WriteLine("Web server running on address http://localhost:8080/");

        _ = Task.Run(() =>
        {
            while (true)
            {
                ClearCacheSignal.WaitOne(); // wait until signaled by working task

                lock (CacheLockObject)
                {
                    Console.WriteLine("Deleting an item.");
                    RemoveElementWithLowestIndex();
                }
            }
        });

        while (true)
        {
            Console.WriteLine($"\nServer is listening. Free worker tasks: {WorkingTasksSemaphore.CurrentCount}");
            HttpListenerContext Context = Listener.GetContext();
            if (Context.Request.RawUrl == "/favicon.ico")
            {
                continue;
            }    
            _ = Task.Run(() => {
                ProcessRequest(Context);
            });
        }
    }

    static void RemoveElementWithLowestIndex()
    {
        double lowestValue = double.MaxValue;
        string keyToRemove = null;

        foreach (var kvp in Cache)
        {
            CacheData data = kvp.Value;
            TimeSpan age = DateTime.Now - data.Time;

            double value = (double) data.Requests * data.FileSize / age.TotalSeconds;

            if (value < lowestValue)
            {
                lowestValue = value;
                keyToRemove = kvp.Key;
            }
        }

        if (keyToRemove != null)
        {
            Cache.Remove(keyToRemove);
            Console.WriteLine($"Removed element with key '{keyToRemove}'");
        }
    }

    private static async void ProcessRequest(HttpListenerContext Context)
    {
        WorkingTasksSemaphore.Wait();
        Console.WriteLine("Worker task started.");
        HttpListenerRequest Request = Context.Request;
        //PrintRequestData(Request);
        string QueryString = Request.RawUrl!;

        if (QueryString == "/")
        {
            Console.WriteLine("Error - empty query");
            Context.Response.StatusCode = 400;
            SendResponse(Context.Response, "400 - Empty query");
            return;
        }

        QueryString = QueryString.Substring(1);

        if (!IsValidQuery(QueryString))
        {
            Console.WriteLine("Error - invalid query");
            Context.Response.StatusCode = 400;
            SendResponse(Context.Response, "400 - Invalid query");
            return;
        }

        if (Cache.ContainsKey(QueryString))
        {
            Console.WriteLine("Success - hash found in cache");
            CacheData cacheData = Cache[QueryString];
            cacheData.Requests += 1;
            Cache[QueryString] = cacheData;
            Context.Response.StatusCode = 200;
            SendResponse(Context.Response, $"{QueryString} - {Cache[QueryString].Hash}");
            return;
        }

        string Filedata = await GetFileData(QueryString);

        if (Filedata == "")
        {
            Console.WriteLine("Error - file not found");
            Context.Response.StatusCode = 404;
            SendResponse(Context.Response, "404 - File not found");
            return;
        }

        Task<string> TaskFileHash = Task.Run(() =>
        {
            return ComputeSHA256Hash(Filedata);
        });

        Task<long> TaskFileSize = Task.Run(() =>
        {
            return GetFileSize(QueryString);
        });

        string HashedFiledata = TaskFileHash.Result;
        long FileSize = TaskFileSize.Result;

        lock (CacheLockObject)
        {
            if (!Cache.ContainsKey(QueryString))
            {
                CacheData NewCacheData = new CacheData();
                NewCacheData.Hash = HashedFiledata;
                NewCacheData.Time = DateTime.Now;
                NewCacheData.FileSize = FileSize;
                NewCacheData.Requests = 1;
                Cache[QueryString] = NewCacheData;
                if (Cache.Count == MAX_NUMBER_OF_ITEMS_IN_CACHE)
                {
                    ClearCacheSignal.Set();
                }
            }
        }

        Console.WriteLine("Success - file hashed");
        Context.Response.StatusCode = 200;
        SendResponse(Context.Response, $"{QueryString} - {HashedFiledata}");
    }

    private static void SendResponse(HttpListenerResponse response, string responseString)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();

        WorkingTasksSemaphore.Release();
    }

    private static bool IsValidQuery(string Query)
    {

        string[] QuerySplitted = Query.Split(".");

        if (QuerySplitted.Length != 2) return false;

        if (QuerySplitted[1] != "txt") return false;

        return true;
    }

    private static void PrintRequestData(HttpListenerRequest Request)
    {
        Console.WriteLine("REQUEST:");
        Console.WriteLine($"URL: {Request.Url.ToString()}");
        Console.WriteLine("COOKIES:");
        foreach (Cookie cookie in Request.Cookies)
        {
            Console.WriteLine($"Cookie Name: {cookie.Name}");
            Console.WriteLine($"Cookie Value: {cookie.Value}");
            Console.WriteLine($"Cookie Domain: {cookie.Domain}");
            Console.WriteLine($"Cookie Path: {cookie.Path}");
            Console.WriteLine($"Cookie Expiration Date: {cookie.Expires}");
            Console.WriteLine("------------------------------------");
        }
        Console.WriteLine($"HEADERS: {Request.Headers.ToString()}");
    }

    private static async Task<string> GetFileData(string Filename)
    {
        string Filedata = "";

        try
        {
            using (StreamReader Reader = new StreamReader($"../../../files/{Filename}"))
            {
                Filedata = await Reader.ReadToEndAsync();
            }
        }
        catch (Exception Exception)
        {
            Console.WriteLine(Exception.Message);
            return "";
        }

        return Filedata;
	}

    private static long GetFileSize(string Filename)
    {
        FileInfo FileInfo = new FileInfo($"../../../files/{Filename}");
        return FileInfo.Length;
    }

    private static string ComputeSHA256Hash(string Input)
    {
        using (SHA256 Sha256 = SHA256.Create())
        {
            byte[] Bytes = Encoding.UTF8.GetBytes(Input);
            byte[] Hash = Sha256.ComputeHash(Bytes);

            StringBuilder result = new StringBuilder();
            for (int i = 0; i < Hash.Length; i++)
            {
                result.Append(Hash[i].ToString("X2"));
            }

            return result.ToString();
        }
    }
}