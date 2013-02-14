using System;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using RunProcess;

namespace Integration.Tests
{
    [TestFixture]
    public class SimpleIntegrationTests
    {
        [Test]
        public void can_start_interact_with_and_stop_a_process ()
        {
            using (var subject = new InteractiveShell(">", "bye"))
            {
	            subject.Start("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory());

	            var intro = subject.ReadToPrompt();
	            Assert.That(intro.Item1, Is.StringStarting(ExampleProcess.Program.Intro));

	            var interact = subject.SendAndReceive("This is a test");
	            Assert.That(interact.Item1, Is.StringStarting("You wrote This is a test"));

                Assert.That(subject.IsAlive());

	            subject.Terminate();
                Assert.That(subject.IsAlive(), Is.False);
            }
	        Assert.Pass();
        }

        [Test]
        public void can_run_and_read_from_a_non_interactive_process ()
        {
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();
                Thread.Sleep(250);
				
				Assert.That(subject.IsAlive(), Is.False);

				var output = subject.StdOut.ReadAllText(Encoding.Default);
                Assert.That(output, Is.StringStarting(ExampleNoninteractiveProcess.Program.StdOutMsg), "Standard Out");

				var err = subject.StdErr.ReadAllText(Encoding.Default);
                Assert.That(err, Is.StringStarting(ExampleNoninteractiveProcess.Program.StdErrMsg), "Standard Error");
			}
        }
		
		[Test]
        public void can_pass_arguments_to_process ()
        {
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
				subject.Start("print hello world");
                Thread.Sleep(250);
				
				var output = subject.StdOut.ReadAllText(Encoding.Default);
                Assert.That(output, Is.StringStarting("hello world"));
            }
        }

        [Test]
        public void can_wait_for_process_and_kill_if_required ()
        {
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
                subject.Start("wait");

                var ended = subject.WaitForExit(TimeSpan.FromSeconds(1));

                Assert.That(ended, Is.False, "Ended");
                Assert.That(subject.IsAlive(), Is.True, "Alive");

                subject.Kill();
                subject.WaitForExit(TimeSpan.FromSeconds(1));

                Assert.That(subject.IsAlive(), Is.False, "Alive after kill");
            }
        }
    }
}
