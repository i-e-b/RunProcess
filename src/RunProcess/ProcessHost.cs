using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using RunProcess.Internal;

namespace RunProcess
{
	/// <summary>
	/// Windows specific process host,
	/// more reliable than System.Diagnostics.Process
	/// </summary>
	public class ProcessHost : IDisposable
	{
		Kernel32.Startupinfo _si;
		Kernel32.ProcessInformation _pi;
		readonly Pipe _stdIn;
		readonly Pipe _stdErr;
		readonly Pipe _stdOut;
		readonly string _executablePath;
		readonly string _workingDirectory;



		/// <summary>
		/// Create a new process wrapper with an executable path and wroking directory.
		/// </summary>
		/// <param name="executablePath">Path to executable</param>
		/// <param name="workingDirectory">Starting directory for executable. May be left empty to inherit from parent</param>
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
		}

		/// <summary>
		/// Start the child process with no arguments
		/// </summary>
		public void Start()
		{
			Start(null);
		}

		/// <summary>
		/// Start the process with an argument string
		/// Arguments will be split and passed to the child by Windows APIs
		/// </summary>
		public void Start(string arguments)
		{
			string safePath;

			if (_executablePath.StartsWith("\"") && _executablePath.EndsWith("\"")) safePath = _executablePath;
			else safePath = "\"" + _executablePath + "\"";

			if (arguments != null)
			{
				safePath += " ";
				safePath += arguments;
			}

			if (!Kernel32.CreateProcess(null, safePath, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, _workingDirectory, ref _si, out _pi))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		/// <summary>
		/// Writable standard input pipe
		/// </summary>
		public Pipe StdIn { get { return _stdIn; } }

		/// <summary>
		/// Readable standard 'error' pipe.
		/// Note that most processes use this for human-readable output, not only for errors
		/// </summary>
		public Pipe StdErr { get { return _stdErr; } }

		/// <summary>
		/// Readable standard output pipe.
		/// This is usually used for machine-readable output
		/// </summary>
		public Pipe StdOut { get { return _stdOut; } }

		/// <summary>
		/// True if child process is still running. False if the child has exited.
		/// </summary>
		/// <returns></returns>
		public bool IsAlive()
		{
			return !WaitForExit(TimeSpan.FromMilliseconds(1));
		}

		/// <summary>
		/// Waits a given time for the process to exit.
		/// Returns true if exited within timeout, false if still running after timeout.
		/// Will return true if the process is unstarted or already exited.
		/// </summary>
		public bool WaitForExit(TimeSpan timeout)
		{
			int dummy;
			return WaitForExit(timeout, out dummy);
		}

		/// <summary>
		/// Waits a given time for the process to exit.
		/// Returns true if exited within timeout, false if still running after timeout.
		/// Will return true if the process is unstarted or already exited.
		/// Gives process exit code, or 0 if timed out.
		/// </summary>
		public bool WaitForExit(TimeSpan timeout, out int exitCode)
		{
			exitCode = 0;
			if ((_pi.hProcess == IntPtr.Zero) || (_pi.hThread == IntPtr.Zero)) return true;

			var processRef = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.Synchronize | Kernel32.ProcessAccessFlags.QueryInformation, false, _pi.dwProcessId);
			if (processRef == IntPtr.Zero) return true;
			var err = Marshal.GetLastWin32Error();

			try
			{
				if (err != 0) return true;

				var safeWait = (timeout.TotalMilliseconds >= long.MaxValue)
					? long.MaxValue - 1L
					: (long)timeout.TotalMilliseconds;

				var result = Kernel32.WaitForSingleObject(processRef, safeWait);

				switch (result)
				{
					case Kernel32.WaitResult.WaitFailed:
						throw new Win32Exception("Wait failed. Possibly failed to get Synchronize privilege");

					case Kernel32.WaitResult.WaitComplete:
						if (!Kernel32.GetExitCodeProcess(_pi.hProcess, out exitCode))
							throw new Win32Exception(Marshal.GetLastWin32Error());
						return true;

					case Kernel32.WaitResult.WaitTimeout:
						return false;

					case Kernel32.WaitResult.WaitAbandoned:
						return false;

					default: return false;
				}
			}
			finally
			{
				Kernel32.CloseHandle(processRef);
			}
		}

		/// <summary>
		/// Immediately terminate a process.
		/// This will not allow the child to clean up and should be used as a last resort.
		/// </summary>
		public void Kill()
		{
			if (!Kernel32.TerminateProcess(_pi.hProcess, 127))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		/// <summary>
		/// Get exit code from process. Throws if not exited.
		/// Use WaitForExit if you want to wait for a return code
		/// </summary>
		public int ExitCode()
		{
			int code;
			if (!WaitForExit(TimeSpan.Zero, out code))
				throw new Exception("Process not exited");
			return code;
		}

		/// <summary>
		/// Return hosted process id
		/// </summary>
		public uint ProcessId()
		{
			return _pi.dwProcessId;
		}

		/// <summary>
		/// Dispose on destruction
		/// </summary>
		~ProcessHost()
		{
			Dispose();
		}

		/// <summary>
		/// Release all process-related resources.
		/// The child process will be killed if still running
		/// </summary>
		public void Dispose()
		{
			try { Kill(); }
			catch (Win32Exception) { }

			StdErr.Dispose();
			StdOut.Dispose();
			StdIn.Dispose();

			var processMainThread = Interlocked.Exchange(ref _pi.hThread, IntPtr.Zero);
			if (processMainThread != IntPtr.Zero)
				Kernel32.CloseHandle(processMainThread);

			var processHandle = Interlocked.Exchange(ref _pi.hProcess, IntPtr.Zero);
			if (processHandle != IntPtr.Zero)
				Kernel32.CloseHandle(processHandle);
		}
	}
}