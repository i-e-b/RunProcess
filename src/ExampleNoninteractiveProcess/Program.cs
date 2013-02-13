using System;
using System.Text;
using System.Threading;

namespace ExampleNoninteractiveProcess
{
	public class Program
	{
        public const string StdOutMsg = "This is my little diatribe!";
        public const string StdErrMsg = "Here is a message on std err";

		static void Main(string[] args)
		{
            if (args.Length > 0 && args[0] == "wait")
            {
                for(;;)
                {
	                Thread.Sleep(1000);
                }
            }

			Console.WriteLine(StdOutMsg);
            using (var stdErr = Console.OpenStandardError())
            {
                var msgBytes = Encoding.ASCII.GetBytes(StdErrMsg);
                stdErr.Write(msgBytes, 0, msgBytes.Length);
            }
		}
	}
}
