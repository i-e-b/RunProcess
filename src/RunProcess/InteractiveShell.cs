using System;
using System.Text;
using System.Threading;

namespace RunProcess
{
	/// <summary>
	/// Special handler for interactive shell child processes.
	/// (e.g. FTP, telnet)
	/// </summary>
	public class InteractiveShell : IDisposable
	{
		ProcessHost _host;

		/// <summary>
		/// Name of application, as passed to Start()
		/// </summary>
		public string ApplicationName { get; private set; }

		/// <summary>
		/// Prompt as supplied by shell.
		/// Used for ReadToPrompt()
		/// </summary>
		protected string Prompt { get; set; }

		/// <summary>
		/// Command to exit shell
		/// Used by Terminate()
		/// </summary>
		protected string ExitCommand { get; set; }

		/// <summary>
		/// Text encoding of shell
		/// </summary>
		protected Encoding Encoding { get; set; }

		/// <summary>
		/// Create a new wrapper for interactive shells.
		/// </summary>
		/// <param name="processPrompt">Prompt displayed by shell</param>
		/// <param name="exitCommand">command to send for exit</param>
		public InteractiveShell(string processPrompt, string exitCommand)
		{
			Encoding = Encoding.Default;
			Prompt = processPrompt;
			ExitCommand = exitCommand;
		}

		/// <summary>
		/// Read stdout and stderr until prompt is printed.
		/// </summary>
		/// <returns>A 2-string Tuple; the first item is stdout, the second stderr.</returns>
		/// <remarks>This method may never return as it doesn't have a time-out.</remarks>
		public Tuple<string, string> ReadToPrompt()
		{
			const int bufferLength = 128;
			var buffer = new byte[bufferLength];
			int bytesReadCount;
			var stdOut = new StringBuilder(4096);
			var stdErr = new StringBuilder();

			while (!stdOut.ToString().EndsWith("\n" + Prompt) && stdOut.ToString() != Prompt)
			{
				while (_host.StdErr.Peek() > 0)
				{
					bytesReadCount = _host.StdErr.Read(buffer, 0, bufferLength);
					stdErr.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				while (_host.StdOut.Peek() > 0)
				{
					bytesReadCount = _host.StdOut.Read(buffer, 0, bufferLength);
					stdOut.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				Thread.Sleep(20);
			}
			while (_host.StdErr.Peek() > 0)
			{
				bytesReadCount = _host.StdErr.Read(buffer, 0, bufferLength);
				stdErr.Append(Encoding.GetString(buffer, 0, bytesReadCount));
			}

			return new Tuple<string, string>(stdOut.ToString(), stdErr.ToString());
		}

		/// <summary>
		/// Read stdout and stderr until prompt is printed.
		/// </summary>
		/// <param name="toSend">What to send without the line-feed and carriage return.</param>
		/// <returns>A 2-string Tuple; the first item is stdout, the second stderr.</returns>
		/// <remarks>This method may never return as it doesn't have a time-out.</remarks>
		public Tuple<string, string> SendAndReceive(string toSend)
		{
			SendCommand(toSend);
			return ReadToPrompt();
		}

		/// <summary>
		/// Start shell.
		/// </summary>
		public void Start(string applicationName, string workDirectory)
		{
			_host = new ProcessHost(applicationName, workDirectory);
			ApplicationName = applicationName;
			_host.Start();
		}

		/// <summary>
		/// Tell shell to exit and free resources.
		/// </summary>
		public void Terminate()
		{
			SendCommand(ExitCommand);
			_host.Dispose();
		}

		void SendCommand(string s)
		{
			byte[] bytesToWrite = Encoding.GetBytes(s + "\r\n");
			_host.StdIn.Write(bytesToWrite, 0, bytesToWrite.Length);
		}

		/// <summary>
		/// Kill and dispose of child process
		/// </summary>
		public void Dispose()
		{
			_host.Dispose();
		}

		/// <summary>
		/// True is child process is still running. False if child has exited
		/// </summary>
		/// <returns></returns>
		public bool IsAlive()
		{
			return _host.IsAlive();
		}
	}
}
