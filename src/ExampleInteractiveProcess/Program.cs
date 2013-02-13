using System;

namespace ExampleProcess
{
	public class Program
	{
        public const string Intro = "Hi there. Please type some lines; Type 'bye' to exit:";

		static void Main(string[] args)
		{
			Console.WriteLine(Intro);

			for (;;)
			{
                Console.Write(">");
				var line = Console.ReadLine();
                if (line.ToLower().StartsWith("bye") || line.ToLower().StartsWith("'bye")) break;
				Console.WriteLine("You wrote " + line);
			}
			Console.WriteLine("Bye");
		}
	}
}
