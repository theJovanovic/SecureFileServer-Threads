using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MyWebServer;

public struct ContextAndTime
{
    public HttpListenerContext context;
    public Stopwatch timer;
}

class Program
{

    private static Dictionary<string, string> cache_dictionary = new Dictionary<string, string>();
    private static readonly object lock_object = new object();
    private static readonly string cache_path = "../../../cache.txt";

    public static void Main(string[] args)
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();
        Console.WriteLine("Web server running on address http://localhost:8080/");

        // LOAD CACHE.TXT DATA
        LoadCache();
        Console.WriteLine("Cache file loaded.\n\n");

        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            ContextAndTime context_and_time = new ContextAndTime();
            context_and_time.context = context;
            context_and_time.timer = new Stopwatch();
            context_and_time.timer.Start();
            ProcessRequest(context_and_time);
        }

        listener.Stop();
    }

    private static void LoadCache()
    {
        using (StreamReader reader = new StreamReader(cache_path))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split('-');
                if (parts.Length == 2)
                {
                    cache_dictionary.Add(parts[0], parts[1]);
                }
            }
        }
    }

    private static void ProcessRequest(ContextAndTime context_and_time)
    {
        if (!ThreadPool.QueueUserWorkItem(ProcessRequestTask, context_and_time))
        {
            context_and_time.context.Response.StatusCode = 500;
            SendResponse(context_and_time.context.Response, "500 - Failed", context_and_time.timer);
        }
    }

    private static void ProcessRequestTask(object state)
    {

        ContextAndTime context_and_time = (ContextAndTime) state;

        HttpListenerContext context = context_and_time.context;
        Stopwatch timer = context_and_time.timer;

        Console.WriteLine(
                $"########## Request ##########:\n" +
                $"User host name: {context.Request.UserHostName}\n" +
                $"HTTP method: {context.Request.HttpMethod}\n" +
                $"HTTP headers: {context.Request.Headers}" +
                $"Content type: {context.Request.ContentType}\n" +
                $"Content length: {context.Request.ContentLength64}\n" +
                $"Cookies: {context.Request.Cookies}\n"
                );

        HttpListenerRequest request = context.Request;

        HttpListenerResponse response = context.Response;
        response.ContentType = "text/plain; charset=utf-8";
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        string query = request.Url.AbsolutePath;
        string path = "../../../files" + query;

        if (query == "/cache.txt")
        {
            // ACCESS to CACHE.TXT FILE DENIED
            response.StatusCode = 403;
            SendResponse(response, "403 - Forbidden access", timer);
            return;
        }

        if (cache_dictionary.ContainsKey(query))
        {
            // DATA FOUND IN CACHE
            response.ContentLength64 = 64;
            SendResponse(response, cache_dictionary[query], timer);
            return;
        }

        string text = ReadFile(path);

        if (text == "")
        {
            // FILE NOT FOUND (OR FILE IS EMPTY)
            response.StatusCode = 404;
            SendResponse(response, "404 - File not found", timer);
            return;
        }

        string responseString = ComputeSHA256Hash(text);

        SendResponse(response, responseString, timer);

        // ADD NEW DATA TO CACHE.TXT
        lock (lock_object)
        {
            if (!cache_dictionary.ContainsKey(query))
            {
                cache_dictionary.Add(query, responseString);

                using (StreamWriter writer = new StreamWriter(cache_path, true))
                {
                    writer.WriteLine(query + "-" + responseString);
                    Console.WriteLine("Cache file updated!\n");
                }
            }
        }
    }

    private static string ReadFile(string file_name)
	{
		string file_text = "";

		try
		{
			using (StreamReader reader = new StreamReader(file_name))
			{
				file_text = reader.ReadToEnd();
			}
		}
		catch (Exception ex)
		{
			return "";
		}

		return file_text;
	}

    private static string ComputeSHA256Hash(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);

            StringBuilder result = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("X2"));
            }

            return result.ToString();
        }
    }

    private static void SendResponse(HttpListenerResponse response, string responseString, Stopwatch timer)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();
        timer.Stop();
        Console.WriteLine(
            $"########## Response ##########:\n" +
            $"Status code: {response.StatusCode}\n" +
            $"Content type: {response.ContentType}\n" +
            $"Content length: {response.ContentLength64}\n" +
            $"Time taken for response: {timer.ElapsedMilliseconds} ms\n" +
            $"Body: {responseString}\n"
            );
    }

    private static void PrintCache()
    {
        lock (lock_object)
        {
            foreach (string key in cache_dictionary.Keys)
            {
                Console.WriteLine(key + ":" + cache_dictionary[key]);
            }
        }
    }

}