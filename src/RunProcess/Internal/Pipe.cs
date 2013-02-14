using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RunProcess.Internal
{
	public class Pipe : IDisposable
	{
		readonly Direction _dir;
		readonly IntPtr _readHandle;
		IntPtr _writeHandle;

		public enum Direction
		{
			In,
			Out
		};

		public Pipe(Direction dir)
		{
			_dir = dir;
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
        
        /// <summary>
        /// Write a byte buffer to the pipe
        /// </summary>
		public unsafe void Write(byte[] buffer, int index, int count)
		{
			if (_dir == Direction.Out)
	            throw new Exception("Can't write to an outbound pipe");

			fixed (byte* p = buffer)
			{
				int n = 0;
				if (!Kernel32.WriteFile(_writeHandle, p + index, count, &n, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
		}

        /// <summary>
        /// Return the number of bytes available on the pipe
        /// </summary>
		public unsafe int Peek()
		{
			if (_dir == Direction.In)
	            throw new Exception("Can't read from an inbound pipe");

			int n = 0;
			if (!Kernel32.PeekNamedPipe(_readHandle, IntPtr.Zero, 0, IntPtr.Zero, (IntPtr)(&n), IntPtr.Zero))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return n;
		}
        
        /// <summary>
        /// Read from the pipe into a byte buffer without removing from the pipe,
        /// blocking until buffer has at least some data.
        /// Use Peek() to determine if a read would be blocking.
        /// Returns number of bytes read
        /// </summary>
		public unsafe int PeekRead(byte[] buffer, int index, int count)
		{
			if (_dir == Direction.In)
	            throw new Exception("Can't read from an inbound pipe");

			int n = 0;
			fixed (byte* p = buffer)
			{
				if (!Kernel32.PeekNamedPipe(_readHandle, (IntPtr)p + index, count, (IntPtr)(&n), IntPtr.Zero, IntPtr.Zero))
					throw new Win32Exception(Marshal.GetLastWin32Error());
			}
	        return n;
		}

        /// <summary>
        /// Read from the pipe into a byte buffer, blocking until buffer has at least some data.
        /// Use Peek() to determine if a read would be blocking.
        /// </summary>
		public unsafe int Read(byte[] buffer, int index, int count)
		{
			if (_dir == Direction.In)
	            throw new Exception("Can't read from an inbound pipe");

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

        /// <summary>
        /// Read all available data on the pipe as text
        /// </summary>
		public string ReadAllText(Encoding encoding)
		{
            var sb = new StringBuilder();
            var buf = new byte[1024];
	        while (Peek() > 0)
            {
	            var len = Read(buf,0,1024);
	            sb.Append(encoding.GetString(buf,0,len));
            }

	        return sb.ToString();
		}

        /// <summary>
        /// Write a text string to the pipe.
        /// No line endings are added.
        /// </summary>
		public void WriteAllText(Encoding encoding, string message)
		{
            var bytes = encoding.GetBytes(message);
			Write(bytes, 0, bytes.Length);
		}
        
        /// <summary>
        /// Write a text string to the pipe, adding a line terminator.
        /// </summary>
		public void WriteLine(Encoding encoding, string message)
		{
			var bytes = encoding.GetBytes(message + Environment.NewLine);
			Write(bytes, 0, bytes.Length);
		}

        /// <summary>
        /// Read text from a pipe, up until the next line ending.
        /// The line ending will be consumed. Will wait up to timeout
        /// for a line ending while no data is being read.
        /// </summary>
        /// <remarks>This reads one byte at a time, and is therefore quite slow!</remarks>
		public string ReadLine(Encoding encoding, TimeSpan timeout)
		{
            var sb = new StringBuilder();
            var buf = new byte[2];

            var maxMilliseconds = (long)timeout.TotalMilliseconds;
            var sw = new Stopwatch();

            while (sw.ElapsedMilliseconds < maxMilliseconds)
            {
	            while (Peek() > 0)
	            {
                    sw.Restart();


		            // This isn't a great way of doing it...
		            var len = Read(buf, 0, encoding.IsSingleByte ? 1 : 2);
                    if (len < 1) break; // break out of the peek loop

		            var str = encoding.GetString(buf, 0, len);

                    if (IsLineEnd(str[0]))
                    {
                        if (str[0] == '\r' && Peek() > 0) // Might need to consume "\r\n"
                        {
                            len = PeekRead(buf, 0, encoding.IsSingleByte ? 1 : 2);
                            str = encoding.GetString(buf, 0, len);
                            if (str[0] == '\n') Read(buf, 0, encoding.IsSingleByte ? 1 : 2);
                        }
                        return sb.ToString();
                    }

		            sb.Append(encoding.GetString(buf, 0, len));
	            }
            }

	        return sb.ToString();
		}

		static bool IsLineEnd(char c)
		{
			switch ((int)c)
			{
                case 0x000A: //LF
				case 0x000B: //VT
				case 0x000C: //FF
				case 0x000D: //CR
				case 0x0085: //NEL
				case 0x2028: //LS
				case 0x2029: //PS
                    return true;

				default:
                    return false;
			}
		}
	}
}