using System;
using System.Diagnostics;
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
		readonly TimeSpan one_second = TimeSpan.FromSeconds(1);

		[Test]
		public void can_start_interact_with_and_stop_a_process()
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
		public void can_run_and_read_from_a_non_interactive_process()
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
		public void can_pass_arguments_to_process()
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
		public void can_wait_for_process_and_kill_if_required()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start("wait");

				var ended = subject.WaitForExit(one_second);

				Assert.That(ended, Is.False, "Ended");
				Assert.That(subject.IsAlive(), Is.True, "Alive");

				subject.Kill();
				var endedAfterKill = subject.WaitForExit(one_second);

				Assert.That(endedAfterKill, Is.True, "ended after kill");
				Assert.That(subject.IsAlive(), Is.False, "Alive after kill");
				Assert.That(subject.ExitCode(), Is.EqualTo(127), "standard killed code");
			}
		}

		[Test]
		public void can_get_exit_code_from_process()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start("return 1729");

				subject.WaitForExit(one_second);
				var code = subject.ExitCode();

				Assert.That(code, Is.EqualTo(1729));
			}
		}

		[Test]
		public void can_write_strings_from_a_processes_pipes()
		{
			using (var subject = new ProcessHost("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();

				subject.StdIn.WriteAllText(Encoding.Default, "bye\r\n");

				int exitCode;
				var ok = subject.WaitForExit(one_second, out exitCode);

				Assert.That(ok, Is.True);
				Assert.That(exitCode, Is.EqualTo(0));
			}
		}

		[Test]
		public void Reading_from_an_IN_pipe_throws_an_exception()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();

				var dummy = new byte[2];

				Assert.Throws<Exception>(() => subject.StdIn.Peek());
				Assert.Throws<Exception>(() => subject.StdIn.Read(dummy, 0, 1));

				int exitCode;
				var exited = subject.WaitForExit(one_second, out exitCode);

				Assert.That(exited, Is.True);
				Assert.That(exitCode, Is.EqualTo(0));
			}
		}

		[Test]
		public void Writing_to_an_OUT_pipe_throws_an_exception()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();

				var dummy = new byte[2];

				Assert.Throws<Exception>(() => subject.StdOut.Write(dummy, 0, 1));

				int exitCode;
				var exited = subject.WaitForExit(one_second, out exitCode);

				Assert.That(exited, Is.True);
				Assert.That(exitCode, Is.EqualTo(0));
			}
		}

		[Test]
		public void can_read_and_write_single_lines_on_a_processes_pipes()
		{
			using (var subject = new ProcessHost("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();

				var read = subject.StdOut.ReadLine(Encoding.Default, one_second);
				Assert.That(read, Is.EqualTo(ExampleProcess.Program.Intro));

				subject.StdIn.WriteLine(Encoding.Default, "bye");

				int exitCode;
				var ok = subject.WaitForExit(one_second, out exitCode);

				Assert.That(ok, Is.True);
				Assert.That(exitCode, Is.EqualTo(0));
			}
		}

		[Test]
		public void can_get_process_id_and_use_with_existing_dotnet_libraries()
		{
			using (var subject = new ProcessHost("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();

                uint id = subject.ProcessId();

                var process = Process.GetProcessById((int)id);
                Assert.That(process.HasExited, Is.False, "Exited");

                process.Kill();

				int exitCode;
				var exited = subject.WaitForExit(one_second, out exitCode);

				Assert.That(exited, Is.True, "Exited after kill");
				Assert.That(exitCode, Is.EqualTo(0), "Exit code");
			}
		}

        [Test]
        public void process_is_killed_when_process_host_is_disposed ()
        {
            uint processId;
            using (var subject = new ProcessHost("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
	            subject.Start();
                processId = subject.ProcessId();
            }

			try
			{
				// system.diagnostics.process throws in debug mode, returns null in release mode!
				var prc = Process.GetProcessById((int)processId);
				Assert.That(prc, Is.Null);
			}
			catch (ArgumentException)
			{
				Assert.Pass();
			}
            catch (InvalidOperationException)
            {
                Assert.Pass();
            }
            Assert.Fail();
        }

        [Test, Explicit,
		Description(
@"This test should cause high CPU use, but should not exhaust Handle count
or increase memory use. Check that no child processes are open after running.")]
        public void stress_test ()
        {
	        for (int i = 0; i < 10000; i++)
	        {
		        using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
		        {
			        subject.Start();
		        }
	        }

            Assert.Pass();
        }
	}
}
