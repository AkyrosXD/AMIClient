using System;
using System.Collections.Generic;
using Akyros.Asterisk.AMI;

namespace AmiExamples
{
    class Program
    {
        static AmiClient amiClient;

        static void Main(string[] args)
        {
            amiClient = new AmiClient("USERNAME_HERE", "SECRET_HERE", "URL_HERE", 0 /* and your port here */);
            amiClient.OnEvent += EventCallback;
            amiClient.OnLoginSuccess += OnLoginSuccessCallback;
            amiClient.OnLoginFailed += OnLoginFailedCallback;
            amiClient.OnLogoff += OnLogoffCallback;
            amiClient.Login();
        }

        static void EventCallback(Dictionary<string, string> eventData)
        {
            // here you can test these two examples.
            // more examples will be added in the future

            //Examples.PrintEvent(eventData);
            //Examples.PrintCallerAndReceiver(eventData);
        }

        private static void OnLoginSuccessCallback()
        {
            Console.WriteLine("welcome!!!");
        }

        private static void OnLoginFailedCallback()
        {
            Console.WriteLine("login failed! check your username and secret");
        }

        private static void OnLogoffCallback()
        {
            Console.WriteLine("logged off, goodbye!");
        }
    }

    public static class Examples
    {
        public static void PrintEvent(Dictionary<string, string> eventData)
        {
            foreach (KeyValuePair<string, string> pair in eventData)
            {
                Console.WriteLine($"{pair.Key}: {pair.Value}");
            }
        }

        public static void PrintCallerAndReceiver(Dictionary<string, string> eventData)
        {
            const string CDR_APIDIDNUMBER = "CDR(APIDIDNUMBER)=";
            if (eventData.TryGetValue("Event", out string eventName) && eventName == "Newexten")
            {
                if (eventData.TryGetValue("AppData", out string appData))
                {
                    int cdrIndex = appData.IndexOf(CDR_APIDIDNUMBER);
                    if (cdrIndex == 0)
                    {
                        if (eventData.TryGetValue("CallerIDNum", out string caller))
                        {
                            string receiver = appData[CDR_APIDIDNUMBER.Length..];
                            Console.WriteLine($"Caller: {caller}\nReceiver: {receiver}");
                        }
                    }
                }
            }
        }
    }
}
