using System;

class Test
{
    static void Main()
    {
        string apiKey = Environment.GetEnvironmentVariable("API_KEY");

        Console.WriteLine(apiKey);
    }
}