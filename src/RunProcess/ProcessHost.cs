using System;
using System.ComponentModel;
using System.IO;
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
		int _lastExitCode;

		/// <summary>
		/// Returns true if the host operating system can run
		/// ProcessHost. If false, trying to create a new ProcessHost
		/// will result in an exception
		/// </summary>
		public static bool HostIsCompatible()
		{
			var isWin32 = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var isModern = Environment.OSVersion.Version.Major >= 5;
			return (isWin32 && isModern);
		}

		/// <summary>
		/// Create a new process wrapper with an executable path and wroking directory.
		/// </summary>
		/// <param name="executablePath">Path to executable</param>
		/// <param name="workingDirectory">Starting directory for executable. May be left empty to inherit from parent</param>
		public ProcessHost(string executablePath, string workingDirectory)
		{
			if (!HostIsCompatible()) throw new NotSupportedException("The host operating system is not compatible");

			_executablePath = executablePath;
			_workingDirectory = DefaultToCurrentIfEmpty(workingDirectory);

			_stdIn = new Pipe(Pipe.Direction.In);
			_stdErr = new Pipe(Pipe.Direction.Out);
			_stdOut = new Pipe(Pipe.Direction.Out);

			_lastExitCode = 0;

			_si = new Kernel32.Startupinfo
			{
				wShowWindow = 0,
				dwFlags = Kernel32.StartfUsestdhandles | Kernel32.StartfUseshowwindow,
				hStdOutput = StdOut.WriteHandle,
				hStdError = StdErr.WriteHandle,
				hStdInput = StdIn.ReadHandle
			};

			_si.cb = (uint) Marshal.SizeOf(_si);
			_pi = new Kernel32.ProcessInformation();
		}

		string DefaultToCurrentIfEmpty(string s)
		{
			return (string.IsNullOrWhiteSpace(s)) ? (Directory.GetCurrentDirectory()) : (s);
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
			var safePath = WrapPathsInQuotes(_executablePath);

			if (arguments != null)
			{
				safePath += " ";
				safePath += arguments;
			}

			if (!Kernel32.CreateProcess(null, safePath, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, _workingDirectory, ref _si, out _pi))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		/// <summary>
		/// Start the process with an argument string, impersonating another user
		/// with the supplied credentials.
		/// Arguments will be split and passed to the child by Windows APIs.
		/// Password is sent in the clear... caution!
		/// </summary>
		/// <param name="domain">Windows domain that will validate the user. This may be null</param>
		/// <param name="user">Name of the account to impersonate. If domain is null, should use user@domain style.</param>
		/// <param name="password">Clear text password of user account. Careful!</param>
		/// <param name="arguments">Arguments to supply to the process</param>
		public void StartAsAnotherUser(string domain, string user, string password, string arguments)
		{
			var safePath = WrapPathsInQuotes(_executablePath);

			if (arguments != null)
			{
				safePath += " ";
				safePath += arguments;
			}

			if (!Kernel32.CreateProcessWithLogonW(
				user, domain, password, Kernel32.LogonFlags.NoFlags, null, safePath, 0, IntPtr.Zero, _workingDirectory,
				ref _si, out _pi) )
				throw new Win32Exception(Marshal.GetLastWin32Error());

		}

		static string WrapPathsInQuotes(string path)
		{
			if (path.Contains("\"")) return path; // already quoted, or would create ambiguous quotation
			
			return "\"" + path + "\""; // otherwise, quote all paths by default
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
			return WaitForExit(timeout, out _lastExitCode);
		}

		/// <summary>
		/// Waits a given time for the process to exit.
		/// Returns true if exited within timeout, false if still running after timeout.
		/// Will return true if the process is unstarted or already exited.
		/// Gives process exit code, or 0 if timed out.
		/// </summary>
		public bool WaitForExit(TimeSpan timeout, out int exitCode)
		{
			exitCode = _lastExitCode;
			if ((_pi.hProcess == IntPtr.Zero) || (_pi.hThread == IntPtr.Zero)) return true;

			int err;
			var processRef = WaitForProcessReference(out err);

			try
			{
				if (err != 0) throw new Win32Exception(err, "Could not find process");
				
				var requestedWait = (uint)timeout.TotalMilliseconds;
				var safeWait = Math.Min(uint.MaxValue - 1, requestedWait);

				var result = Kernel32.WaitForSingleObject(processRef, safeWait);

				switch (result)
				{
					case Kernel32.WaitResult.WaitFailed:
						throw new Win32Exception("Wait failed. Possibly failed to get Synchronize privilege");

					case Kernel32.WaitResult.WaitComplete:
						if (!Kernel32.GetExitCodeProcess(_pi.hProcess, out exitCode))
							throw new Win32Exception(Marshal.GetLastWin32Error());

						if (exitCode == -1) exitCode = _lastExitCode;
						else _lastExitCode = exitCode;
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

		IntPtr WaitForProcessReference(out int err)
		{
			err = 0;

			for (int i = 0; i < 10; i++)
			{
				var processRef = Kernel32.OpenProcess(
					  Kernel32.ProcessAccessFlags.Synchronize
					| Kernel32.ProcessAccessFlags.QueryInformation,
					false, _pi.dwProcessId);

				if (processRef == IntPtr.Zero)
					err = Marshal.GetLastWin32Error();

				if (err == 0) return processRef;
				Thread.Sleep(100);
			}

			return IntPtr.Zero;
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