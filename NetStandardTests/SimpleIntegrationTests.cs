using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using NUnit.Framework;
using RunProcessNetStd;
using RunProcessNetStd.Internal;

namespace NetStandardTests
{
	[TestFixture]
	public class SimpleIntegrationTests
	{
		public const string StdOutMsg = "This is my little diatribe!";
		public const string StdErrMsg = "Here is a message on std err";
		public const string Intro = "Hi there. Please type some lines; Type 'bye' to exit:";
		
		readonly TimeSpan one_second = TimeSpan.FromSeconds(1);

		[OneTimeSetUp]
		public void is_compatible_windows()
		{
			Assert.That(ProcessHost.HostIsCompatible(), "Host operating system can't run these tests");
		}

		[Test, Explicit("Requires users to be available")]
		[TestCase("devvirtual-pc", "exampleUser", "exampleUser")]
		public void can_impersonate_another_user (string domain, string user, string password)
		{
			var expected = domain+"\\"+user+"\r\n";
			using (var subject = new ProcessHost("whoami", null))
			{
				subject.StartAsAnotherUser(domain, user, password, "");
				subject.WaitForExit(TimeSpan.FromSeconds(2));
				Assert.That(subject.IsAlive(), Is.False);

				var output = subject.StdOut.ReadAllText(Encoding.Default);
				Assert.That(output.ToLower(), Is.EqualTo(expected.ToLower()), "Standard Out");

				var err = subject.StdErr.ReadAllText(Encoding.Default);
				Assert.That(err, Is.Empty, "Standard Error");
			}
		}

		[Test]
		public void can_access_kernel_functions()
		{
			var x = Kernel32.GetCurrentProcess();
			Console.WriteLine(x.ToInt64());
		}

		[Test]
		public void can_call_environment_executables ()
		{
			using (var subject = new ProcessHost("net", null))
			{
				subject.Start();
                subject.WaitForExit(TimeSpan.FromSeconds(5));

				Assert.That(subject.IsAlive(), Is.False);

				var output = subject.StdOut.ReadAllText(Encoding.Default);
				Assert.That(output, Is.EqualTo(""), "Standard Out");

				var err = subject.StdErr.ReadAllText(Encoding.Default);
				Assert.That(err, Does.StartWith("The syntax of this command is:"), "Standard Error");
			}
		}

		[Test]
		public void can_run_and_read_from_a_non_interactive_process()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();
				Thread.Sleep(500);

				Assert.That(subject.IsAlive(), Is.False);

				var output = subject.StdOut.ReadAllWithTimeout(Encoding.Default, TimeSpan.FromSeconds(10));
				Assert.That(output, Does.StartWith(StdOutMsg), "Standard Out");

				var err = subject.StdErr.ReadAllWithTimeout(Encoding.Default, TimeSpan.FromSeconds(10));
				Assert.That(err, Does.StartWith(StdErrMsg), "Standard Error");
			}
		}

		[Test]
		public void can_pass_arguments_to_process()
		{
			using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start("print hello world");
				Thread.Sleep(500);

				var output = subject.StdOut.ReadAllWithTimeout(Encoding.Default, TimeSpan.FromSeconds(10));
				Assert.That(output, Does.StartWith("hello world"));
			}
		}
        
        [Test]
        public void can_pass_environment_variables_to_process()
        {
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
                var envars = new Dictionary<string,string>{
                    { "one", "two" },
                    { "three", "four" }
                };
                subject.Start("envarg", envars);
                Thread.Sleep(500);

                var output = subject.StdOut.ReadAllWithTimeout(Encoding.Default, TimeSpan.FromSeconds(10));
                Assert.That(output, Does.Contain("one = two"));
                Assert.That(output, Does.Contain("three = four"));
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
			var loc = Assembly.GetExecutingAssembly().GetAssemblyLocation();
			var dir = Path.GetDirectoryName(loc);
			using (var subject = new ProcessHost($"{dir}/ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start("return 1729");

				Assert.That(subject.WaitForExit(one_second), "process did not exit");
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
				Assert.That(read, Does.StartWith(Intro));

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
		public void process_is_killed_when_process_host_is_disposed()
		{
			uint processId;
			using (var subject = new ProcessHost("./ExampleInteractiveProcess.exe", Directory.GetCurrentDirectory()))
			{
				subject.Start();
				processId = subject.ProcessId();
			}
			Thread.Sleep(500);
			try
			{
				// system.diagnostics.process throws in debug mode, returns null in release mode!
				var prc = Process.GetProcessById((int)processId);
				Assert.That(prc?.ProcessName, Is.Null);
			}
			catch (ArgumentException)
			{
				Assert.Pass();
				return;
			}
			catch (InvalidOperationException)
			{
				Assert.Pass();
				return;
			}
			Assert.Fail();
		}


        [Test]
        public void can_start_process_as_child()
        {
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
                subject.StartAsChild("print hello world");
                Thread.Sleep(500);
                var output = subject.StdOut.ReadAllText(Encoding.Default);
                //var output = subject.StdOut.ReadToTimeout(Encoding.Default, TimeSpan.FromSeconds(10));
                Assert.That(output, Does.StartWith("hello world"));
            }
        }

        [Test]
        public void child_process_can_be_killed_when_parent_is_killed()
        {
            Process p;
            int pid;
            using (var subject = new ProcessHost("./ExampleNoninteractiveProcess.exe", Directory.GetCurrentDirectory()))
            {
                // start a process, which calls `StartAsChild`. Then kill that process and check the child died
                subject.Start("spawn");
                var output = subject.StdOut.ReadToTimeout(Encoding.Default, TimeSpan.FromSeconds(1));
                var ok = int.TryParse(output, out pid);
                Assert.That(ok, Is.True, "PID was {" + output +"}");
                
                p = Process.GetProcessById(pid);
                Assert.That(p.HasExited, Is.False, "Child process not running");
            }
            Thread.Sleep(500);

            Assert.That(p.HasExited, Is.True, "Child process is still running (pid = " + pid + ")");
        }

        [Test, Explicit,
		Description(
@"This test should cause high CPU use, but should not exhaust Handle count
or increase memory use. Check that no child processes are open after running.")]
		public void stress_test()
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
