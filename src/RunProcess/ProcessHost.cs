using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using RunProcess.Internal;

namespace RunProcess
{
	public class ProcessHost : IDisposable
	{
		readonly Kernel32.Startupinfo _si;
		Kernel32.ProcessInformation _pi;
		readonly Pipe _stdIn;
		readonly Pipe _stdErr;
		readonly Pipe _stdOut;
		readonly string _executablePath;
		readonly string _workingDirectory;

		public ProcessHost(string executablePath, string workingDirectory)
		{
			_executablePath = executablePath;
			_workingDirectory = workingDirectory;
            
			_stdIn = new Pipe(Pipe.Direction.In);
			_stdErr = new Pipe(Pipe.Direction.Out);
			_stdOut = new Pipe(Pipe.Direction.Out);

			_si = new Kernel32.Startupinfo
			{
				wShowWindow = 0,
				dwFlags = Kernel32.StartfUsestdhandles | Kernel32.StartfUseshowwindow,
				hStdOutput = StdOut.WriteHandle,
				hStdError = StdErr.WriteHandle,
				hStdInput = StdIn.ReadHandle
			};

			_si.cb = (uint)Marshal.SizeOf(_si);
			_pi = new Kernel32.ProcessInformation();

			if (!Kernel32.CreateProcess(_executablePath, null, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, _workingDirectory, ref _si, out _pi))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		public Pipe StdIn { get { return _stdIn; } }
		public Pipe StdErr { get { return _stdErr; } }
		public Pipe StdOut { get { return _stdOut; } }


		~ProcessHost()
		{
			Dispose();
		}

		public void Dispose()
		{
			StdErr.Dispose();
			StdOut.Dispose();
			StdIn.Dispose();

			var processHandle = Interlocked.Exchange(ref _pi.hProcess, IntPtr.Zero);
			if (processHandle != IntPtr.Zero && !Kernel32.CloseHandle(processHandle))
				throw new Win32Exception(Marshal.GetLastWin32Error());

			var processMainThread = Interlocked.Exchange(ref _pi.hThread, IntPtr.Zero);
			if (!Kernel32.CloseHandle(processMainThread))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}
}