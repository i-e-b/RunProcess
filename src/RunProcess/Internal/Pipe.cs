using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace RunProcess.Internal
{
	public class Pipe : IDisposable
	{
		IntPtr _readHandle;
		IntPtr _writeHandle;

		public enum Direction
		{
			In,
			Out
		};

		public Pipe(Direction dir)
		{
			var _sa = new Kernel32.SecurityAttributes
			{
				bInheritHandle = true,
				lpSecurityDescriptor = IntPtr.Zero,
				length = Marshal.SizeOf(typeof (Kernel32.SecurityAttributes))
			};
			_sa.lpSecurityDescriptor = IntPtr.Zero;

			if (!Kernel32.CreatePipe(out _readHandle, out _writeHandle, ref _sa, 0))
				throw new Win32Exception(Marshal.GetLastWin32Error());

			if (dir == Direction.In)
			{
				if (!Kernel32.SetHandleInformation(_writeHandle, Kernel32.HandleFlagInherit, 0))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			else
			{
				if (!Kernel32.SetHandleInformation(_readHandle, Kernel32.HandleFlagInherit, 0))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}
        
		public unsafe void Write(byte[] buffer, int index, int count)
		{
			fixed (byte* p = buffer)
			{
				int n = 0;
				if (!Kernel32.WriteFile(_writeHandle, p + index, count, &n, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}

		public unsafe int Peek()
		{
			int n = 0;
			if (!Kernel32.PeekNamedPipe(_readHandle, IntPtr.Zero, 0, IntPtr.Zero, &n, IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return n;
		}

		public unsafe int Read(byte[] buffer, int index, int count)
		{
			int n = 0;
			fixed (byte* p = buffer)
			{
				if (!Kernel32.ReadFile(_readHandle, p + index, count, &n, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			return n;
		}

		public IntPtr ReadHandle { get { return _readHandle; } }
		public IntPtr WriteHandle { get { return _writeHandle; } }

		public void Dispose()
		{
			var local = Interlocked.Exchange(ref _writeHandle, IntPtr.Zero);
			if (local == IntPtr.Zero) return;

			if (!Kernel32.CloseHandle(local))
				throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		~Pipe()
		{
			Dispose();
		}
	}
}