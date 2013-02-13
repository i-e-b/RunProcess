using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using RunProcess.Internal;

namespace RunProcess
{
	public class InteractiveShell
	{
		Kernel32.Startupinfo _si;
		Kernel32.ProcessInformation _pi;
		Pipe _stdIn, _stdErr, _stdOut;

		public string ApplicationName { get; private set; }
		protected string Prompt { get; set; }
		protected string ExitCommand { get; set; }
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
				while (_stdErr.Peek() > 0)
				{
					bytesReadCount = _stdErr.Read(buffer, 0, bufferLength);
					stdErr.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				while (_stdOut.Peek() > 0)
				{
					bytesReadCount = _stdOut.Read(buffer, 0, bufferLength);
					stdOut.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				Thread.Sleep(20);
			}
			while (_stdErr.Peek() > 0)
			{
				bytesReadCount = _stdErr.Read(buffer, 0, bufferLength);
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
            _stdIn = new Pipe(Pipe.Direction.In);
            _stdErr = new Pipe(Pipe.Direction.Out);
            _stdOut = new Pipe(Pipe.Direction.Out);

			_si = new Kernel32.Startupinfo
			{
				wShowWindow = 0,
				dwFlags = Kernel32.StartfUsestdhandles | Kernel32.StartfUseshowwindow,
				hStdOutput = _stdOut.WriteHandle,
				hStdError = _stdErr.WriteHandle,
				hStdInput = _stdIn.ReadHandle
			};

			_si.cb = (uint)Marshal.SizeOf(_si);
			_pi = new Kernel32.ProcessInformation();

			if (!Kernel32.CreateProcess(applicationName, null, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, workDirectory, ref _si, out _pi))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			ApplicationName = applicationName;
		}

		/// <summary>
		/// Tell shell to exit and free resources.
		/// </summary>
		public void Terminate()
		{
			SendCommand(ExitCommand);

            _stdErr.Dispose();
            _stdOut.Dispose();
            _stdIn.Dispose();

			if (!Kernel32.CloseHandle(_pi.hProcess))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CloseHandle(_pi.hThread))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		void SendCommand(string s)
		{
			byte[] bytesToWrite = Encoding.GetBytes(s + "\r\n");
			_stdIn.Write(bytesToWrite, 0, bytesToWrite.Length);
		}
	}
}
