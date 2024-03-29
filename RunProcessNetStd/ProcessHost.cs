using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using RunProcessNetStd.Internal;
// ReSharper disable InconsistentNaming

namespace RunProcessNetStd
{
	/// <summary>
	/// Windows specific process host,
	/// more reliable than System.Diagnostics.Process
	/// </summary>
	public class ProcessHost : IDisposable
	{
		Kernel32.Startupinfo _si;
		Kernel32.ProcessInformation _pi;
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
            var isModern = Environment.OSVersion.Version?.Major >= 5;
			return (isWin32 && isModern);
		}

		/// <summary>
		/// Create a new process wrapper with an executable path and working directory.
		/// </summary>
		/// <param name="executablePath">Path to executable</param>
		/// <param name="workingDirectory">Starting directory for executable. May be left empty to inherit from parent</param>
		public ProcessHost(string executablePath, string? workingDirectory)
		{
			if (!HostIsCompatible()) throw new NotSupportedException("The host operating system is not compatible");

			_executablePath = executablePath;
			_workingDirectory = DefaultToCurrentIfEmpty(workingDirectory);

			StdIn = new Pipe(Pipe.Direction.In);
			StdErr = new Pipe(Pipe.Direction.Out);
			StdOut = new Pipe(Pipe.Direction.Out);

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

		string DefaultToCurrentIfEmpty(string? s)
		{
			return (string.IsNullOrWhiteSpace(s!)) ? (Directory.GetCurrentDirectory()) : (s);
		}

        /// <summary>
        /// Start child process, and automatically kill it if the parent process is ended.
        /// This simulates Unix child processes.
        /// </summary>
        /// <remarks>This is done by attaching a debugger to the child process. You will need enough permissions to attach.
        /// If you have the code to the child process, it would be simpler to have the child directly monitor its parent.</remarks>
        /// <param name="arguments">Arguments will be split and passed to the child by Windows APIs</param>
        /// <param name="environmentVariables">Optional: environment variables to pass to the child process</param>
        public void StartAsChild(string? arguments, IDictionary<string,string>? environmentVariables = null) {
            var safePath = WrapPathsInQuotes(_executablePath);

            if (arguments != null)
            {
                safePath += " ";
                safePath += arguments;
            }

            new Thread(() =>
            {
                // MUST create AND listen on the same thead!
                IntPtr environmentPtr = DictToBytePtr(environmentVariables);
                if (!Kernel32.CreateProcess(null!, safePath, IntPtr.Zero, IntPtr.Zero, true, (uint)(Kernel32.ProcessCreationFlags.DebugOnlyThisProcess), environmentPtr, _workingDirectory, ref _si, out _pi))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                Marshal.FreeHGlobal(environmentPtr);

                while (true)
                {
	                var debug_event = new Kernel32.DEBUG_EVENT {debugInfo = new byte[100]};

	                var gotEvent = Kernel32.WaitForDebugEvent(ref debug_event, int.MaxValue);
                    if (!gotEvent) continue;

                    if (debug_event.dwProcessId != _pi.dwProcessId) return; //Console.WriteLine("Received event for process " + debug_event.dwProcessId + ". We are expecting " + _pi.dwProcessId);
                    if (debug_event.dwDebugEventCode == Kernel32.DebugEventType.EXIT_PROCESS_DEBUG_EVENT) return; // child exited, end this thread

                    Kernel32.ContinueDebugEvent((uint)debug_event.dwProcessId, (uint)debug_event.dwThreadId, 0x10002);
                }
            })
            { IsBackground = true }.Start();

            for (int i = 0; i < 10; i++) // wait for child process to come up
            {
                if (IsAlive()) break;
                Thread.Sleep(100);
            }
        }

        private IntPtr DictToBytePtr(IDictionary<string, string>? environmentVariables)
        {
            if (environmentVariables == null) return IntPtr.Zero;
            
            var sb = new StringBuilder();
            foreach (var variable in environmentVariables)
            {
	            if (string.IsNullOrWhiteSpace(variable.Key!)) continue;
                sb.Append(variable.Key);
                sb.Append('=');
                sb.Append(variable.Value??"");
                sb.Append('\0');
            }
            sb.Append('\0');
            var managedBytes = Encoding.ASCII.GetBytes(sb.ToString());

            var rawBytes = Marshal.AllocHGlobal(managedBytes.Length);
            Marshal.Copy(managedBytes, 0, rawBytes, managedBytes.Length);
            return rawBytes;
        }

        /// <summary>
        /// Start the process with an argument string and environment variables.
        /// Arguments will be split and passed to the child by Windows APIs
        /// </summary>
        /// <param name="arguments">Arguments will be split and passed to the child by Windows APIs</param>
        /// <param name="environmentVariables">Optional: environment variables to pass to the child process</param>
        public void Start(string? arguments = null, IDictionary<string,string>? environmentVariables = null)
		{
			var safePath = WrapPathsInQuotes(_executablePath);

			if (arguments != null)
			{
				safePath += " ";
				safePath += arguments;
			}
            
            IntPtr environmentPtr = DictToBytePtr(environmentVariables);
			if (!Kernel32.CreateProcess(null!, safePath, IntPtr.Zero, IntPtr.Zero, true, 0, environmentPtr, _workingDirectory, ref _si, out _pi))
				throw new Win32Exception(Marshal.GetLastWin32Error());
            Marshal.FreeHGlobal(environmentPtr);
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
		public void StartAsAnotherUser(string domain, string user, string password, string? arguments)
		{
			var safePath = WrapPathsInQuotes(_executablePath);

			if (arguments != null)
			{
				safePath += " ";
				safePath += arguments;
			}

			if (!Kernel32.CreateProcessWithLogonW(
				user, domain, password, Kernel32.LogonFlags.NoFlags, null!, safePath, 0, IntPtr.Zero, _workingDirectory,
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
		public Pipe StdIn { get; }

	    /// <summary>
		/// Readable standard 'error' pipe.
		/// Note that most processes use this for human-readable output, not only for errors
		/// </summary>
		public Pipe StdErr { get; }

	    /// <summary>
		/// Readable standard output pipe.
		/// This is usually used for machine-readable output
		/// </summary>
		public Pipe StdOut { get; }

	    /// <summary>
		/// True if child process is still running. False if the child has exited.
		/// </summary>
		/// <returns></returns>
		public bool IsAlive()
		{
            if (_pi.dwProcessId < 2) return false;
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

			var processRef = WaitForProcessReference(out var err);

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
			if (!WaitForExit(TimeSpan.Zero, out var code))
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