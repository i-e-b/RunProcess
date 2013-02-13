namespace RunProcess
{
	using System;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;

	public class InteractiveShell
	{
		IntPtr _hStdoutR, _hStdoutW, _hStderrR, _hStderrW, _hStdinR, _hStdinW;
		Kernel32.SecurityAttributes _sa;
		Kernel32.Startupinfo _si;
		Kernel32.ProcessInformation _pi;

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
				while (Peek(_hStderrR) > 0)
				{
					bytesReadCount = Read(_hStderrR, buffer, 0, bufferLength);
					stdErr.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				while (Peek(_hStdoutR) > 0)
				{
					bytesReadCount = Read(_hStdoutR, buffer, 0, bufferLength);
					stdOut.Append(Encoding.GetString(buffer, 0, bytesReadCount));
				}
				Thread.Sleep(20);
			}
			while (Peek(_hStderrR) > 0)
			{
				bytesReadCount = Read(_hStderrR, buffer, 0, bufferLength);
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
			_sa = new Kernel32.SecurityAttributes
			{
				bInheritHandle = true,
				lpSecurityDescriptor = IntPtr.Zero,
				length = Marshal.SizeOf(typeof(Kernel32.SecurityAttributes))
			};
			_sa.lpSecurityDescriptor = IntPtr.Zero;

			if (!Kernel32.CreatePipe(out _hStdoutR, out _hStdoutW, ref _sa, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CreatePipe(out _hStderrR, out _hStderrW, ref _sa, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CreatePipe(out _hStdinR, out _hStdinW, ref _sa, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.SetHandleInformation(_hStdoutR, Kernel32.HandleFlagInherit, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.SetHandleInformation(_hStderrR, Kernel32.HandleFlagInherit, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.SetHandleInformation(_hStdinW, Kernel32.HandleFlagInherit, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());

			_si = new Kernel32.Startupinfo
			{
				wShowWindow = 0,
				dwFlags = Kernel32.StartfUsestdhandles | Kernel32.StartfUseshowwindow,
				hStdOutput = _hStdoutW,
				hStdError = _hStderrW,
				hStdInput = _hStdinR
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
			if (!Kernel32.CloseHandle(_hStderrW))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CloseHandle(_hStdoutW))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CloseHandle(_hStdinW))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CloseHandle(_pi.hProcess))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			if (!Kernel32.CloseHandle(_pi.hThread))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		static unsafe void Write(IntPtr h, byte[] buffer, int index, int count)
		{
			fixed (byte* p = buffer)
			{
			    int n = 0;
				if (!Kernel32.WriteFile(h, p + index, count, &n, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}

		static unsafe int Peek(IntPtr h)
		{
			int n = 0;
			if (!Kernel32.PeekNamedPipe(h, IntPtr.Zero, 0, IntPtr.Zero, &n, IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return n;
		}

		static unsafe int Read(IntPtr h, byte[] buffer, int index, int count)
		{
			int n = 0;
			fixed (byte* p = buffer)
			{
				if (!Kernel32.ReadFile(h, p + index, count, &n, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			return n;
		}

		void SendCommand(string s)
		{
			byte[] bytesToWrite = Encoding.GetBytes(s + "\r\n");
			Write(_hStdinW, bytesToWrite, 0, bytesToWrite.Length);
		}
	}
}
