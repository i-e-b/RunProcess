using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using RunProcess.Internal;

namespace RunProcess
{
	public class ProcessHost : IDisposable
	{
		Kernel32.Startupinfo _si;
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
		}

        public void Start()
        {
            Start(null);
        }

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

		public Pipe StdIn { get { return _stdIn; } }
		public Pipe StdErr { get { return _stdErr; } }
		public Pipe StdOut { get { return _stdOut; } }

        public bool IsAlive()
        {
            if ((_pi.hProcess == IntPtr.Zero) || (_pi.hThread == IntPtr.Zero)) return false;
            
            var processRef = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.Synchronize, false, _pi.dwProcessId);
            if (processRef == IntPtr.Zero) return false;
            var err = Marshal.GetLastWin32Error();

            Kernel32.WaitResult result;
			try
			{
				if (err == WindowsErrors.InvalidArgument) return false; // already closed
				if (err != 0) throw new Win32Exception(err);

				result = Kernel32.WaitForSingleObject(processRef, 1);
			}
            finally
			{
				Kernel32.CloseHandle(processRef);
			}

	        if (result == Kernel32.WaitResult.WaitFailed)
                throw new Win32Exception("Wait failed. Possibly failed to get Synchronize privilege");

            return (result == Kernel32.WaitResult.WaitTimeout);
        }

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
			if (processMainThread != IntPtr.Zero && !Kernel32.CloseHandle(processMainThread))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}
}