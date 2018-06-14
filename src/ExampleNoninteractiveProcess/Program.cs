using System;
using System.Linq;
using System.Text;
using System.Threading;
using RunProcess;

namespace ExampleNoninteractiveProcess
{
	public class Program
	{
        public const string StdOutMsg = "This is my little diatribe!";
        public const string StdErrMsg = "Here is a message on std err";

		static int Main(string[] args)
		{
            if (args.Length > 0 && args[0] == "wait")
            {
                for(;;)
                {
	                Thread.Sleep(1000);
                }
            }

            if (args.Length > 0 && args[0] == "print")
            {
	            Console.WriteLine(string.Join(" ", args.Skip(1)));
                return 0;
            }

		    if (args.Length > 0 && args[0] == "spawn")
		    {
                // spawn a new process, and output its ID. Then wait forever
		        var ph = new ProcessHost("ExampleNoninteractiveProcess.exe", "");
                ph.StartAsChild("wait");
		        Console.WriteLine(ph.ProcessId());
		        for(;;)
		        {
		            Thread.Sleep(1000);
		        }
		    }

            
            if (args.Length > 1 && args[0] == "return")
            {
                return int.Parse(args[1]);
            }

			Console.WriteLine(StdOutMsg);
            using (var stdErr = Console.OpenStandardError())
            {
                var msgBytes = Encoding.ASCII.GetBytes(StdErrMsg);
                stdErr.Write(msgBytes, 0, msgBytes.Length);
            }

            return 0;
		}
	}
}
